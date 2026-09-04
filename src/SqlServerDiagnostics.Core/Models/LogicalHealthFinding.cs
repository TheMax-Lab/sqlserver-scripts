using System.Collections.Generic;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class LogicalHealthFinding
    {
        public LogicalHealthFinding() { SupportingFindings = new List<DiagnosticFinding>(); }
        public string Group { get; set; }
        public DiagnosticFinding PrimaryFinding { get; set; }
        public IList<DiagnosticFinding> SupportingFindings { get; private set; }
        public DiagnosticSeverity Severity { get; set; }
        public FindingImpact Impact { get; set; }
        public decimal ScoreContribution { get; set; }
    }
}