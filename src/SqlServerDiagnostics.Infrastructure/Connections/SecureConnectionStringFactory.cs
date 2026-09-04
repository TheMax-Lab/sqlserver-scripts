using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Infrastructure.Connections
{
    public sealed class SecureConnectionStringFactory
    {
        private readonly ICredentialService credentialService;

        public SecureConnectionStringFactory(ICredentialService credentialService)
        {
            this.credentialService = credentialService ?? throw new ArgumentNullException("credentialService");
        }

        public async Task<string> CreateAsync(DatabaseConnection connection, CancellationToken cancellationToken)
        {
            if (connection == null) throw new ArgumentNullException("connection");
            connection.Validate();
            var builder = CreateBaseBuilder(connection);
            if (connection.AuthenticationType == AuthenticationType.Windows)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.IntegratedSecurity = false;
                builder.UserID = connection.UserName;
                builder.Password = await credentialService.GetPasswordAsync(connection.CredentialKey, cancellationToken).ConfigureAwait(false);
            }
            return builder.ConnectionString;
        }

        public string CreateSanitized(DatabaseConnection connection)
        {
            if (connection == null) throw new ArgumentNullException("connection");
            connection.Validate();
            var builder = CreateBaseBuilder(connection);
            builder.IntegratedSecurity = connection.AuthenticationType == AuthenticationType.Windows;
            if (!builder.IntegratedSecurity) builder.UserID = connection.UserName;
            builder.Remove("Password");
            return builder.ConnectionString;
        }

        private static SqlConnectionStringBuilder CreateBaseBuilder(DatabaseConnection connection)
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = connection.ServerName,
                InitialCatalog = string.IsNullOrWhiteSpace(connection.DatabaseName) ? "master" : connection.DatabaseName,
                ConnectTimeout = connection.ConnectionTimeoutSeconds,
                Encrypt = connection.Encrypt,
                TrustServerCertificate = connection.TrustServerCertificate,
                ApplicationName = "SQL Server Diagnostics",
                PersistSecurityInfo = false,
                MultipleActiveResultSets = false
            };
        }
    }
}