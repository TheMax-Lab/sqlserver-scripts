using System.Linq;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Presentation
{
    public sealed class HealthCheckSummary
    {
        public int Selected { get; private set; }
        public int Completed { get; private set; }
        public int Critical { get; private set; }
        public int Warnings { get; private set; }
        public int Information { get; private set; }
        public int Skipped { get; private set; }
        public int Failed { get; private set; }
        public int Cancelled { get; private set; }

        public static HealthCheckSummary FromReport(HealthCheckReport report, int selected)
        {
            var summary = new HealthCheckSummary { Selected = selected };
            if (report == null) return summary;
            summary.Completed = report.Results.Count;
            summary.Critical = report.Results.SelectMany(item => item.Findings).Count(item => item.Severity == DiagnosticSeverity.Critical);
            summary.Warnings = report.Results.SelectMany(item => item.Findings).Count(item => item.Severity == DiagnosticSeverity.Warning);
            summary.Information = report.Results.SelectMany(item => item.Findings).Count(item => item.Severity == DiagnosticSeverity.Information);
            summary.Skipped = report.SkippedCount;
            summary.Failed = report.FailedCount;
            summary.Cancelled = report.CancelledCount;
            return summary;
        }
    }
}