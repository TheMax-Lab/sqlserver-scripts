namespace TheMaxLab.SqlServerDiagnostics.Core.Enums
{
    public enum DiagnosticProgressStage
    {
        Started = 0,
        Executing = 1,
        Completed = 2,
        Failed = 3,
        Skipped = 4,
        Cancelled = 5
    }
}