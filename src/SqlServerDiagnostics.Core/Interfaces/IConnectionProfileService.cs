using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface IConnectionProfileService
    {
        Task<IReadOnlyList<ConnectionProfile>> GetAllAsync(CancellationToken cancellationToken);
        Task SaveAsync(ConnectionProfile profile, CancellationToken cancellationToken);
        Task DeleteAsync(string id, CancellationToken cancellationToken);
    }
}