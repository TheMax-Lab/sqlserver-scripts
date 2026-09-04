using System.Collections.Generic;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class SqlQueryResult
    {
        public SqlQueryResult() { ResultSets = new List<DiagnosticResultSet>(); }
        public IList<DiagnosticResultSet> ResultSets { get; private set; }
    }
}