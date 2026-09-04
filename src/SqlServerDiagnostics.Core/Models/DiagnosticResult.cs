using System;
using System.Collections.Generic;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DiagnosticResult
    {
        public DiagnosticResult()
        {
            Findings = new List<DiagnosticFinding>();
            ResultSets = new List<DiagnosticResultSet>();
            Status = DiagnosticExecutionStatus.NotStarted;
        }

        public string DiagnosticId { get; set; }
        public string DiagnosticName { get; set; }
        public DiagnosticCategory Category { get; set; }
        public DiagnosticScope ExecutionScope { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DiagnosticExecutionStatus Status { get; set; }
        public DiagnosticFailureKind FailureKind { get; set; }
        public string UserMessage { get; set; }
        public string ErrorMessage { get; set; }
        public int? SqlErrorNumber { get; set; }
        public string RequiredPermission { get; set; }
        public IList<string> RequiredPermissions { get; private set; } = new List<string>();
        public IList<DiagnosticFinding> Findings { get; private set; }
        public IList<DiagnosticResultSet> ResultSets { get; private set; }
        public bool Success { get { return Status == DiagnosticExecutionStatus.Succeeded; } }
    }
}