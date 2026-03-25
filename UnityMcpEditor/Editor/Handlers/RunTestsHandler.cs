using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class RunTestsHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_run_tests";

        public async Task<object> HandleAsync(JObject @params)
        {
            var testMode = @params?["testMode"]?.Value<string>() ?? "EditMode";
            var testFilter = @params?["testFilter"]?.Value<string>();
            var categoryFilter = @params?["categoryFilter"]?.Value<string>();

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var tcs = new TaskCompletionSource<object>();

            var filter = new Filter
            {
                testMode = testMode switch
                {
                    "PlayMode" => TestMode.PlayMode,
                    "All" => TestMode.PlayMode | TestMode.EditMode,
                    _ => TestMode.EditMode
                }
            };

            if (!string.IsNullOrEmpty(testFilter))
                filter.testNames = new[] { testFilter };
            if (!string.IsNullOrEmpty(categoryFilter))
                filter.categoryNames = new[] { categoryFilter };

            var callbacks = new TestCallbacks(tcs);
            api.RegisterCallbacks(callbacks);

            try
            {
                api.Execute(new ExecutionSettings(filter));

                var timeoutTask = Task.Delay(300000); // 5 min timeout
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                api.UnregisterCallbacks(callbacks);

                if (completedTask != tcs.Task)
                    throw new TimeoutException("테스트 실행이 시간 초과되었습니다 (5분)");

                return await tcs.Task;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(api);
            }
        }

        private class TestCallbacks : ICallbacks
        {
            private readonly TaskCompletionSource<object> _tcs;
            private readonly List<object> _failed = new List<object>();
            private readonly List<string> _passed = new List<string>();
            private readonly List<string> _skipped = new List<string>();
            private readonly Stopwatch _stopwatch = new Stopwatch();
            private int _total;

            public TestCallbacks(TaskCompletionSource<object> tcs)
            {
                _tcs = tcs;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                _stopwatch.Start();
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _stopwatch.Stop();

                var duration = _stopwatch.Elapsed.TotalSeconds < 1
                    ? $"{_stopwatch.Elapsed.TotalMilliseconds:F0}ms"
                    : $"{_stopwatch.Elapsed.TotalSeconds:F1}s";

                var summary = new
                {
                    summary = new
                    {
                        total = _total,
                        passed = _passed.Count,
                        failed = _failed.Count,
                        skipped = _skipped.Count,
                        duration
                    },
                    failed = _failed,
                    passed = _passed,
                    skipped = _skipped
                };

                _tcs.TrySetResult(summary);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren)
                    return;

                _total++;

                var resultState = result.TestStatus.ToString();

                if (resultState == "Passed")
                {
                    _passed.Add(result.Test.Name);
                }
                else if (resultState == "Failed")
                {
                    _failed.Add(new
                    {
                        name = result.Test.Name,
                        message = result.Message ?? "",
                        stackTrace = result.StackTrace ?? ""
                    });
                }
                else
                {
                    _skipped.Add(result.Test.Name);
                }
            }
        }
    }
}
