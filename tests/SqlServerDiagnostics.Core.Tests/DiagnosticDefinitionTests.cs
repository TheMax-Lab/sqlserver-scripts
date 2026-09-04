using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Tests
{
    [TestClass]
    public sealed class DiagnosticDefinitionTests
    {
        [TestMethod]
        public void Validate_AllowsReadOnlyLowCostHealthCheckDiagnostic()
        {
            DiagnosticDefinition definition = CreateValidDefinition();
            definition.HealthCheckEnabled = true;
            definition.ReadOnly = true;
            definition.ExecutionCost = DiagnosticExecutionCost.Low;

            definition.Validate();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Validate_RejectsNonReadOnlyHealthCheckDiagnostic()
        {
            DiagnosticDefinition definition = CreateValidDefinition();
            definition.HealthCheckEnabled = true;
            definition.ReadOnly = false;
            definition.Validate();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Validate_RejectsHighCostAutomaticDiagnostic()
        {
            DiagnosticDefinition definition = CreateValidDefinition();
            definition.HealthCheckEnabled = true;
            definition.ReadOnly = true;
            definition.ExecutionCost = DiagnosticExecutionCost.High;
            definition.Validate();
        }

        private static DiagnosticDefinition CreateValidDefinition()
        {
            return new DiagnosticDefinition { Id = "sample", Name = "Sample", ScriptPath = "Performance/sample.sql" };
        }
    }
}