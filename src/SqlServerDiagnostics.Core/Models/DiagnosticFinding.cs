using System.Collections.Generic;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DiagnosticFinding
    {
        public DiagnosticFinding() { Data = new Dictionary<string, object>(); }
        public string Id { get; set; }
        public string DiagnosticId { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
        public string SuggestedSql { get; set; }
        public FindingImpact Impact { get; set; }
        public InterpretationConfidence Confidence { get; set; }
        public decimal ScoreContribution { get; set; }
        public IDictionary<string, object> Data { get; private set; }
    }
}