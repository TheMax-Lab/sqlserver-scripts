using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class SqlServerInfo
    {
        public string ServerName { get; set; }
        public string ProductVersion { get; set; }
        public int MajorVersion { get; set; }
        public string Edition { get; set; }
        public string ProductLevel { get; set; }
        public SqlServerEngineType EngineType { get; set; }
    }
}