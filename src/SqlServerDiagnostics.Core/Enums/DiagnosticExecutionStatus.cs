namespace TheMaxLab.SqlServerDiagnostics.Core.Enums
{
    public enum DiagnosticExecutionStatus
    {
        NotStarted = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
        Skipped = 4,
        Cancelled = 5
    }
}