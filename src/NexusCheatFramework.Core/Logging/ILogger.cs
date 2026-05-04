using System;

namespace NexusCheatFramework.Logging
{
    public interface ILogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception? ex = null);
    }

    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? ex = null) { }
    }

    public sealed class ConsoleLogger : ILogger
    {
        private readonly bool _verbose;
        public ConsoleLogger(bool verbose = false) { _verbose = verbose; }
        public void Debug(string message) { if (_verbose) Console.WriteLine($"[DBG] {message}"); }
        public void Info(string message) => Console.WriteLine($"[INF] {message}");
        public void Warn(string message) => Console.WriteLine($"[WRN] {message}");
        public void Error(string message, Exception? ex = null)
            => Console.Error.WriteLine($"[ERR] {message}{(ex != null ? " :: " + ex.Message : "")}");
    }
}
