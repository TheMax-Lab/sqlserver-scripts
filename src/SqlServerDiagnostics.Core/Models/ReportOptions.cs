namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class ReportOptions
    {
        public ReportOptions()
        {
            IncludeRawResults = false;
            IncludeSuggestedSql = true;
            IncludeDiagnosticDetails = true;
        }

        public bool IncludeRawResults { get; set; }
        public bool IncludeSuggestedSql { get; set; }
        public bool IncludeDiagnosticDetails { get; set; }
    }
}