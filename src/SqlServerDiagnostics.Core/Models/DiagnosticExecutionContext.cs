using System;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DiagnosticExecutionContext
    {
        public DatabaseConnection Connection { get; set; }
        public SqlServerInfo Server { get; set; }
        public DatabaseInfo Database { get; set; }

        public bool Supports(DiagnosticDefinition definition, out DiagnosticFailureKind failureKind, out string reason)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (Server == null) { failureKind = DiagnosticFailureKind.ConnectionFailure; reason = "SQL Server information is unavailable."; return false; }
            var detectedVersion = new SqlServerVersion(Server.MajorVersion, 0, 0, 0);
            if (!string.IsNullOrWhiteSpace(definition.MinimumSqlServerVersion) && detectedVersion.CompareTo(SqlServerVersion.Parse(definition.MinimumSqlServerVersion)) < 0) { failureKind = DiagnosticFailureKind.UnsupportedVersion; reason = "Requires SQL Server " + definition.MinimumSqlServerVersion + " or later; detected " + Server.ProductVersion + "."; return false; }
            if (!string.IsNullOrWhiteSpace(definition.MaximumSqlServerVersion) && detectedVersion.CompareTo(SqlServerVersion.Parse(definition.MaximumSqlServerVersion)) > 0) { failureKind = DiagnosticFailureKind.UnsupportedVersion; reason = "Supports SQL Server through " + definition.MaximumSqlServerVersion + "; detected " + Server.ProductVersion + "."; return false; }
            if (definition.ExecutionScope == DiagnosticScope.Database && (Database == null || !Database.IsAccessible)) { failureKind = DiagnosticFailureKind.DatabaseUnavailable; reason = "The selected database is unavailable."; return false; }
            if (definition.RequiresQueryStore && (Database == null || !Database.IsQueryStoreEnabled)) { failureKind = DiagnosticFailureKind.QueryStoreUnavailable; reason = "Query Store is unavailable for the selected database."; return false; }
            if (Server.EngineType == SqlServerEngineType.AzureSqlDatabase && !definition.SupportsAzureSql) { failureKind = DiagnosticFailureKind.UnsupportedVersion; reason = "This diagnostic does not support Azure SQL Database."; return false; }
            failureKind = DiagnosticFailureKind.None;
            reason = null;
            return true;
        }

        public bool IsHealthCheckEligible(DiagnosticDefinition definition, out DiagnosticFailureKind failureKind, out string reason)
        {
            if (!definition.HealthCheckEnabled) { failureKind = DiagnosticFailureKind.InvalidDefinition; reason = "The diagnostic is not enabled for the automatic Health Check."; return false; }
            if (!definition.ReadOnly) { failureKind = DiagnosticFailureKind.InvalidDefinition; reason = "Only read-only diagnostics can run automatically."; return false; }
            if (definition.ExecutionCost == DiagnosticExecutionCost.High) { failureKind = DiagnosticFailureKind.InvalidDefinition; reason = "High-cost diagnostics require manual execution."; return false; }
            return Supports(definition, out failureKind, out reason);
        }
    }
}