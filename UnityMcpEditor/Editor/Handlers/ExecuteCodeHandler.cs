using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CSharp;
using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity
{
    public class ExecuteCodeHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_execute_code";

        public async Task<object> HandleAsync(JObject @params)
        {
            var code = @params?["code"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("'code' parameter is required");

            var usingsParam = @params?["usings"]?.Value<string>() ?? "UnityEngine,UnityEditor";
            var fullSource = BuildSource(code, usingsParam);

            // 1) 컴파일 — 외부 csc 프로세스 호출이라 메인 스레드가 필요 없다. 백그라운드 스레드에서
            //    수행해 컴파일 동안 Editor 메인 스레드(및 다른 요청)가 멈추지 않게 한다.
            CompileOutcome compiled;
            try
            {
                compiled = await Task.Run(() => Compile(fullSource));
            }
            catch (Exception ex)
            {
                throw new Exception($"Compilation failed: {ex.Message}");
            }

            if (compiled.Errors != null)
            {
                return new
                {
                    success = false,
                    compilationErrors = compiled.Errors,
                    generatedSource = fullSource
                };
            }

            // 2) 실행 — 반드시 메인 스레드. AssetDatabase/PrefabUtility 등 Editor API는 메인 스레드 전용이다.
            //    여기서 명시적으로 마샬링하므로 await 이후 재개 스레드와 무관하게 안전하다.
            //    주의: 사용자 코드가 무한 루프이거나 메인 스레드를 다시 동기 대기(.Result/.Wait/while(true))하면
            //    메인 스레드를 점유해 Editor가 멈춘다. 메인 스레드 작업은 abort 불가이므로, 이 경우의 복구는
            //    Bridge 측 요청 타임아웃(연결 재설정)에 의존한다.
            return await MainThreadDispatcher.RunOnMainThread(() => Execute(compiled.Assembly));
        }

        private static string BuildSource(string code, string usingsParam)
        {
            var usingStatements = string.Join("\n", usingsParam
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrEmpty(u))
                .Select(u => $"using {u};"));

            var wrappedCode = WrapCode(code);

            return $@"
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
        }

        private readonly struct CompileOutcome
        {
            public readonly Assembly Assembly;
            public readonly string[] Errors;

            private CompileOutcome(Assembly assembly, string[] errors)
            {
                Assembly = assembly;
                Errors = errors;
            }

            public static CompileOutcome Ok(Assembly assembly) => new(assembly, null);
            public static CompileOutcome Failed(string[] errors) => new(null, errors);
        }

        private static CompileOutcome Compile(string fullSource)
        {
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
                return CompileOutcome.Failed(errors);
            }

            return CompileOutcome.Ok(results.CompiledAssembly);
        }

        private static object Execute(Assembly compiledAssembly)
        {
            var runnerType = compiledAssembly.GetType("McpCodeRunner");
            var runMethod = runnerType.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

            object returnValue;
            try
            {
                returnValue = runMethod.Invoke(null, null);
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                return new { success = false, runtimeError = inner.Message, stackTrace = inner.StackTrace };
            }
            catch (Exception ex)
            {
                return new { success = false, runtimeError = ex.Message, stackTrace = ex.StackTrace };
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
