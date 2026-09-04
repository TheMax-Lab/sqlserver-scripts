using System;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Presentation
{
    public sealed class ConnectionProfilePresentation
    {
        public ConnectionProfile CreateProfile(DatabaseConnection connection, string name, string existingId = null)
        {
            if (connection == null) throw new ArgumentNullException("connection");
            connection.Validate();
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("A profile name is required.");
            if (connection.AuthenticationType == AuthenticationType.SqlServer &&
                (string.IsNullOrWhiteSpace(connection.CredentialKey) || !connection.CredentialKey.StartsWith("saved:", StringComparison.Ordinal)))
                throw new InvalidOperationException("Enable Remember credentials before saving a SQL Server authentication profile.");

            var profile = new ConnectionProfile
            {
                Id = string.IsNullOrWhiteSpace(existingId) ? Guid.NewGuid().ToString("N") : existingId,
                Name = name.Trim(),
                Server = connection.ServerName,
                Database = connection.DatabaseName,
                AuthenticationType = connection.AuthenticationType,
                UserName = connection.AuthenticationType == AuthenticationType.SqlServer ? connection.UserName : null,
                CredentialKey = connection.AuthenticationType == AuthenticationType.SqlServer ? connection.CredentialKey : null,
                Encrypt = connection.Encrypt,
                TrustServerCertificate = connection.TrustServerCertificate,
                TimeoutSeconds = connection.ConnectionTimeoutSeconds
            };
            profile.Validate();
            return profile;
        }
    }
}