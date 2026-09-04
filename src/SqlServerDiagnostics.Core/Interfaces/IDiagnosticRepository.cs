using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface IDiagnosticRepository
    {
        Task<IReadOnlyList<DiagnosticDefinition>> GetAllAsync(CancellationToken cancellationToken);
        Task<string> LoadScriptAsync(DiagnosticDefinition definition, CancellationToken cancellationToken);
    }
}