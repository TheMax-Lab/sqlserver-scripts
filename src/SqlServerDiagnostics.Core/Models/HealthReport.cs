using System;
using System.Collections.Generic;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class HealthReport
    {
        public HealthReport()
        {
            Interpretations = new List<DiagnosticInterpretation>();
            LogicalFindings = new List<LogicalHealthFinding>();
            Findings = new List<DiagnosticFinding>();
            Results = new List<DiagnosticResult>();
        }
        public DateTimeOffset GeneratedAt { get; set; }
        public SqlServerInfo Server { get; set; }
        public DatabaseInfo Database { get; set; }
        public string SqlServerVersion { get; set; }
        public string ApplicationVersion { get; set; }
        public AssessmentStatus AssessmentStatus { get; set; }
        public string AssessmentMessage { get; set; }
        public int DiagnosticsTotal { get; set; }
        public HealthScore HealthScore { get; set; }
        public HealthCoverage Coverage { get; set; }
        public IList<DiagnosticInterpretation> Interpretations { get; private set; }
        public IList<LogicalHealthFinding> LogicalFindings { get; private set; }
        public IList<DiagnosticFinding> Findings { get; private set; }
        public IList<DiagnosticResult> Results { get; private set; }
    }
}