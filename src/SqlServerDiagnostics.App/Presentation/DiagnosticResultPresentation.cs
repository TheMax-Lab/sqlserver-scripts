using System;
using System.Linq;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Presentation
{
    public sealed class DiagnosticResultPresentation
    {
        public string GetDisplayStatus(DiagnosticResult result)
        {
            if (result == null) throw new ArgumentNullException("result");
            if (result.Status != DiagnosticExecutionStatus.Succeeded) return result.Status.ToString();
            if (result.Findings.Any(item => item.Severity == DiagnosticSeverity.Critical)) return "Critical";
            if (result.Findings.Any(item => item.Severity == DiagnosticSeverity.Warning)) return "Warning";
            if (result.Findings.Any(item => item.Severity == DiagnosticSeverity.Information)) return "Information";
            if (result.Findings.Any(item => item.Severity == DiagnosticSeverity.Passed)) return "Passed";
            return "Succeeded";
        }

        public string GetResultSetTitle(DiagnosticResultSet resultSet)
        {
            if (resultSet == null) throw new ArgumentNullException("resultSet");
            return string.Format("{0} ({1:N0} rows{2})", string.IsNullOrWhiteSpace(resultSet.Name) ? "Result Set " + (resultSet.Index + 1) : resultSet.Name, resultSet.RowCount, resultSet.IsTruncated ? ", truncated" : string.Empty);
        }

        public string GetTruncationMessage(DiagnosticResultSet resultSet)
        {
            if (resultSet == null || !resultSet.IsTruncated) return string.Empty;
            return string.Format("Showing {0:N0} of {1:N0} rows. Result was truncated for UI safety.", resultSet.RowCount, resultSet.RowsRead);
        }
    }
}