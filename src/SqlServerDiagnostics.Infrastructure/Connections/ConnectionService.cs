using System;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Infrastructure.Connections
{
    public sealed class ConnectionService : IConnectionService
    {
        private readonly ISqlServerService sqlServerService;
        private readonly SecureConnectionStringFactory connectionStringFactory;
        private readonly IApplicationLogger logger;

        public ConnectionService(ISqlServerService sqlServerService, SecureConnectionStringFactory connectionStringFactory, IApplicationLogger logger)
        {
            this.sqlServerService = sqlServerService ?? throw new ArgumentNullException("sqlServerService");
            this.connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException("connectionStringFactory");
            this.logger = logger ?? throw new ArgumentNullException("logger");
        }

        public async Task<ConnectionTestResult> TestAsync(DatabaseConnection connection, CancellationToken cancellationToken)
        {
            Log(LogLevel.Information, "connection.attempt", "Testing a SQL Server connection.", null);
            try
            {
                SqlServerInfo server = await sqlServerService.GetServerInfoAsync(connection, cancellationToken).ConfigureAwait(false);
                DatabaseInfo database = await sqlServerService.GetDatabaseInfoAsync(connection, cancellationToken).ConfigureAwait(false);
                Log(LogLevel.Information, "connection.success", "SQL Server connection succeeded.", null);
                return new ConnectionTestResult { Success = true, Message = "Connection succeeded.", FailureKind = DiagnosticFailureKind.None, Server = server, Database = database };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                Log(LogLevel.Warning, "connection.failure", "SQL Server connection failed.", exception);
                return new ConnectionTestResult { Success = false, Message = "Unable to connect to SQL Server. Verify the server, authentication, network, and certificate settings.", FailureKind = Classify(exception) };
            }
        }

        public string GetSanitizedDescription(DatabaseConnection connection) { return connectionStringFactory.CreateSanitized(connection); }

        private void Log(LogLevel level, string eventName, string message, Exception exception)
        {
            var logEvent = new LogEvent { Level = level, EventName = eventName, Message = message };
            logger.Log(logEvent, exception);
        }

        private static DiagnosticFailureKind Classify(Exception exception)
        {
            var sqlException = exception as System.Data.SqlClient.SqlException;
            if (sqlException == null) return DiagnosticFailureKind.ConnectionFailure;
            if (sqlException.Number == -2) return DiagnosticFailureKind.Timeout;
            if (sqlException.Number == 18456 || sqlException.Number == 229 || sqlException.Number == 916) return DiagnosticFailureKind.PermissionDenied;
            if (sqlException.Number == 4060 || sqlException.Number == 911) return DiagnosticFailureKind.DatabaseUnavailable;
            return DiagnosticFailureKind.ConnectionFailure;
        }
    }
}