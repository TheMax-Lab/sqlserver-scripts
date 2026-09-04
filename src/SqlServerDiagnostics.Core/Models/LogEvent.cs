using System;
using System.Collections.Generic;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class LogEvent
    {
        public LogEvent() { TimestampUtc = DateTimeOffset.UtcNow; Properties = new Dictionary<string, string>(); }
        public DateTimeOffset TimestampUtc { get; set; }
        public LogLevel Level { get; set; }
        public string EventName { get; set; }
        public string Message { get; set; }
        public IDictionary<string, string> Properties { get; private set; }
    }
}