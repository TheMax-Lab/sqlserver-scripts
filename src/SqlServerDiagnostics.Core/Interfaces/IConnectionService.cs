using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface IConnectionService
    {
        Task<ConnectionTestResult> TestAsync(DatabaseConnection connection, CancellationToken cancellationToken);
        string GetSanitizedDescription(DatabaseConnection connection);
    }
}