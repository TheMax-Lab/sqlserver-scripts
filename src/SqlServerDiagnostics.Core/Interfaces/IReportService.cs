using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface IReportService
    {
        Task ExportFindingsCsvAsync(HealthReport report, ReportOptions options, string filePath, CancellationToken cancellationToken);
        Task ExportDiagnosticsCsvAsync(HealthReport report, ReportOptions options, string filePath, CancellationToken cancellationToken);
        Task ExportJsonAsync(HealthReport report, ReportOptions options, string filePath, CancellationToken cancellationToken);
        Task ExportHtmlAsync(HealthReport report, ReportOptions options, string filePath, CancellationToken cancellationToken);
        string CreateSafeFileName(HealthReport report, string extension);
    }
}