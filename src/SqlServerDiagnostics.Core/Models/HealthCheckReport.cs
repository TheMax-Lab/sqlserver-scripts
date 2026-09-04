using System;
using System.Collections.Generic;
using System.Linq;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class HealthCheckReport
    {
        public HealthCheckReport()
        {
            Results = new List<DiagnosticResult>();
            StartedAtUtc = DateTimeOffset.UtcNow;
        }

        public SqlServerInfo Server { get; set; }
        public DatabaseInfo Database { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public HealthScore Score { get; set; }
        public IList<DiagnosticResult> Results { get; private set; }
        public int ExecutedCount { get { return Results.Count(r => r.Status == DiagnosticExecutionStatus.Succeeded); } }
        public int SkippedCount { get { return Results.Count(r => r.Status == DiagnosticExecutionStatus.Skipped); } }
        public int FailedCount { get { return Results.Count(r => r.Status == DiagnosticExecutionStatus.Failed); } }
        public int CancelledCount { get { return Results.Count(r => r.Status == DiagnosticExecutionStatus.Cancelled); } }
        public TimeSpan Duration { get { return (CompletedAtUtc ?? DateTimeOffset.UtcNow) - StartedAtUtc; } }
    }
}