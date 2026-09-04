using System;
using System.Windows.Forms;
using TheMaxLab.SqlServerDiagnostics.App.Forms;
using TheMaxLab.SqlServerDiagnostics.App.Presentation;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Connections;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Logging;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Profiles;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Security;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Execution;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Repositories;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Interpretation;
using TheMaxLab.SqlServerDiagnostics.Reporting;

namespace TheMaxLab.SqlServerDiagnostics.App
{
    internal static class Program
    {
        private static IApplicationLogger applicationLogger;

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string localData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheMaxLab", "SqlServerDiagnostics");
            ICredentialService credentials = new DpapiCredentialService(System.IO.Path.Combine(localData, "Credentials"));
            IConnectionProfileService profiles = new JsonConnectionProfileService(System.IO.Path.Combine(localData, "profiles.json"), credentials);
            IApplicationLogger logger = new FileApplicationLogger(System.IO.Path.Combine(localData, "Logs"));
            applicationLogger = logger;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += ApplicationThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            var connectionStringFactory = new SecureConnectionStringFactory(credentials);
            ISqlServerService sqlServerService = new SqlServerService(connectionStringFactory);
            IConnectionService connectionService = new ConnectionService(sqlServerService, connectionStringFactory, logger);
            string diagnosticsRoot = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics");
            IDiagnosticRepository diagnosticRepository = new ManifestDiagnosticRepository(diagnosticsRoot);
            IDiagnosticEngine diagnosticEngine = new DiagnosticEngine(diagnosticRepository, sqlServerService, new DiagnosticResultNormalizer(), new DiagnosticErrorClassifier(), logger);
            IDiagnosticInterpreter diagnosticInterpreter = new DiagnosticInterpreter();
            IHealthScoreService healthScoreService = new HealthScoreService();
            IReportService reportService = new HealthReportService();
            IExternalLinkLauncher externalLinkLauncher = new ExternalLinkLauncher(logger);
            logger.Log(new Core.Models.LogEvent { Level = Core.Enums.LogLevel.Information, EventName = "application.startup", Message = "SQL Server Diagnostics started." });
            Application.Run(new MainForm(connectionService, sqlServerService, credentials, profiles, diagnosticEngine, diagnosticRepository, diagnosticInterpreter, healthScoreService, reportService, externalLinkLauncher));
        }

        private static void ApplicationThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            LogUnexpected(e.Exception, "application.ui-unhandled");
            MessageBox.Show("An unexpected error occurred. The operation could not be completed. You can continue using the application.", "SQL Server Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogUnexpected(e.ExceptionObject as Exception, "application.domain-unhandled");
        }

        private static void LogUnexpected(Exception exception, string eventName)
        {
            if (applicationLogger == null) return;
            try { applicationLogger.Log(new Core.Models.LogEvent { Level = Core.Enums.LogLevel.Error, EventName = eventName, Message = "An unexpected application error occurred." }, exception); }
            catch { }
        }
    }
}