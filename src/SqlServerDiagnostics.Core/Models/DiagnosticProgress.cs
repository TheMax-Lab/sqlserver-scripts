namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DiagnosticProgress
    {
        public int Completed { get; set; }
        public int Total { get; set; }
        public string CurrentDiagnosticId { get; set; }
        public string CurrentDiagnosticName { get; set; }
        public Enums.DiagnosticProgressStage Stage { get; set; }
        public string Message { get; set; }
        public int Percentage { get { return Total <= 0 ? 0 : (Completed * 100) / Total; } }
    }
}