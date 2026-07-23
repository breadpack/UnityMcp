using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using BreadPack.Mcp.Unity;

namespace BreadPack.Mcp.Unity.Input
{
    public static class ResultSnapshot
    {
        public static async Task<JObject> CaptureAsync(
            CommonOptions opts,
            Func<JObject> resolvedJsonProvider)
        {
            var response = new JObject
            {
                ["ok"] = true,
                ["resolved"] = resolvedJsonProvider()
            };

            // 콘솔 로그 캡처 시작 (waitFrames + waitFor 동안 기록)
            var logs = new List<JObject>();
            void OnLog(string condition, string stack, LogType type)
            {
                logs.Add(new JObject
                {
                    ["level"] = type.ToString(),
                    ["message"] = condition
                });
            }
            if (opts.CaptureResult) Application.logMessageReceived += OnLog;

            try
            {
                // 1. waitFrames 진행
                if (opts.WaitFrames > 0)
                    await MainThreadDispatcher.DelayFrames(opts.WaitFrames);

                // 2. waitFor 평가
                if (opts.WaitFor != null)
                {
                    var waitResult = await WaitConditions.EvaluateAsync(opts.WaitFor);
                    if (waitResult != null) response["waitFor"] = waitResult.ToJson();
                }

                // 3. captureResult: 스크린샷 + 로그
                if (opts.CaptureResult)
                {
                    var screenshot = await GameViewCaptureService.CaptureEncodedAsync(75, 0);
                    response["screenshotBase64"] = screenshot.imageBase64;
                    response["mimeType"] = screenshot.mimeType;
                    response["width"] = screenshot.width;
                    response["height"] = screenshot.height;
                    response["consoleLogsDelta"] = JArray.FromObject(logs);
                }
            }
            finally
            {
                if (opts.CaptureResult) Application.logMessageReceived -= OnLog;
            }

            return response;
        }

    }
}
