using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class ConsoleLogBuffer
    {
        private readonly int _maxSize;
        private readonly Queue<LogEntry> _buffer;

        public ConsoleLogBuffer(int maxSize = 200)
        {
            _maxSize = maxSize;
            _buffer = new Queue<LogEntry>(maxSize);
        }

        public void Start()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        public void Stop()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (_buffer.Count >= _maxSize) _buffer.Dequeue();
            _buffer.Enqueue(new LogEntry
            {
                Message = condition,
                StackTrace = stackTrace,
                Type = type.ToString(),
                Timestamp = DateTime.UtcNow
            });
        }

        public List<LogEntry> GetLogs(int count = 50, string logType = null)
        {
            var result = new List<LogEntry>();
            foreach (var entry in _buffer)
            {
                if (logType != null && entry.Type != logType) continue;
                result.Add(entry);
            }
            int skip = Math.Max(0, result.Count - count);
            return result.GetRange(skip, result.Count - skip);
        }

        public int TotalBuffered => _buffer.Count;

        public class LogEntry
        {
            public string Message;
            public string StackTrace;
            public string Type;
            public DateTime Timestamp;
        }
    }
}
