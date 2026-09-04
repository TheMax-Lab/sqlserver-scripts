using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Infrastructure.Profiles
{
    public sealed class JsonConnectionProfileService : IConnectionProfileService
    {
        private readonly string filePath;
        private readonly ICredentialService credentialService;

        public JsonConnectionProfileService(string filePath) : this(filePath, null)
        {
        }

        public JsonConnectionProfileService(string filePath, ICredentialService credentialService)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A profile file path is required.", "filePath");
            this.filePath = Path.GetFullPath(filePath);
            this.credentialService = credentialService;
        }

        public async Task<IReadOnlyList<ConnectionProfile>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(filePath)) return new List<ConnectionProfile>().AsReadOnly();
            return await Task.Run<IReadOnlyList<ConnectionProfile>>(() =>
            {
                using (FileStream stream = File.OpenRead(filePath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(ProfileDocument));
                    ProfileDocument document;
                    try { document = serializer.ReadObject(stream) as ProfileDocument; }
                    catch (SerializationException) { throw new InvalidOperationException("The saved connection profile file is invalid."); }
                    catch (System.Xml.XmlException) { throw new InvalidOperationException("The saved connection profile file is invalid."); }
                    if (document != null && document.SchemaVersion != 0 && document.SchemaVersion != 1) throw new InvalidOperationException("The saved connection profile version is not supported.");
                    return (document == null || document.Profiles == null ? new List<ProfileDto>() : document.Profiles)
                        .Select(item => item.ToModel()).ToList().AsReadOnly();
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task SaveAsync(ConnectionProfile profile, CancellationToken cancellationToken)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            profile.Validate();
            var profiles = (await GetAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
            profiles.RemoveAll(item => string.Equals(item.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
            profiles.Add(profile);
            await WriteAsync(profiles, cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A profile ID is required.", "id");
            var existing = (await GetAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
            ConnectionProfile deleted = existing.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            var profiles = existing.Where(item => !string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
            await WriteAsync(profiles, cancellationToken).ConfigureAwait(false);
            if (deleted != null && credentialService != null && !string.IsNullOrWhiteSpace(deleted.CredentialKey)) await credentialService.DeleteAsync(deleted.CredentialKey, cancellationToken).ConfigureAwait(false);
        }

        private Task WriteAsync(IReadOnlyCollection<ConnectionProfile> profiles, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporaryPath = filePath + ".tmp";
                var document = new ProfileDocument { SchemaVersion = 1, Profiles = profiles.Select(ProfileDto.FromModel).ToList() };
                using (FileStream stream = File.Create(temporaryPath)) new DataContractJsonSerializer(typeof(ProfileDocument)).WriteObject(stream, document);
                if (File.Exists(filePath)) File.Replace(temporaryPath, filePath, null); else File.Move(temporaryPath, filePath);
            }, cancellationToken);
        }

        [DataContract]
        private sealed class ProfileDocument
        {
            [DataMember(Name = "schemaVersion", Order = 1)] public int SchemaVersion { get; set; }
            [DataMember(Name = "profiles", Order = 2)] public List<ProfileDto> Profiles { get; set; }
        }

        [DataContract]
        private sealed class ProfileDto
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "server")] public string Server { get; set; }
            [DataMember(Name = "database")] public string Database { get; set; }
            [DataMember(Name = "authenticationType")] public string AuthenticationType { get; set; }
            [DataMember(Name = "userName")] public string UserName { get; set; }
            [DataMember(Name = "credentialKey")] public string CredentialKey { get; set; }
            [DataMember(Name = "encrypt")] public bool Encrypt { get; set; }
            [DataMember(Name = "trustServerCertificate")] public bool TrustServerCertificate { get; set; }
            [DataMember(Name = "timeoutSeconds")] public int TimeoutSeconds { get; set; }

            public ConnectionProfile ToModel()
            {
                AuthenticationType authentication;
                if (!Enum.TryParse(AuthenticationType, true, out authentication) || !Enum.IsDefined(typeof(AuthenticationType), authentication)) throw new SerializationException("A connection profile contains an invalid authentication type.");
                return new ConnectionProfile { Id = Id, Name = Name, Server = Server, Database = Database, AuthenticationType = authentication, UserName = UserName, CredentialKey = CredentialKey, Encrypt = Encrypt, TrustServerCertificate = TrustServerCertificate, TimeoutSeconds = TimeoutSeconds };
            }

            public static ProfileDto FromModel(ConnectionProfile model)
            {
                return new ProfileDto { Id = model.Id, Name = model.Name, Server = model.Server, Database = model.Database, AuthenticationType = model.AuthenticationType.ToString(), UserName = model.UserName, CredentialKey = model.CredentialKey, Encrypt = model.Encrypt, TrustServerCertificate = model.TrustServerCertificate, TimeoutSeconds = model.TimeoutSeconds };
            }
        }
    }
}