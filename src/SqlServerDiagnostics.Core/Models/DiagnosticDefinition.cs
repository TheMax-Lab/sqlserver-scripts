using System;
using System.Collections.Generic;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DiagnosticDefinition
    {
        public DiagnosticDefinition()
        {
            RequiredPermissions = new List<string>();
            Tags = new List<string>();
            TimeoutSeconds = 30;
            ResultInterpretation = new ResultInterpretationPolicy();
            ScorePolicy = new DiagnosticScorePolicy();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DiagnosticCategory Category { get; set; }
        public string ScriptPath { get; set; }
        public string OriginalPath { get; set; }
        public bool ReadOnly { get; set; }
        public bool HealthCheckEnabled { get; set; }
        public DiagnosticExecutionCost ExecutionCost { get; set; }
        public DiagnosticScope ExecutionScope { get; set; }
        public string MinimumSqlServerVersion { get; set; }
        public string MaximumSqlServerVersion { get; set; }
        public bool RequiresQueryStore { get; set; }
        public IList<string> RequiredPermissions { get; private set; }
        public bool SupportsAzureSql { get; set; }
        public bool MultipleResultSets { get; set; }
        public DiagnosticSeverity DefaultSeverity { get; set; }
        public string DeduplicationGroup { get; set; }
        public int TimeoutSeconds { get; set; }
        public string CompatibilityNotes { get; set; }
        public IList<string> Tags { get; private set; }
        public ResultInterpretationPolicy ResultInterpretation { get; set; }
        public DiagnosticScorePolicy ScorePolicy { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id)) throw new InvalidOperationException("A diagnostic ID is required.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(Id, "^[a-z0-9]+(?:-[a-z0-9]+)*$")) throw new InvalidOperationException("The diagnostic ID must use lowercase kebab-case.");
            if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException("A diagnostic name is required.");
            if (string.IsNullOrWhiteSpace(ScriptPath)) throw new InvalidOperationException("A diagnostic script path is required.");
            if (TimeoutSeconds <= 0) throw new InvalidOperationException("The diagnostic timeout must be greater than zero.");
            if (TimeoutSeconds > 3600) throw new InvalidOperationException("The diagnostic timeout cannot exceed 3600 seconds.");
            if (!string.IsNullOrWhiteSpace(MinimumSqlServerVersion)) SqlServerVersion.Parse(MinimumSqlServerVersion);
            if (!string.IsNullOrWhiteSpace(MaximumSqlServerVersion)) SqlServerVersion.Parse(MaximumSqlServerVersion);
            if (HealthCheckEnabled && !ReadOnly) throw new InvalidOperationException("Only read-only diagnostics can be enabled for the automatic health check.");
            if (HealthCheckEnabled && ExecutionCost == DiagnosticExecutionCost.High) throw new InvalidOperationException("High-cost diagnostics require manual execution by default.");
            if (ResultInterpretation == null) throw new InvalidOperationException("A result interpretation policy is required.");
            if (ScorePolicy == null) throw new InvalidOperationException("A score policy is required.");
            ScorePolicy.Validate();
            if (ScorePolicy.ScoreEligible && ResultInterpretation.Mode == InterpretationMode.Unknown) throw new InvalidOperationException("An unknown interpretation cannot be score eligible.");
        }
    }
}