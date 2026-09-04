using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class HealthScoreBreakdown
    {
        public string DiagnosticId { get; set; }
        public string DiagnosticName { get; set; }
        public string DeduplicationGroup { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        public decimal Weight { get; set; }
        public decimal Penalty { get; set; }
        public bool Included { get; set; }
        public string Explanation { get; set; }
    }
}