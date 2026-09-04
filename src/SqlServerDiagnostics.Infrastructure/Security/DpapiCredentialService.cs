using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;

namespace TheMaxLab.SqlServerDiagnostics.Infrastructure.Security
{
    public sealed class DpapiCredentialService : ICredentialService
    {
        private const string SessionPrefix = "session:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TheMaxLab.SqlServerDiagnostics.v1");
        private readonly ConcurrentDictionary<string, string> sessionSecrets = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private readonly string credentialDirectory;

        public DpapiCredentialService(string credentialDirectory)
        {
            if (string.IsNullOrWhiteSpace(credentialDirectory)) throw new ArgumentException("A credential directory is required.", "credentialDirectory");
            this.credentialDirectory = Path.GetFullPath(credentialDirectory);
        }

        public Task SaveAsync(string key, string userName, string password, CancellationToken cancellationToken)
        {
            ValidateKey(key);
            if (password == null) throw new ArgumentNullException("password");
            cancellationToken.ThrowIfCancellationRequested();
            if (key.StartsWith(SessionPrefix, StringComparison.Ordinal))
            {
                sessionSecrets[key] = password;
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(credentialDirectory);
                byte[] plaintext = Encoding.UTF8.GetBytes(password);
                try
                {
                    byte[] protectedBytes = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
                    File.WriteAllBytes(GetPath(key), protectedBytes);
                }
                finally { Array.Clear(plaintext, 0, plaintext.Length); }
            }, cancellationToken);
        }

        public Task<string> GetPasswordAsync(string key, CancellationToken cancellationToken)
        {
            ValidateKey(key);
            cancellationToken.ThrowIfCancellationRequested();
            string sessionPassword;
            if (key.StartsWith(SessionPrefix, StringComparison.Ordinal))
            {
                if (!sessionSecrets.TryGetValue(key, out sessionPassword)) throw new InvalidOperationException("The session credential is unavailable.");
                return Task.FromResult(sessionPassword);
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = GetPath(key);
                if (!File.Exists(path)) throw new InvalidOperationException("The protected credential is unavailable.");
                try
                {
                    byte[] protectedBytes = File.ReadAllBytes(path);
                    byte[] plaintext = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                    try { return Encoding.UTF8.GetString(plaintext); }
                    finally { Array.Clear(plaintext, 0, plaintext.Length); }
                }
                catch (CryptographicException)
                {
                    throw new InvalidOperationException("The protected credential is invalid or unavailable for the current Windows user.");
                }
                catch (IOException)
                {
                    throw new InvalidOperationException("The protected credential could not be read.");
                }
            }, cancellationToken);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            ValidateKey(key);
            cancellationToken.ThrowIfCancellationRequested();
            string removed;
            sessionSecrets.TryRemove(key, out removed);
            if (!key.StartsWith(SessionPrefix, StringComparison.Ordinal))
            {
                string path = GetPath(key);
                if (File.Exists(path)) File.Delete(path);
            }
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
        {
            ValidateKey(key);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(key.StartsWith(SessionPrefix, StringComparison.Ordinal) ? sessionSecrets.ContainsKey(key) : File.Exists(GetPath(key)));
        }

        private string GetPath(string key)
        {
            byte[] bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
            var name = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) name.Append(value.ToString("x2"));
            return Path.Combine(credentialDirectory, name + ".credential");
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A credential key is required.", "key");
        }
    }
}