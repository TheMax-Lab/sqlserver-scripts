using System;
using System.Collections.Generic;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Execution
{
    public sealed class DiagnosticResultNormalizer
    {
        public void Normalize(DiagnosticDefinition definition, DiagnosticResult result)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (result == null) throw new ArgumentNullException("result");
            foreach (DiagnosticResultSet resultSet in result.ResultSets)
            {
                if (!HasNormalizedContract(resultSet)) continue;
                foreach (IReadOnlyDictionary<string, object> row in resultSet.Rows)
                {
                    var finding = new DiagnosticFinding
                    {
                        Severity = MapSeverity(GetString(row, "Priority"), definition.DefaultSeverity),
                        Title = GetString(row, "Finding"),
                        Description = GetString(row, "Evidence"),
                        Recommendation = GetString(row, "Recommendation"),
                        SuggestedSql = GetString(row, "SuggestedSql")
                    };
                    foreach (var item in row) finding.Data[item.Key] = item.Value;
                    result.Findings.Add(finding);
                }
            }
        }

        private static bool HasNormalizedContract(DiagnosticResultSet resultSet)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DiagnosticColumn column in resultSet.Columns) names.Add(column.Name);
            return names.Contains("Priority") && names.Contains("Finding") && names.Contains("Recommendation");
        }

        private static string GetString(IReadOnlyDictionary<string, object> row, string key)
        {
            object value;
            return row.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }

        private static DiagnosticSeverity MapSeverity(string priority, DiagnosticSeverity fallback)
        {
            if (string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase)) return DiagnosticSeverity.Critical;
            if (string.Equals(priority, "Medium", StringComparison.OrdinalIgnoreCase)) return DiagnosticSeverity.Warning;
            if (string.Equals(priority, "Low", StringComparison.OrdinalIgnoreCase)) return DiagnosticSeverity.Information;
            if (string.Equals(priority, "Info", StringComparison.OrdinalIgnoreCase)) return DiagnosticSeverity.Information;
            return fallback;
        }
    }
}