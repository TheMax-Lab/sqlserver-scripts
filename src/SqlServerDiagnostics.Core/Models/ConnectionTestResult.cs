namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class ConnectionTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Enums.DiagnosticFailureKind FailureKind { get; set; }
        public SqlServerInfo Server { get; set; }
        public DatabaseInfo Database { get; set; }
    }
}