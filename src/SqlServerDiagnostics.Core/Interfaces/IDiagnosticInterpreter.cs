using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface IDiagnosticInterpreter
    {
        DiagnosticInterpretation Interpret(DiagnosticDefinition definition, DiagnosticResult result);
    }
}