using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Presentation
{
    public static class AboutPresentation
    {
        public const string Author = "TheMax-Lab";
        public const string RepositoryName = "TheMax-Lab/sqlserver-scripts";
        public const string RepositoryUrl = "https://github.com/TheMax-Lab/sqlserver-scripts";
        public const string DonationUrl = "https://paypal.me/TheMaxLab";
        public const string License = "MIT License";
        public const string Description = "SQL Server Diagnostics is a Windows desktop application designed to help database administrators and developers inspect the health, configuration, performance and potential issues of Microsoft SQL Server instances.";
        public const string Detail = "The application runs a curated set of read-only diagnostic queries and presents the results through health assessments, findings and exportable reports.";

        public static string ApplicationName { get { return ApplicationInfo.ProductName; } }
        public static string Version { get { return ApplicationInfo.ApplicationVersion; } }
    }
}