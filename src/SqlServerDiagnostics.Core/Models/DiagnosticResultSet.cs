using System.Collections.Generic;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DiagnosticResultSet
    {
        public DiagnosticResultSet()
        {
            Columns = new List<DiagnosticColumn>();
            Rows = new List<IReadOnlyDictionary<string, object>>();
        }

        public int Index { get; set; }
        public string Name { get; set; }
        public IList<DiagnosticColumn> Columns { get; private set; }
        public IList<IReadOnlyDictionary<string, object>> Rows { get; private set; }
        public int RowCount { get { return Rows.Count; } }
        public long RowsRead { get; set; }
        public bool IsTruncated { get; set; }
    }
}