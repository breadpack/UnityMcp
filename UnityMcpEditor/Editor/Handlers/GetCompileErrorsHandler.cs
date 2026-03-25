using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace BreadPack.Mcp.Unity
{
    public class GetCompileErrorsHandler : IRequestHandler
    {
        private static readonly Regex CompilerMessagePattern =
            new Regex(@"(.+?)\((\d+),(\d+)\):\s*(error|warning)\s+(CS\d+):\s*(.+)",
                RegexOptions.Compiled);

        public string ToolName => "unity_get_compile_errors";

        public object Handle(JObject @params)
        {
            bool isCompiling = EditorApplication.isCompiling;

            var entries = GetLogEntries();

            var compileErrors = entries
                .Where(e => e.mode == 1 && IsCompilerMessage(e.message, "error"))
                .Select(e => ParseCompilerMessage(e.message))
                .ToList();

            var compileWarnings = entries
                .Where(e => e.mode == 2 && IsCompilerMessage(e.message, "warning"))
                .Select(e => ParseCompilerMessage(e.message))
                .Take(50)
                .ToList();

            var assemblies = CompilationPipeline.GetAssemblies()
                .Select(a => a.name)
                .ToArray();

            return new
            {
                isCompiling,
                hasErrors = compileErrors.Count > 0,
                errorCount = compileErrors.Count,
                warningCount = compileWarnings.Count,
                errors = compileErrors,
                warnings = compileWarnings,
                assemblies
            };
        }

        private static bool IsCompilerMessage(string message, string level)
        {
            return message.Contains($"{level} CS") || message.Contains($"{level}:");
        }

        private static object ParseCompilerMessage(string message)
        {
            var match = CompilerMessagePattern.Match(message);
            if (match.Success)
            {
                return new
                {
                    file = match.Groups[1].Value,
                    line = int.Parse(match.Groups[2].Value),
                    column = int.Parse(match.Groups[3].Value),
                    level = match.Groups[4].Value,
                    code = match.Groups[5].Value,
                    message = match.Groups[6].Value
                };
            }

            return new
            {
                file = (string)null,
                line = 0,
                column = 0,
                level = (string)null,
                code = (string)null,
                message
            };
        }

        private static List<(string message, int mode)> GetLogEntries()
        {
            var result = new List<(string, int)>();

            try
            {
                var logEntriesType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries")
                    ?? typeof(EditorApplication).Assembly.GetType("UnityEditorInternal.LogEntries");

                if (logEntriesType == null)
                    return result;

                var flags = BindingFlags.Static | BindingFlags.Public;
                var startMethod = logEntriesType.GetMethod("StartGettingEntries", flags);
                var endMethod = logEntriesType.GetMethod("EndGettingEntries", flags);
                var getCountMethod = logEntriesType.GetMethod("GetCount", flags);
                var getEntryMethod = logEntriesType.GetMethod("GetLinesAndModeFromEntryInternal", flags);

                if (startMethod == null || endMethod == null || getCountMethod == null || getEntryMethod == null)
                    return result;

                startMethod.Invoke(null, null);
                try
                {
                    int count = (int)getCountMethod.Invoke(null, null);
                    int limit = Math.Min(count, 500);

                    for (int i = 0; i < limit; i++)
                    {
                        // GetLinesAndModeFromEntryInternal(int row, int numberOfLines, ref int mask, ref string outString)
                        var args = new object[] { i, 1, 0, "" };
                        getEntryMethod.Invoke(null, args);
                        int mode = (int)args[2];
                        string text = (string)args[3];

                        if (!string.IsNullOrEmpty(text))
                        {
                            result.Add((text, mode));
                        }
                    }
                }
                finally
                {
                    endMethod.Invoke(null, null);
                }
            }
            catch
            {
                // Reflection-based access failed; return empty list
            }

            return result;
        }
    }
}
