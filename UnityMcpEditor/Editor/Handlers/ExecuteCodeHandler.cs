using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
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
            // Force a short temp dir so the mono cmdline stays under Windows MAX_PATH (260)
            // and the 8191-char cmdline limit. Default temp under user profile produced
            // "파일 이름이나 확장명이 너무 깁니다" with hundreds of /r:full-path refs.
            var shortTemp = @"C:\Tmp\McpExec";
            try { System.IO.Directory.CreateDirectory(shortTemp); } catch { }
            var compilerParams = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false,
                TreatWarningsAsErrors = false,
                TempFiles = new TempFileCollection(shortTemp, keepFiles: false)
            };

            // Pass references through a response file (@file) instead of inline /r: arguments.
            // Capping refs by path length (the previous approach) was not enough on large projects:
            // hundreds of assemblies still overflow the command line ("The filename or extension is too
            // long"). A response file keeps the command line tiny and lets us reference *every* loaded
            // assembly, so user code can use any type available in the editor.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rspBuilder = new System.Text.StringBuilder();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.IsDynamic) continue;
                    var loc = asm.Location;
                    if (string.IsNullOrEmpty(loc)) continue;
                    if (!seen.Add(loc)) continue;
                    rspBuilder.Append("-r:\"").Append(loc).Append('"').Append('\n');
                }
                catch
                {
                    // Skip assemblies that can't be referenced
                }
            }

            // Write the response file inside the short temp dir so its own path stays short too.
            var referenceFile = System.IO.Path.Combine(shortTemp, $"refs_{Guid.NewGuid():N}.rsp");
            System.IO.File.WriteAllText(referenceFile, rspBuilder.ToString());
            compilerParams.CompilerOptions = $"@\"{referenceFile}\"";

            CompilerResults results;
            try
            {
                results = provider.CompileAssemblyFromSource(compilerParams, fullSource);
            }
            catch (Exception ex)
            {
                throw new Exception($"Compilation failed: {ex.Message}");
            }
            finally
            {
                try { System.IO.File.Delete(referenceFile); } catch { /* best-effort temp cleanup */ }
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

            // Run the compiled code directly on the calling thread. The request router already invokes every
            // handler on the Unity main thread (McpServerBootstrap.HandleRequestAsync → MainThreadDispatcher),
            // so the code here is already on the main thread and can call editor APIs (PrefabUtility,
            // AssetDatabase, scene ops). Marshalling again would self-deadlock the main thread, and the old
            // worker-thread approach broke every main-thread API. Trade-off: no async timeout/abort is possible
            // on the main thread, so a runaway script will block the editor until it returns.
            var compiledAssembly = results.CompiledAssembly;
            var runnerType = compiledAssembly.GetType("McpCodeRunner");
            var runMethod = runnerType.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

            object returnValue = null;
            Exception executionError = null;

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
