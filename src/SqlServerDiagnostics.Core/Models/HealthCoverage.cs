namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class HealthCoverage
    {
        public int EligibleDiagnostics { get; set; }
        public int ExecutedDiagnostics { get; set; }
        public int SkippedDiagnostics { get; set; }
        public int FailedDiagnostics { get; set; }
        public int SuccessfulDiagnostics { get; set; }
        public decimal CoveragePercentage { get; set; }
    }
}