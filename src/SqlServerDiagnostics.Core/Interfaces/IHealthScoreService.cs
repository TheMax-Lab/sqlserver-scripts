using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface IHealthScoreService
    {
        HealthReport BuildReport(HealthCheckReport report, System.Collections.Generic.IReadOnlyList<DiagnosticDefinition> definitions, System.Collections.Generic.IReadOnlyList<DiagnosticInterpretation> interpretations);
    }
}