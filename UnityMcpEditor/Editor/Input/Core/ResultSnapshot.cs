using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

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
                    var screenshot = await TakeScreenshotInline();
                    response["screenshotBase64"] = screenshot.base64;
                    response["mimeType"] = screenshot.mime;
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

        private static async Task<(string base64, string mime, int width, int height)> TakeScreenshotInline()
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            await MainThreadDispatcher.DelayFrames(1);

            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
                return ("", "image/jpeg", 0, 0);

            try
            {
                var bytes = tex.EncodeToJPG(75);
                return (Convert.ToBase64String(bytes), "image/jpeg", tex.width, tex.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
