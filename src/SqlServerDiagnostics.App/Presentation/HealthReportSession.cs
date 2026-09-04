using System;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Presentation
{
    public sealed class HealthReportSession
    {
        private HealthReport report;
        private string server;
        private string database;

        public void Set(HealthReport value, DiagnosticExecutionContext context)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (context == null || context.Server == null || context.Database == null) throw new ArgumentException("A complete execution context is required.", "context");
            report = value; server = context.Server.ServerName; database = context.Database.Name;
        }

        public HealthReport GetFor(DiagnosticExecutionContext context)
        {
            if (report == null || context == null || context.Server == null || context.Database == null) return null;
            return string.Equals(server, context.Server.ServerName, StringComparison.OrdinalIgnoreCase) && string.Equals(database, context.Database.Name, StringComparison.OrdinalIgnoreCase) ? report : null;
        }

        public void Clear() { report = null; server = null; database = null; }
    }
}