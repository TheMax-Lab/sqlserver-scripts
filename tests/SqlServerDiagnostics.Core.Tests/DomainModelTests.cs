using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Tests
{
    [TestClass]
    public sealed class DomainModelTests
    {
        [TestMethod]
        public void DiagnosticEnums_HaveStableOrderedValues()
        {
            Assert.AreEqual(0, (int)DiagnosticSeverity.Passed);
            Assert.AreEqual(3, (int)DiagnosticSeverity.Critical);
            Assert.AreEqual(2, (int)DiagnosticExecutionCost.High);
            Assert.AreEqual(1, (int)DiagnosticScope.Instance);
            Assert.AreEqual(2, (int)DiagnosticExecutionStatus.Succeeded);
        }

        [TestMethod]
        public void ResultSet_PreservesColumnsRowsAndRowCount()
        {
            var resultSet = new DiagnosticResultSet();
            resultSet.Columns.Add(new DiagnosticColumn { Name = "Priority", DataType = typeof(string), Ordinal = 0 });
            resultSet.Rows.Add(new Dictionary<string, object> { { "Priority", "High" } });

            Assert.AreEqual(1, resultSet.Columns.Count);
            Assert.AreEqual(1, resultSet.RowCount);
            Assert.AreEqual("High", resultSet.Rows[0]["Priority"]);
        }

        [TestMethod]
        public void HealthCheckReport_CountsExecutionOutcomes()
        {
            var report = new HealthCheckReport();
            report.Results.Add(new DiagnosticResult { Status = DiagnosticExecutionStatus.Succeeded });
            report.Results.Add(new DiagnosticResult { Status = DiagnosticExecutionStatus.Skipped });
            report.Results.Add(new DiagnosticResult { Status = DiagnosticExecutionStatus.Failed });
            report.Results.Add(new DiagnosticResult { Status = DiagnosticExecutionStatus.Cancelled });

            Assert.AreEqual(1, report.ExecutedCount);
            Assert.AreEqual(1, report.SkippedCount);
            Assert.AreEqual(1, report.FailedCount);
            Assert.AreEqual(1, report.CancelledCount);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void HealthScore_RejectsValueOutsideRange()
        {
            new HealthScore { Overall = 101 }.Validate();
        }

        [TestMethod]
        public void SqlServerVersion_UsesNumericMajorVersionComparison()
        {
            Assert.IsTrue(SqlServerVersion.Parse("2022").CompareTo(SqlServerVersion.Parse("2019")) > 0);
            Assert.IsTrue(SqlServerVersion.Parse("16.0.1000.6").CompareTo(SqlServerVersion.Parse("2022")) > 0);
        }

        [TestMethod]
        public void ExecutionContext_SkipsUnsupportedVersionAndUnavailableQueryStore()
        {
            var context = new DiagnosticExecutionContext { Server = new SqlServerInfo { MajorVersion = 13, ProductVersion = "13.0.1.0", EngineType = SqlServerEngineType.SqlServer }, Database = new DatabaseInfo { IsAccessible = true, IsQueryStoreEnabled = false } };
            var definition = new DiagnosticDefinition { Id = "sample", Name = "Sample", ScriptPath = "Performance/sample.sql", MinimumSqlServerVersion = "2019", ExecutionScope = DiagnosticScope.Database };
            DiagnosticFailureKind kind; string reason;
            Assert.IsFalse(context.Supports(definition, out kind, out reason));
            Assert.AreEqual(DiagnosticFailureKind.UnsupportedVersion, kind);
            definition.MinimumSqlServerVersion = "2016"; definition.RequiresQueryStore = true;
            Assert.IsFalse(context.Supports(definition, out kind, out reason));
            Assert.AreEqual(DiagnosticFailureKind.QueryStoreUnavailable, kind);
        }
    }
}