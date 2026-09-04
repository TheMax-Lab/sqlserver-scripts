using System.Threading;
using System.Threading.Tasks;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface ICredentialService
    {
        Task SaveAsync(string key, string userName, string password, CancellationToken cancellationToken);
        Task<string> GetPasswordAsync(string key, CancellationToken cancellationToken);
        Task DeleteAsync(string key, CancellationToken cancellationToken);
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);
    }
}