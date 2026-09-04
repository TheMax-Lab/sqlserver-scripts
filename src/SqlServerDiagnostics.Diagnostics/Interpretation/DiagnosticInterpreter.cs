using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Interpretation
{
    public sealed class DiagnosticInterpreter : IDiagnosticInterpreter
    {
        public DiagnosticInterpretation Interpret(DiagnosticDefinition definition, DiagnosticResult result)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (result == null) throw new ArgumentNullException("result");
            var interpretation = new DiagnosticInterpretation
            {
                DiagnosticId = definition.Id, DiagnosticName = definition.Name, Category = definition.Category,
                Mode = definition.ResultInterpretation.Mode, Confidence = definition.ResultInterpretation.Confidence,
                ScoreEligible = definition.ScorePolicy.ScoreEligible
            };
            if (result.Status == DiagnosticExecutionStatus.Failed) return Complete(interpretation, DiagnosticInterpretationStatus.Failed, "Execution failed; this is not a database-health finding.");
            if (result.Status == DiagnosticExecutionStatus.Skipped) return Complete(interpretation, DiagnosticInterpretationStatus.Skipped, "Execution was skipped; coverage is incomplete.");
            if (result.Status == DiagnosticExecutionStatus.Cancelled) return Complete(interpretation, DiagnosticInterpretationStatus.Cancelled, "Execution was cancelled; coverage is incomplete.");
            if (result.Status != DiagnosticExecutionStatus.Succeeded) return Complete(interpretation, DiagnosticInterpretationStatus.Unknown, "No successful result is available for interpretation.");

            switch (definition.ResultInterpretation.Mode)
            {
                case InterpretationMode.FindingContract:
                case InterpretationMode.Custom:
                    AddContractFindings(definition, result, interpretation);
                    return CompleteFromFindings(definition, interpretation);
                case InterpretationMode.Inventory:
                    return Complete(interpretation, result.ResultSets.Sum(x => x.Rows.Count) == 0 ? MapEmpty(definition.ResultInterpretation.EmptyResultMeaning) : DiagnosticInterpretationStatus.Informational, "Inventory output is presented without a health penalty.");
                case InterpretationMode.Threshold:
                    InterpretThreshold(definition, result, interpretation);
                    return interpretation.Status == DiagnosticInterpretationStatus.Unknown ? interpretation : CompleteFromFindings(definition, interpretation);
                default:
                    interpretation.ScoreEligible = false;
                    return Complete(interpretation, DiagnosticInterpretationStatus.Unknown, "No trustworthy semantic interpretation is configured.");
            }
        }

        private static void AddContractFindings(DiagnosticDefinition definition, DiagnosticResult result, DiagnosticInterpretation interpretation)
        {
            int index = 0;
            foreach (DiagnosticFinding finding in result.Findings)
            {
                finding.Id = string.IsNullOrWhiteSpace(finding.Id) ? definition.Id + ":" + (++index).ToString(CultureInfo.InvariantCulture) : finding.Id;
                finding.DiagnosticId = definition.Id;
                finding.Impact = definition.ResultInterpretation.Impact;
                finding.Confidence = definition.ResultInterpretation.Confidence;
                interpretation.Findings.Add(finding);
            }
        }

        private static void InterpretThreshold(DiagnosticDefinition definition, DiagnosticResult result, DiagnosticInterpretation interpretation)
        {
            ResultInterpretationPolicy policy = definition.ResultInterpretation;
            if (string.IsNullOrWhiteSpace(policy.Metric) || !policy.WarningThreshold.HasValue || !policy.CriticalThreshold.HasValue)
            { interpretation.Status = DiagnosticInterpretationStatus.Unknown; interpretation.ScoreEligible = false; interpretation.Explanation = "Threshold metadata is incomplete."; return; }
            int index = 0;
            foreach (var row in result.ResultSets.SelectMany(x => x.Rows))
            {
                object raw; decimal value;
                if (!row.TryGetValue(policy.Metric, out raw) || raw == null || !decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out value)) continue;
                DiagnosticSeverity severity = EvaluateThreshold(value, policy);
                if (severity == DiagnosticSeverity.Passed) continue;
                var finding = new DiagnosticFinding { Id = definition.Id + ":threshold:" + (++index), DiagnosticId = definition.Id, Severity = severity, Impact = policy.Impact, Confidence = policy.Confidence, Title = policy.Metric + " crossed its configured threshold", Description = policy.Metric + "=" + value.ToString(CultureInfo.InvariantCulture), Recommendation = definition.Description };
                foreach (var item in row) finding.Data[item.Key] = item.Value;
                interpretation.Findings.Add(finding);
            }
            if (index == 0 && result.ResultSets.SelectMany(x => x.Rows).Any() && !result.ResultSets.SelectMany(x => x.Rows).Any(x => x.ContainsKey(policy.Metric)))
            { interpretation.Status = DiagnosticInterpretationStatus.Unknown; interpretation.ScoreEligible = false; interpretation.Explanation = "The configured threshold metric was not returned."; }
        }

        private static DiagnosticSeverity EvaluateThreshold(decimal value, ResultInterpretationPolicy policy)
        {
            if (policy.HigherIsWorse)
            { if (value >= policy.CriticalThreshold.Value) return DiagnosticSeverity.Critical; if (value >= policy.WarningThreshold.Value) return DiagnosticSeverity.Warning; }
            else
            { if (value <= policy.CriticalThreshold.Value) return DiagnosticSeverity.Critical; if (value <= policy.WarningThreshold.Value) return DiagnosticSeverity.Warning; }
            return DiagnosticSeverity.Passed;
        }

        private static DiagnosticInterpretation CompleteFromFindings(DiagnosticDefinition definition, DiagnosticInterpretation interpretation)
        {
            if (interpretation.Status == DiagnosticInterpretationStatus.Unknown && !string.IsNullOrWhiteSpace(interpretation.Explanation)) return interpretation;
            if (interpretation.Findings.Any(x => x.Severity == DiagnosticSeverity.Critical || x.Severity == DiagnosticSeverity.Warning)) return Complete(interpretation, DiagnosticInterpretationStatus.Findings, "The diagnostic returned one or more adverse findings.");
            if (interpretation.Findings.Count > 0) return Complete(interpretation, DiagnosticInterpretationStatus.Informational, "The diagnostic returned informational observations only.");
            DiagnosticInterpretationStatus emptyStatus = MapEmpty(definition.ResultInterpretation.EmptyResultMeaning);
            if (emptyStatus == DiagnosticInterpretationStatus.Healthy)
            {
                interpretation.Findings.Add(new DiagnosticFinding { Id = definition.Id + ":healthy", DiagnosticId = definition.Id, Severity = DiagnosticSeverity.Passed, Impact = FindingImpact.None, Confidence = definition.ResultInterpretation.Confidence, Title = "No adverse findings detected", Description = "The diagnostic returned no findings and its manifest explicitly defines an empty result as healthy." });
            }
            return Complete(interpretation, emptyStatus, "The diagnostic returned no findings; the configured empty-result meaning was applied.");
        }

        private static DiagnosticInterpretationStatus MapEmpty(EmptyResultMeaning meaning)
        {
            if (meaning == EmptyResultMeaning.Healthy) return DiagnosticInterpretationStatus.Healthy;
            if (meaning == EmptyResultMeaning.Informational) return DiagnosticInterpretationStatus.Informational;
            if (meaning == EmptyResultMeaning.NotApplicable) return DiagnosticInterpretationStatus.NotApplicable;
            return DiagnosticInterpretationStatus.Unknown;
        }

        private static DiagnosticInterpretation Complete(DiagnosticInterpretation value, DiagnosticInterpretationStatus status, string explanation) { value.Status = status; value.Explanation = explanation; return value; }
    }
}