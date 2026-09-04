namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DatabaseInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int CompatibilityLevel { get; set; }
        public string State { get; set; }
        public bool IsAccessible { get; set; }
        public bool IsQueryStoreEnabled { get; set; }
    }
}