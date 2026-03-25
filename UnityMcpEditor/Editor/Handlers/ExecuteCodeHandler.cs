using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CSharp;
using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity
{
    public class ExecuteCodeHandler : IRequestHandler
    {
        public string ToolName => "unity_execute_code";

        private const int TimeoutMs = 10000;

        public object Handle(JObject @params)
        {
            var code = @params?["code"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("'code' parameter is required");

            var usingsParam = @params?["usings"]?.Value<string>() ?? "UnityEngine,UnityEditor";

            var usingStatements = string.Join("\n", usingsParam
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrEmpty(u))
                .Select(u => $"using {u};"));

            // Determine if code needs auto-return wrapping
            var wrappedCode = WrapCode(code);

            var fullSource = $@"
{usingStatements}
using System;
using System.Collections.Generic;
using System.Linq;

public static class McpCodeRunner
{{
    public static object Run()
    {{
        {wrappedCode}
    }}
}}";

            // Compile
            var provider = new CSharpCodeProvider();
            var compilerParams = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false,
                TreatWarningsAsErrors = false
            };

            // Add references from loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
                    {
                        compilerParams.ReferencedAssemblies.Add(asm.Location);
                    }
                }
                catch
                {
                    // Skip assemblies that can't be referenced
                }
            }

            CompilerResults results;
            try
            {
                results = provider.CompileAssemblyFromSource(compilerParams, fullSource);
            }
            catch (Exception ex)
            {
                throw new Exception($"Compilation failed: {ex.Message}");
            }

            if (results.Errors.HasErrors)
            {
                var errors = results.Errors.Cast<CompilerError>()
                    .Where(e => !e.IsWarning)
                    .Select(e => $"Line {e.Line}: {e.ErrorText}")
                    .ToArray();

                return new
                {
                    success = false,
                    compilationErrors = errors,
                    generatedSource = fullSource
                };
            }

            // Execute with timeout
            var compiledAssembly = results.CompiledAssembly;
            var runnerType = compiledAssembly.GetType("McpCodeRunner");
            var runMethod = runnerType.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

            object returnValue = null;
            Exception executionError = null;

            var thread = new Thread(() =>
            {
                try
                {
                    returnValue = runMethod.Invoke(null, null);
                }
                catch (TargetInvocationException tie)
                {
                    executionError = tie.InnerException ?? tie;
                }
                catch (Exception ex)
                {
                    executionError = ex;
                }
            });

            thread.Start();
            if (!thread.Join(TimeoutMs))
            {
                thread.Abort();
                throw new TimeoutException(
                    $"Code execution timed out after {TimeoutMs / 1000} seconds. " +
                    "Possible infinite loop or long-running operation.");
            }

            if (executionError != null)
            {
                return new
                {
                    success = false,
                    runtimeError = executionError.Message,
                    stackTrace = executionError.StackTrace
                };
            }

            return new
            {
                success = true,
                result = returnValue?.ToString(),
                resultType = returnValue?.GetType().FullName ?? "null"
            };
        }

        private static string WrapCode(string code)
        {
            var trimmed = code.Trim().TrimEnd(';');

            // If code already contains "return", use as-is
            if (code.Contains("return ") || code.Contains("return;"))
                return code;

            // If code contains multiple statements (semicolons not inside strings),
            // wrap last expression as return
            var lines = code.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToArray();

            if (lines.Length == 1)
            {
                // Single expression - auto-return
                return $"return (object)({trimmed});";
            }

            // Multiple lines: return the last expression, keep others as statements
            var allButLast = string.Join("\n        ", lines.Take(lines.Length - 1));
            var lastLine = lines.Last().TrimEnd(';');

            return $@"{allButLast}
        return (object)({lastLine});";
        }
    }
}
