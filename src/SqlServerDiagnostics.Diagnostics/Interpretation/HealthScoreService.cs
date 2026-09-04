using System;
using System.Collections.Generic;
using System.Linq;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Interpretation
{
    public sealed class HealthScoreService : IHealthScoreService
    {
        public HealthReport BuildReport(HealthCheckReport source, IReadOnlyList<DiagnosticDefinition> definitions, IReadOnlyList<DiagnosticInterpretation> interpretations)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (definitions == null) throw new ArgumentNullException("definitions");
            if (interpretations == null) throw new ArgumentNullException("interpretations");
            var report = new HealthReport { GeneratedAt = source.CompletedAtUtc ?? DateTimeOffset.UtcNow, Server = source.Server, Database = source.Database, SqlServerVersion = source.Server == null ? string.Empty : source.Server.ProductVersion, ApplicationVersion = ApplicationInfo.ApplicationVersion, DiagnosticsTotal = definitions.Count };
            foreach (var result in source.Results) report.Results.Add(result);
            foreach (var interpretation in interpretations) report.Interpretations.Add(interpretation);
            foreach (var finding in interpretations.SelectMany(x => x.Findings)) report.Findings.Add(finding);
            report.Coverage = CalculateCoverage(definitions, source.Results);
            report.HealthScore = CalculateScore(definitions, interpretations, report.Coverage, report.LogicalFindings);
            report.AssessmentStatus = HealthAssessmentPolicy.GetStatus(report.Coverage);
            report.AssessmentMessage = HealthAssessmentPolicy.GetMessage(report.AssessmentStatus, report.Coverage);
            return report;
        }

        private static HealthCoverage CalculateCoverage(IEnumerable<DiagnosticDefinition> definitions, IEnumerable<DiagnosticResult> results)
        {
            var eligibleIds = new HashSet<string>(definitions.Where(IsAutomaticCandidate).Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
            var relevant = results.Where(x => eligibleIds.Contains(x.DiagnosticId)).ToList();
            int successful = relevant.Count(x => x.Status == DiagnosticExecutionStatus.Succeeded);
            int eligible = eligibleIds.Count;
            return new HealthCoverage { EligibleDiagnostics = eligible, ExecutedDiagnostics = relevant.Count, SuccessfulDiagnostics = successful, SkippedDiagnostics = relevant.Count(x => x.Status == DiagnosticExecutionStatus.Skipped), FailedDiagnostics = relevant.Count(x => x.Status == DiagnosticExecutionStatus.Failed || x.Status == DiagnosticExecutionStatus.Cancelled), CoveragePercentage = eligible == 0 ? 0 : Math.Round(successful * 100m / eligible, 2) };
        }

        private static HealthScore CalculateScore(IReadOnlyList<DiagnosticDefinition> definitions, IReadOnlyList<DiagnosticInterpretation> interpretations, HealthCoverage coverage, IList<LogicalHealthFinding> logicalFindings)
        {
            var definitionMap = definitions.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            var successful = interpretations.Where(x => x.ScoreEligible && (x.Status == DiagnosticInterpretationStatus.Healthy || x.Status == DiagnosticInterpretationStatus.Findings || x.Status == DiagnosticInterpretationStatus.Informational)).Where(x => definitionMap.ContainsKey(x.DiagnosticId)).ToList();
            var groups = successful.GroupBy(x => GroupKey(definitionMap[x.DiagnosticId])).ToList();
            decimal maximum = 0, penalty = 0;
            var score = new HealthScore { DiagnosticsEvaluated = successful.Count, DiagnosticsSkipped = coverage.SkippedDiagnostics, DiagnosticsFailed = coverage.FailedDiagnostics };
            foreach (var group in groups.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var members = group.ToList();
                decimal weight = members.Max(x => definitionMap[x.DiagnosticId].ScorePolicy.Weight);
                maximum += weight;
                var primaryPair = members.SelectMany(x => x.Findings.Select(f => new { Interpretation = x, Finding = f })).OrderByDescending(x => x.Finding.Severity).ThenByDescending(x => x.Finding.Impact).FirstOrDefault();
                DiagnosticSeverity severity = primaryPair == null ? DiagnosticSeverity.Passed : primaryPair.Finding.Severity;
                DiagnosticDefinition policyDefinition = primaryPair == null ? definitionMap[members[0].DiagnosticId] : definitionMap[primaryPair.Interpretation.DiagnosticId];
                decimal groupPenalty = weight * Fraction(policyDefinition.ScorePolicy, severity);
                penalty += groupPenalty;
                score.Breakdown.Add(new HealthScoreBreakdown { DiagnosticId = string.Join(", ", members.Select(x => x.DiagnosticId)), DiagnosticName = string.Join(" / ", members.Select(x => x.DiagnosticName)), DeduplicationGroup = group.Key, Severity = severity, Weight = weight, Penalty = groupPenalty, Included = true, Explanation = groupPenalty == 0 ? "No adverse score contribution." : severity + " finding applies " + Fraction(policyDefinition.ScorePolicy, severity).ToString("P0") + " of this logical group weight." });
                if (primaryPair != null)
                {
                    primaryPair.Finding.ScoreContribution = -groupPenalty;
                    var logical = new LogicalHealthFinding { Group = group.Key, PrimaryFinding = primaryPair.Finding, Severity = severity, Impact = primaryPair.Finding.Impact, ScoreContribution = -groupPenalty };
                    foreach (var pair in members.SelectMany(x => x.Findings).Where(x => !object.ReferenceEquals(x, primaryPair.Finding))) logical.SupportingFindings.Add(pair);
                    logicalFindings.Add(logical);
                }
            }
            foreach (var excluded in interpretations.Where(x => !successful.Contains(x))) score.Breakdown.Add(new HealthScoreBreakdown { DiagnosticId = excluded.DiagnosticId, DiagnosticName = excluded.DiagnosticName, Included = false, Explanation = excluded.ScoreEligible ? "Excluded: execution/interpretation state is " + excluded.Status + "; coverage reflects the missing evidence." : "Excluded: interpretation metadata marks this diagnostic as score-ineligible." });
            score.LogicalGroupsEvaluated = groups.Count;
            score.MaxScore = maximum; score.Score = Math.Max(0, maximum - penalty); score.Percentage = maximum == 0 ? 0 : Math.Round(score.Score * 100m / maximum, 2); score.Grade = Grade(score.Percentage); score.Confidence = Confidence(coverage, successful);
            score.CriticalFindings = interpretations.SelectMany(x => x.Findings).Count(x => x.Severity == DiagnosticSeverity.Critical);
            score.WarningFindings = interpretations.SelectMany(x => x.Findings).Count(x => x.Severity == DiagnosticSeverity.Warning);
            score.InformationFindings = interpretations.SelectMany(x => x.Findings).Count(x => x.Severity == DiagnosticSeverity.Information);
            score.Explanations.Add("Equal-weight logical diagnostic groups; Critical consumes 100%, Warning 50%, Information 0% of a group unit.");
            score.Explanations.Add("Skipped and failed diagnostics affect coverage and confidence, not health points.");
            score.Validate(); return score;
        }

        private static bool IsAutomaticCandidate(DiagnosticDefinition d) { return d.HealthCheckEnabled && d.ReadOnly && d.ExecutionCost != DiagnosticExecutionCost.High; }
        private static string GroupKey(DiagnosticDefinition d) { return string.IsNullOrWhiteSpace(d.DeduplicationGroup) ? d.Id : d.DeduplicationGroup; }
        private static decimal Fraction(DiagnosticScorePolicy policy, DiagnosticSeverity severity) { if (severity == DiagnosticSeverity.Critical) return policy.CriticalPenaltyFraction; if (severity == DiagnosticSeverity.Warning) return policy.WarningPenaltyFraction; if (severity == DiagnosticSeverity.Information) return policy.InformationPenaltyFraction; return 0; }
        private static string Grade(decimal percentage) { if (percentage < 40) return "Critical"; if (percentage < 60) return "Poor"; if (percentage < 75) return "Fair"; if (percentage < 90) return "Good"; return "Excellent"; }
        private static InterpretationConfidence Confidence(HealthCoverage coverage, IEnumerable<DiagnosticInterpretation> evaluated)
        {
            if (coverage.CoveragePercentage <= 0) return InterpretationConfidence.Unknown;
            InterpretationConfidence semantic = evaluated.Any() ? evaluated.Min(x => x.Confidence) : InterpretationConfidence.Unknown;
            InterpretationConfidence coverageConfidence = coverage.CoveragePercentage >= 90 ? InterpretationConfidence.High : coverage.CoveragePercentage >= 70 ? InterpretationConfidence.Medium : InterpretationConfidence.Low;
            return semantic < coverageConfidence ? semantic : coverageConfidence;
        }
    }
}