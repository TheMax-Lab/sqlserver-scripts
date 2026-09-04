using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public static class HealthAssessmentPolicy
    {
        public const decimal CompleteCoverage = 100m;
        public const decimal PartialCoverageMinimum = 70m;

        public static AssessmentStatus GetStatus(HealthCoverage coverage)
        {
            decimal percentage = coverage == null ? 0m : coverage.CoveragePercentage;
            if (percentage >= CompleteCoverage) return AssessmentStatus.Complete;
            if (percentage >= PartialCoverageMinimum) return AssessmentStatus.PartiallyComplete;
            if (percentage > 0m) return AssessmentStatus.Inconclusive;
            return AssessmentStatus.Failed;
        }

        public static string GetMessage(AssessmentStatus status, HealthCoverage coverage)
        {
            int unavailable = coverage == null ? 0 : coverage.FailedDiagnostics + coverage.SkippedDiagnostics;
            switch (status)
            {
                case AssessmentStatus.Complete: return "Assessment complete. All eligible diagnostics were evaluated successfully.";
                case AssessmentStatus.PartiallyComplete: return "Assessment partially complete. " + unavailable + " diagnostic(s) could not be evaluated.";
                case AssessmentStatus.Inconclusive: return "Assessment inconclusive. Diagnostic coverage is below the minimum reliable threshold.";
                default: return "Assessment failed. No eligible diagnostics were evaluated successfully.";
            }
        }
    }
}