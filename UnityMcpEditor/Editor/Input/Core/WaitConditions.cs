using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreadPack.Mcp.Unity.Input
{
    public sealed class WaitResult
    {
        public string Kind;
        public bool Satisfied;
        public bool TimedOut;
        public long ElapsedMs;

        public JObject ToJson()
        {
            return new JObject
            {
                ["kind"] = Kind,
                ["satisfied"] = Satisfied,
                ["timedOut"] = TimedOut,
                ["elapsedMs"] = ElapsedMs
            };
        }
    }

    public static class WaitConditions
    {
        public static async Task<WaitResult> EvaluateAsync(JObject spec)
        {
            if (spec == null) return null;

            var kind = spec["kind"]?.Value<string>() ?? throw new ArgumentException("waitFor.kind 필요");
            var timeoutMs = spec["timeoutMs"]?.Value<int?>() ?? 3000;

            return kind switch
            {
                "objectActive" => await PollAsync(kind, timeoutMs, () => CheckObjectActive(spec)),
                "objectExists" => await PollAsync(kind, timeoutMs, () => CheckObjectExists(spec)),
                "consoleLogContains" => await CheckConsoleLogAsync(spec, timeoutMs),
                "sceneLoaded" => await PollAsync(kind, timeoutMs, () => CheckSceneLoaded(spec)),
                "frames" => await WaitFramesAsync(spec),
                _ => throw new ArgumentException($"알 수 없는 waitFor.kind: {kind}")
            };
        }

        private static async Task<WaitResult> PollAsync(string kind, int timeoutMs, Func<bool> predicate)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (predicate())
                    return new WaitResult { Kind = kind, Satisfied = true, ElapsedMs = sw.ElapsedMilliseconds };
                await MainThreadDispatcher.DelayFrames(1);
            }
            return new WaitResult { Kind = kind, Satisfied = predicate(), TimedOut = true, ElapsedMs = sw.ElapsedMilliseconds };
        }

        private static bool CheckObjectActive(JObject spec)
        {
            // objectActive/Exists는 GameObject로 해석 가능한 target이 필수.
            // position/worldPoint는 GameObject가 아니라 단순 좌표라 해당 조건이 평가 불가능.
            if (spec["target"] == null) return false;
            try
            {
                var ts = TargetSpec.Parse(spec);
                var resolved = TargetResolver.Resolve(ts);
                var expected = spec["expected"]?.Value<bool?>() ?? true;
                return (resolved.GameObject?.activeInHierarchy ?? false) == expected;
            }
            catch { return false; }
        }

        private static bool CheckObjectExists(JObject spec)
        {
            if (spec["target"] == null) return false;
            try
            {
                var ts = TargetSpec.Parse(spec);
                var go = ResolveOrNull(ts);
                var expected = spec["expected"]?.Value<bool?>() ?? true;
                return (go != null) == expected;
            }
            catch { return false; }
        }

        private static GameObject ResolveOrNull(TargetSpec ts)
        {
            try { return TargetResolver.Resolve(ts).GameObject; }
            catch { return null; }
        }

        private static bool CheckSceneLoaded(JObject spec)
        {
            var name = spec["name"]?.Value<string>() ?? "";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == name && s.isLoaded) return true;
            }
            return false;
        }

        private static async Task<WaitResult> CheckConsoleLogAsync(JObject spec, int timeoutMs)
        {
            var pattern = spec["pattern"]?.Value<string>() ?? throw new ArgumentException("pattern 필요");
            var levelStr = spec["level"]?.Value<string>() ?? "Any";
            var regex = new Regex(pattern);
            var matched = false;

            void OnLog(string condition, string stack, LogType type)
            {
                if (matched) return;
                if (levelStr != "Any" && !MatchLevel(type, levelStr)) return;
                if (regex.IsMatch(condition)) matched = true;
            }

            Application.logMessageReceived += OnLog;
            var sw = Stopwatch.StartNew();
            try
            {
                while (sw.ElapsedMilliseconds < timeoutMs && !matched)
                {
                    await MainThreadDispatcher.DelayFrames(1);
                }
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
            return new WaitResult { Kind = "consoleLogContains", Satisfied = matched, TimedOut = !matched, ElapsedMs = sw.ElapsedMilliseconds };
        }

        private static bool MatchLevel(LogType type, string levelStr)
        {
            return levelStr switch
            {
                "Log" => type == LogType.Log,
                "Warning" => type == LogType.Warning,
                "Error" => type == LogType.Error || type == LogType.Exception || type == LogType.Assert,
                _ => true
            };
        }

        private static async Task<WaitResult> WaitFramesAsync(JObject spec)
        {
            var count = spec["count"]?.Value<int?>() ?? 1;
            var sw = Stopwatch.StartNew();
            await MainThreadDispatcher.DelayFrames(count);
            return new WaitResult { Kind = "frames", Satisfied = true, ElapsedMs = sw.ElapsedMilliseconds };
        }
    }
}
