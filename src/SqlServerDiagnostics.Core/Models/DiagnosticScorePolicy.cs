using System;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DiagnosticScorePolicy
    {
        public bool ScoreEligible { get; set; }
        public decimal Weight { get; set; } = 1m;
        public decimal CriticalPenaltyFraction { get; set; } = 1m;
        public decimal WarningPenaltyFraction { get; set; } = 0.5m;
        public decimal InformationPenaltyFraction { get; set; }

        public void Validate()
        {
            if (ScoreEligible && Weight <= 0) throw new InvalidOperationException("A score-eligible diagnostic must have a positive weight.");
            ValidateFraction(CriticalPenaltyFraction, "critical"); ValidateFraction(WarningPenaltyFraction, "warning"); ValidateFraction(InformationPenaltyFraction, "information");
        }

        private static void ValidateFraction(decimal value, string name)
        {
            if (value < 0 || value > 1) throw new InvalidOperationException("The " + name + " penalty fraction must be between zero and one.");
        }
    }
}