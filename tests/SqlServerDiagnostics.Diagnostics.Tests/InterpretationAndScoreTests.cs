using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Interpretation;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Tests
{
    [TestClass]
    public sealed class InterpretationAndScoreTests
    {
        [TestMethod]
        public void FindingContract_UsesAuthoredFindingAndHealthyEmptySemantics()
        {
            DiagnosticDefinition definition = Definition("one", true, "group-one");
            var result = Success("one"); result.Findings.Add(new DiagnosticFinding { Severity = DiagnosticSeverity.Warning, Title = "Evidence-backed warning", SuggestedSql = "ALTER TABLE dbo.T ADD C int" });
            DiagnosticInterpretation interpreted = new DiagnosticInterpreter().Interpret(definition, result);
            Assert.AreEqual(DiagnosticInterpretationStatus.Findings, interpreted.Status); Assert.AreEqual(FindingImpact.High, interpreted.Findings[0].Impact); Assert.AreEqual("ALTER TABLE dbo.T ADD C int", interpreted.Findings[0].SuggestedSql);
            interpreted = new DiagnosticInterpreter().Interpret(definition, Success("one")); Assert.AreEqual(DiagnosticInterpretationStatus.Healthy, interpreted.Status); Assert.AreEqual(DiagnosticSeverity.Passed, interpreted.Findings[0].Severity);
        }

        [TestMethod]
        public void InventoryAndUnknown_DoNotInventHealthFindings()
        {
            DiagnosticDefinition inventory = Definition("inventory", false, "inventory"); inventory.ResultInterpretation.Mode = InterpretationMode.Inventory; inventory.ResultInterpretation.EmptyResultMeaning = EmptyResultMeaning.Informational;
            DiagnosticInterpretation value = new DiagnosticInterpreter().Interpret(inventory, Success("inventory")); Assert.AreEqual(DiagnosticInterpretationStatus.Informational, value.Status); Assert.AreEqual(0, value.Findings.Count);
            inventory.ResultInterpretation.Mode = InterpretationMode.Unknown; value = new DiagnosticInterpreter().Interpret(inventory, Success("inventory")); Assert.AreEqual(DiagnosticInterpretationStatus.Unknown, value.Status); Assert.IsFalse(value.ScoreEligible);
        }

        [TestMethod]
        public void Threshold_UsesOnlyExplicitMetricAndThresholds()
        {
            DiagnosticDefinition definition = Definition("threshold", true, "threshold"); definition.ResultInterpretation.Mode = InterpretationMode.Threshold; definition.ResultInterpretation.Metric = "Percent"; definition.ResultInterpretation.WarningThreshold = 70; definition.ResultInterpretation.CriticalThreshold = 90;
            var result = Success("threshold"); var set = new DiagnosticResultSet(); set.Rows.Add(new Dictionary<string, object> { { "Percent", 95m } }); result.ResultSets.Add(set);
            Assert.AreEqual(DiagnosticSeverity.Critical, new DiagnosticInterpreter().Interpret(definition, result).Findings[0].Severity);
        }

        [TestMethod]
        public void CustomMode_PreservesContractWhileInformationHasNoPenalty()
        {
            DiagnosticDefinition definition = Definition("custom", true, "custom"); definition.ResultInterpretation.Mode = InterpretationMode.Custom;
            var result = Success("custom"); result.Findings.Add(new DiagnosticFinding { Severity = DiagnosticSeverity.Information, Title = "Inventory observation" }); result.Findings.Add(new DiagnosticFinding { Severity = DiagnosticSeverity.Warning, Title = "Explicit warning" });
            DiagnosticInterpretation interpretation = new DiagnosticInterpreter().Interpret(definition, result);
            var check = new HealthCheckReport(); check.Results.Add(result);
            HealthReport report = new HealthScoreService().BuildReport(check, new[] { definition }, new[] { interpretation });
            Assert.AreEqual(InterpretationMode.Custom, interpretation.Mode); Assert.AreEqual(2, report.Findings.Count); Assert.AreEqual(50m, report.HealthScore.Percentage); Assert.AreEqual(-0.5m, interpretation.Findings[1].ScoreContribution);
        }

        [TestMethod]
        public void Score_IsDeterministicExplainableAndDeduplicated()
        {
            DiagnosticDefinition a = Definition("a", true, "same"), b = Definition("b", true, "same"), c = Definition("c", true, "other");
            DiagnosticInterpretation ia = Interpretation(a, DiagnosticSeverity.Warning), ib = Interpretation(b, DiagnosticSeverity.Warning), ic = Interpretation(c, DiagnosticSeverity.Critical);
            var execution = new HealthCheckReport { Server = new SqlServerInfo { ProductVersion = "15.0" }, Database = new DatabaseInfo { Name = "db" }, CompletedAtUtc = DateTimeOffset.UtcNow };
            execution.Results.Add(Success("a")); execution.Results.Add(Success("b")); execution.Results.Add(Success("c"));
            HealthReport first = new HealthScoreService().BuildReport(execution, new[] { a, b, c }, new[] { ia, ib, ic }); HealthReport second = new HealthScoreService().BuildReport(execution, new[] { a, b, c }, new[] { ia, ib, ic });
            Assert.AreEqual(25m, first.HealthScore.Percentage); Assert.AreEqual(first.HealthScore.Percentage, second.HealthScore.Percentage); Assert.AreEqual(2, first.HealthScore.Breakdown.Count); Assert.AreEqual(100m, first.Coverage.CoveragePercentage);
        }

        [TestMethod]
        public void FailedAndSkipped_ReduceCoverageButNotScore()
        {
            DiagnosticDefinition a = Definition("a", true, "a"), b = Definition("b", true, "b"), c = Definition("c", true, "c"); var check = new HealthCheckReport();
            check.Results.Add(Success("a")); check.Results.Add(new DiagnosticResult { DiagnosticId = "b", Status = DiagnosticExecutionStatus.Failed }); check.Results.Add(new DiagnosticResult { DiagnosticId = "c", Status = DiagnosticExecutionStatus.Skipped });
            HealthReport report = new HealthScoreService().BuildReport(check, new[] { a, b, c }, new[] { new DiagnosticInterpreter().Interpret(a, check.Results[0]), new DiagnosticInterpreter().Interpret(b, check.Results[1]), new DiagnosticInterpreter().Interpret(c, check.Results[2]) });
            Assert.AreEqual(100m, report.HealthScore.Percentage); Assert.AreEqual(33.33m, report.Coverage.CoveragePercentage); Assert.AreEqual(AssessmentStatus.Inconclusive, report.AssessmentStatus); Assert.AreEqual(1, report.HealthScore.DiagnosticsFailed); Assert.AreEqual(1, report.HealthScore.DiagnosticsSkipped);
        }

        [TestMethod]
        public void GradeBoundaries_AreAppliedByServiceNotUi()
        {
            Assert.AreEqual("Excellent", ScoreWithWarnings(0).HealthScore.Grade); Assert.AreEqual("Good", ScoreWithWarnings(1).HealthScore.Grade); Assert.AreEqual("Good", ScoreWithWarnings(2).HealthScore.Grade); Assert.AreEqual("Fair", ScoreWithWarnings(3).HealthScore.Grade); Assert.AreEqual("Poor", ScoreWithWarnings(4).HealthScore.Grade); Assert.AreEqual("Critical", ScoreWithCriticals(4).HealthScore.Grade);
        }

        private static HealthReport ScoreWithWarnings(int warnings)
        {
            var definitions = new List<DiagnosticDefinition>(); var interpretations = new List<DiagnosticInterpretation>(); var report = new HealthCheckReport();
            for (int i = 0; i < 4; i++) { var d = Definition("d" + i, true, "g" + i); definitions.Add(d); report.Results.Add(Success(d.Id)); interpretations.Add(i < warnings ? Interpretation(d, DiagnosticSeverity.Warning) : new DiagnosticInterpreter().Interpret(d, Success(d.Id))); }
            return new HealthScoreService().BuildReport(report, definitions, interpretations);
        }
        private static HealthReport ScoreWithCriticals(int criticals)
        {
            var definitions = new List<DiagnosticDefinition>(); var interpretations = new List<DiagnosticInterpretation>(); var report = new HealthCheckReport();
            for (int i = 0; i < 4; i++) { var d = Definition("c" + i, true, "cg" + i); definitions.Add(d); report.Results.Add(Success(d.Id)); interpretations.Add(i < criticals ? Interpretation(d, DiagnosticSeverity.Critical) : new DiagnosticInterpreter().Interpret(d, Success(d.Id))); }
            return new HealthScoreService().BuildReport(report, definitions, interpretations);
        }
        private static DiagnosticDefinition Definition(string id, bool scoreEligible, string group) { return new DiagnosticDefinition { Id = id, Name = id, Description = id, Category = DiagnosticCategory.Performance, ScriptPath = "Performance/" + id + ".sql", ReadOnly = true, HealthCheckEnabled = true, ExecutionCost = DiagnosticExecutionCost.Low, DeduplicationGroup = group, ResultInterpretation = new ResultInterpretationPolicy { Mode = InterpretationMode.FindingContract, EmptyResultMeaning = EmptyResultMeaning.Healthy, Impact = FindingImpact.High, Confidence = InterpretationConfidence.High }, ScorePolicy = new DiagnosticScorePolicy { ScoreEligible = scoreEligible } }; }
        private static DiagnosticResult Success(string id) { return new DiagnosticResult { DiagnosticId = id, DiagnosticName = id, Category = DiagnosticCategory.Performance, Status = DiagnosticExecutionStatus.Succeeded }; }
        private static DiagnosticInterpretation Interpretation(DiagnosticDefinition d, DiagnosticSeverity severity) { var r = Success(d.Id); r.Findings.Add(new DiagnosticFinding { Severity = severity, Title = severity.ToString() }); return new DiagnosticInterpreter().Interpret(d, r); }
    }
}