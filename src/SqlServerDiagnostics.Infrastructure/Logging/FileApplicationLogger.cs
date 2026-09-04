using System;
using System.IO;
using System.Text;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Infrastructure.Logging
{
    public sealed class FileApplicationLogger : IApplicationLogger
    {
        private readonly string logDirectory;
        private readonly object syncRoot = new object();

        public FileApplicationLogger(string logDirectory)
        {
            if (string.IsNullOrWhiteSpace(logDirectory)) throw new ArgumentException("A log directory is required.", "logDirectory");
            this.logDirectory = Path.GetFullPath(logDirectory);
        }

        public void Log(LogEvent logEvent, Exception exception = null)
        {
            if (logEvent == null) throw new ArgumentNullException("logEvent");
            Directory.CreateDirectory(logDirectory);
            var line = new StringBuilder();
            line.Append('{').Append("\"timestampUtc\":\"").Append(logEvent.TimestampUtc.ToString("O")).Append("\",");
            line.Append("\"level\":\"").Append(Escape(logEvent.Level.ToString())).Append("\",");
            line.Append("\"event\":\"").Append(Escape(logEvent.EventName)).Append("\",");
            line.Append("\"message\":\"").Append(Escape(logEvent.Message)).Append('"');
            if (exception != null) line.Append(",\"exceptionType\":\"").Append(Escape(exception.GetType().FullName)).Append('"');
            foreach (var property in logEvent.Properties) line.Append(",\"").Append(Escape(property.Key)).Append("\":\"").Append(Escape(property.Value)).Append('"');
            line.Append('}');
            lock (syncRoot) File.AppendAllText(Path.Combine(logDirectory, DateTime.UtcNow.ToString("yyyyMMdd") + ".log"), line + Environment.NewLine, Encoding.UTF8);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}