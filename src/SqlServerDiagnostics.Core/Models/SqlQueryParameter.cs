using System.Data;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class SqlQueryParameter
    {
        public string Name { get; set; }
        public SqlDbType Type { get; set; }
        public object Value { get; set; }
        public int? Size { get; set; }
    }
}