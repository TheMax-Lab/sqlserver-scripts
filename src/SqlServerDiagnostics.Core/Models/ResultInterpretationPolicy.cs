using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class ResultInterpretationPolicy
    {
        public InterpretationMode Mode { get; set; }
        public EmptyResultMeaning EmptyResultMeaning { get; set; }
        public FindingImpact Impact { get; set; }
        public InterpretationConfidence Confidence { get; set; }
        public string Metric { get; set; }
        public decimal? WarningThreshold { get; set; }
        public decimal? CriticalThreshold { get; set; }
        public bool HigherIsWorse { get; set; } = true;
    }
}