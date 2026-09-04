using System;
using System.Collections.Generic;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class HealthScore
    {
        public HealthScore()
        {
            CategoryScores = new Dictionary<DiagnosticCategory, int>();
            Explanations = new List<string>();
            Breakdown = new List<HealthScoreBreakdown>();
        }

        public int Overall { get { return (int)Math.Round(Percentage, MidpointRounding.AwayFromZero); } set { Percentage = value; } }
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
        public string Grade { get; set; }
        public InterpretationConfidence Confidence { get; set; }
        public int DiagnosticsEvaluated { get; set; }
        public int LogicalGroupsEvaluated { get; set; }
        public int DiagnosticsSkipped { get; set; }
        public int DiagnosticsFailed { get; set; }
        public int CriticalFindings { get; set; }
        public int WarningFindings { get; set; }
        public int InformationFindings { get; set; }
        public IList<HealthScoreBreakdown> Breakdown { get; private set; }
        public IDictionary<DiagnosticCategory, int> CategoryScores { get; private set; }
        public IList<string> Explanations { get; private set; }

        public void Validate()
        {
            if (Percentage < 0 || Percentage > 100) throw new InvalidOperationException("The health score must be between 0 and 100.");
        }
    }
}