using System.Collections.Generic;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DiagnosticInterpretation
    {
        public DiagnosticInterpretation() { Findings = new List<DiagnosticFinding>(); }
        public string DiagnosticId { get; set; }
        public string DiagnosticName { get; set; }
        public DiagnosticCategory Category { get; set; }
        public DiagnosticInterpretationStatus Status { get; set; }
        public InterpretationMode Mode { get; set; }
        public InterpretationConfidence Confidence { get; set; }
        public bool ScoreEligible { get; set; }
        public string Explanation { get; set; }
        public IList<DiagnosticFinding> Findings { get; private set; }
    }
}