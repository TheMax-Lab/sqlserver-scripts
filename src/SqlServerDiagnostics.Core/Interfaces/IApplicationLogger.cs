using System;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface IApplicationLogger
    {
        void Log(LogEvent logEvent, Exception exception = null);
    }
}