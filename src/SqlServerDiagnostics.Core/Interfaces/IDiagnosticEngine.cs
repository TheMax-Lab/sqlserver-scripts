using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface IDiagnosticEngine
    {
        Task<DiagnosticResult> ExecuteAsync(DiagnosticDefinition definition, DiagnosticExecutionContext context, CancellationToken cancellationToken);
        Task<HealthCheckReport> RunHealthCheckAsync(DiagnosticExecutionContext context, IProgress<DiagnosticProgress> progress, CancellationToken cancellationToken);
        Task<IReadOnlyList<DiagnosticDefinition>> GetDiagnosticsAsync(CancellationToken cancellationToken);
        Task<IReadOnlyList<DiagnosticDefinition>> GetHealthCheckCandidatesAsync(DiagnosticExecutionContext context, CancellationToken cancellationToken);
    }
}