using System;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DatabaseConnection
    {
        public DatabaseConnection()
        {
            ConnectionTimeoutSeconds = 15;
        }

        public string ServerName { get; set; }
        public string DatabaseName { get; set; }
        public AuthenticationType AuthenticationType { get; set; }
        public string UserName { get; set; }
        public bool Encrypt { get; set; }
        public bool TrustServerCertificate { get; set; }
        public int ConnectionTimeoutSeconds { get; set; }
        public string CredentialKey { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ServerName)) throw new InvalidOperationException("A SQL Server name is required.");
            if (ConnectionTimeoutSeconds <= 0) throw new InvalidOperationException("The connection timeout must be greater than zero.");
            if (AuthenticationType == AuthenticationType.SqlServer && string.IsNullOrWhiteSpace(UserName)) throw new InvalidOperationException("A user name is required for SQL Server authentication.");
        }
    }
}