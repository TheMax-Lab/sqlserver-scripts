using System;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class ConnectionProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Server { get; set; }
        public string Database { get; set; }
        public AuthenticationType AuthenticationType { get; set; }
        public string UserName { get; set; }
        public string CredentialKey { get; set; }
        public bool Encrypt { get; set; }
        public bool TrustServerCertificate { get; set; }
        public int TimeoutSeconds { get; set; } = 15;

        public DatabaseConnection ToConnection()
        {
            return new DatabaseConnection { ServerName = Server, DatabaseName = Database, AuthenticationType = AuthenticationType, UserName = UserName, CredentialKey = CredentialKey, Encrypt = Encrypt, TrustServerCertificate = TrustServerCertificate, ConnectionTimeoutSeconds = TimeoutSeconds };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id)) throw new InvalidOperationException("A profile ID is required.");
            if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException("A profile name is required.");
            ToConnection().Validate();
        }
    }
}