using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Execution;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Repositories;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Connections;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Profiles;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Security;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Tests
{
    [TestClass]
    public sealed class Phase3BackendTests
    {
        [TestMethod]
        public void Repository_ProductionManifestContains26ValidDiagnostics()
        {
            string root = FindRepositoryRoot();
            var repository = new ManifestDiagnosticRepository(Path.Combine(root, "diagnostics"));
            var definitions = repository.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(26, definitions.Count);
            Assert.AreEqual(26, definitions.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.AreEqual(0, definitions.Count(item => item.HealthCheckEnabled && (!item.ReadOnly || item.ExecutionCost == DiagnosticExecutionCost.High)));
        }

        [TestMethod]
        public void Repository_CorrectedDiagnosticsUseSafeCollationAndDelimitedRowCount()
        {
            string root = FindRepositoryRoot();
            var repository = new ManifestDiagnosticRepository(Path.Combine(root, "diagnostics"));
            var definitions = repository.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult();

            DiagnosticDefinition databaseSizes = definitions.Single(item => item.Id == "database-sizes");
            string databaseSizesSql = repository.LoadScriptAsync(databaseSizes, CancellationToken.None).GetAwaiter().GetResult();
            StringAssert.Contains(databaseSizesSql, "QUOTENAME(af.[logical_name] COLLATE DATABASE_DEFAULT)");
            StringAssert.Contains(databaseSizesSql, "REPLACE(af.[logical_name] COLLATE DATABASE_DEFAULT, '''', '''''')");
            StringAssert.Contains(databaseSizesSql, "af.[file_type_desc] COLLATE DATABASE_DEFAULT,");
            StringAssert.Contains(databaseSizesSql, "af.[state_desc] COLLATE DATABASE_DEFAULT,");
            Assert.IsFalse(databaseSizesSql.Contains("QUOTENAME(af.[logical_name])"));
            Assert.IsFalse(databaseSizesSql.Contains("REPLACE(af.[logical_name], '''', '''''')"));
            Assert.IsFalse(databaseSizesSql.Contains("        af.[file_type_desc],"));
            Assert.IsFalse(databaseSizesSql.Contains("        af.[state_desc],"));

            DiagnosticDefinition missingPrimaryKeys = definitions.Single(item => item.Id == "missing-primary-keys");
            string missingPrimaryKeysSql = repository.LoadScriptAsync(missingPrimaryKeys, CancellationToken.None).GetAwaiter().GetResult();
            StringAssert.Contains(missingPrimaryKeysSql, "AS [RowCount]");
            StringAssert.Contains(missingPrimaryKeysSql, "tr.[RowCount]");
            Assert.IsFalse(missingPrimaryKeysSql.Contains(" AS RowCount"));
            Assert.IsFalse(missingPrimaryKeysSql.Contains("tr.RowCount"));
        }

        [TestMethod]
        public void Repository_RejectsDuplicateIdsMissingScriptsInvalidEnumsAndTraversal()
        {
            AssertManifestFails("{\"schemaVersion\":1,\"diagnostics\":[" + DefinitionJson("same", "Performance/a.sql", "Low") + "," + DefinitionJson("same", "Performance/a.sql", "Low") + "]}", true);
            AssertManifestFails("{\"schemaVersion\":1,\"diagnostics\":[" + DefinitionJson("missing", "Performance/missing.sql", "Low") + "]}", false);
            AssertManifestFails("{\"schemaVersion\":1,\"diagnostics\":[" + DefinitionJson("bad-enum", "Performance/a.sql", "Impossible") + "]}", true);
            AssertManifestFails("{\"schemaVersion\":1,\"diagnostics\":[" + DefinitionJson("traversal", "../a.sql", "Low") + "]}", true);
        }

        [TestMethod]
        public void Normalizer_PreservesGeneratedSqlAsDataOnly()
        {
            var definition = ValidDefinition();
            var set = new DiagnosticResultSet();
            foreach (string name in new[] { "Priority", "Finding", "Evidence", "Recommendation", "SuggestedSql" }) set.Columns.Add(new DiagnosticColumn { Name = name });
            set.Rows.Add(new Dictionary<string, object> { { "Priority", "High" }, { "Finding", "Candidate" }, { "Evidence", "Evidence" }, { "Recommendation", "Review" }, { "SuggestedSql", "CREATE INDEX IX_Test ON dbo.T(C);" } });
            set.RowsRead = 1;
            var result = new DiagnosticResult(); result.ResultSets.Add(set);
            new DiagnosticResultNormalizer().Normalize(definition, result);
            Assert.AreEqual(DiagnosticSeverity.Critical, result.Findings[0].Severity);
            Assert.AreEqual("CREATE INDEX IX_Test ON dbo.T(C);", result.Findings[0].SuggestedSql);
            Assert.AreEqual(1, result.ResultSets[0].Rows.Count);
        }

        [TestMethod]
        public void ErrorClassifier_MapsPermissionTimeoutAndCancellation()
        {
            var classifier = new DiagnosticErrorClassifier();
            Assert.AreEqual(DiagnosticFailureKind.PermissionDenied, classifier.ClassifySqlErrorNumber(229));
            Assert.AreEqual(DiagnosticFailureKind.Timeout, classifier.ClassifySqlErrorNumber(-2));
            Assert.AreEqual(DiagnosticFailureKind.Timeout, classifier.Classify(new TimeoutException()));
            Assert.AreEqual(DiagnosticFailureKind.Cancellation, classifier.Classify(new OperationCanceledException()));
        }

        [TestMethod]
        public void Engine_ExecutesOriginalScriptOnceAndPreservesMultipleResults()
        {
            var definition = ValidDefinition(); definition.MultipleResultSets = true;
            var repository = new FakeRepository(definition, "SELECT 'CREATE INDEX' AS SuggestedSql;");
            var sql = new FakeSqlServerService();
            sql.Result.ResultSets.Add(new DiagnosticResultSet { Index = 0, RowsRead = 1 });
            sql.Result.ResultSets.Add(new DiagnosticResultSet { Index = 1, RowsRead = 2, IsTruncated = true });
            var engine = CreateEngine(repository, sql);
            DiagnosticResult result = engine.ExecuteAsync(definition, Context(), CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(DiagnosticExecutionStatus.Succeeded, result.Status);
            Assert.AreEqual(2, result.ResultSets.Count);
            Assert.AreEqual(1, sql.ExecutionCount);
            Assert.AreEqual("SELECT 'CREATE INDEX' AS SuggestedSql;", sql.LastCommandText);
        }

        [TestMethod]
        public void Engine_MapsCancellationWithoutExecutingSql()
        {
            var tokenSource = new CancellationTokenSource(); tokenSource.Cancel();
            var sql = new FakeSqlServerService();
            DiagnosticResult result = CreateEngine(new FakeRepository(ValidDefinition(), "SELECT 1;"), sql).ExecuteAsync(ValidDefinition(), Context(), tokenSource.Token).GetAwaiter().GetResult();
            Assert.AreEqual(DiagnosticExecutionStatus.Cancelled, result.Status);
            Assert.AreEqual(DiagnosticFailureKind.Cancellation, result.FailureKind);
            Assert.AreEqual(0, sql.ExecutionCount);
        }

        [TestMethod]
        public void Engine_MapsTimeoutAndRunsHealthCheckSequentially()
        {
            var first = ValidDefinition(); first.Id = "first";
            var second = ValidDefinition(); second.Id = "second";
            var repository = new FakeRepository(new List<DiagnosticDefinition> { first, second }, "SELECT 1;");
            var sql = new FakeSqlServerService { ExceptionToThrow = new TimeoutException() };
            DiagnosticResult timeout = CreateEngine(repository, sql).ExecuteAsync(first, Context(), CancellationToken.None).Result;
            Assert.AreEqual(DiagnosticExecutionStatus.Failed, timeout.Status);
            Assert.AreEqual(DiagnosticFailureKind.Timeout, timeout.FailureKind);

            sql.ExceptionToThrow = null;
            HealthCheckReport report = CreateEngine(repository, sql).RunHealthCheckAsync(Context(), null, CancellationToken.None).Result;
            Assert.AreEqual(2, report.ExecutedCount);
            Assert.AreEqual(1, sql.MaximumConcurrentExecutions);
        }

        [TestMethod]
        public void SecureConnectionString_DoesNotExposePasswordInSanitizedOutput()
        {
            using (var directory = new TemporaryDirectory())
            {
                var credentials = new DpapiCredentialService(directory.Path);
                credentials.SaveAsync("session:test", "sa", "TopSecret!", CancellationToken.None).GetAwaiter().GetResult();
                var factory = new SecureConnectionStringFactory(credentials);
                var connection = new DatabaseConnection { ServerName = "localhost", DatabaseName = "master", AuthenticationType = AuthenticationType.SqlServer, UserName = "sa", CredentialKey = "session:test" };
                string actual = factory.CreateAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
                string sanitized = factory.CreateSanitized(connection);
                StringAssert.Contains(actual, "TopSecret!");
                Assert.IsFalse(sanitized.Contains("TopSecret!"));
                Assert.IsFalse(sanitized.ToLowerInvariant().Contains("password"));
            }
        }

        [TestMethod]
        public void DpapiCredentialService_ProtectsPersistentCredential()
        {
            using (var directory = new TemporaryDirectory())
            {
                var credentials = new DpapiCredentialService(directory.Path);
                credentials.SaveAsync("saved:test", "user", "TopSecret!", CancellationToken.None).GetAwaiter().GetResult();
                Assert.IsTrue(credentials.ExistsAsync("saved:test", CancellationToken.None).Result);
                Assert.AreEqual("TopSecret!", credentials.GetPasswordAsync("saved:test", CancellationToken.None).Result);
                string stored = Convert.ToBase64String(File.ReadAllBytes(Directory.GetFiles(directory.Path).Single()));
                Assert.IsFalse(stored.Contains("TopSecret!"));
                credentials.DeleteAsync("saved:test", CancellationToken.None).Wait();
                Assert.IsFalse(credentials.ExistsAsync("saved:test", CancellationToken.None).Result);
            }
        }

        [TestMethod]
        public void ConnectionProfileStore_PersistsNoPasswordField()
        {
            using (var directory = new TemporaryDirectory())
            {
                string path = Path.Combine(directory.Path, "profiles.json");
                var service = new JsonConnectionProfileService(path);
                service.SaveAsync(new ConnectionProfile { Id = "local", Name = "Local", Server = "localhost", Database = "master", AuthenticationType = AuthenticationType.SqlServer, UserName = "user", CredentialKey = "saved:key", TimeoutSeconds = 15 }, CancellationToken.None).Wait();
                ConnectionProfile loaded = service.GetAllAsync(CancellationToken.None).Result.Single();
                Assert.AreEqual("saved:key", loaded.CredentialKey);
                Assert.IsFalse(File.ReadAllText(path).ToLowerInvariant().Contains("password"));
            }
        }

        private static DiagnosticEngine CreateEngine(IDiagnosticRepository repository, ISqlServerService sql) { return new DiagnosticEngine(repository, sql, new DiagnosticResultNormalizer(), new DiagnosticErrorClassifier(), new NullLogger()); }
        private static DiagnosticDefinition ValidDefinition() { return new DiagnosticDefinition { Id = "sample", Name = "Sample", Description = "Sample", ScriptPath = "Performance/sample.sql", ReadOnly = true, HealthCheckEnabled = true, ExecutionCost = DiagnosticExecutionCost.Low, ExecutionScope = DiagnosticScope.Database, MinimumSqlServerVersion = "2016", TimeoutSeconds = 30, DefaultSeverity = DiagnosticSeverity.Information }; }
        private static DiagnosticExecutionContext Context() { return new DiagnosticExecutionContext { Connection = new DatabaseConnection { ServerName = "server", DatabaseName = "database" }, Server = new SqlServerInfo { MajorVersion = 16, ProductVersion = "16.0.1.0", EngineType = SqlServerEngineType.SqlServer }, Database = new DatabaseInfo { Name = "database", IsAccessible = true, IsQueryStoreEnabled = true } }; }
        private static string DefinitionJson(string id, string path, string cost) { return "{\"id\":\"" + id + "\",\"name\":\"Name\",\"description\":\"Description\",\"category\":\"Performance\",\"scriptPath\":\"" + path.Replace("\\", "\\\\") + "\",\"readOnly\":true,\"healthCheckEnabled\":false,\"executionCost\":\"" + cost + "\",\"executionScope\":\"Database\",\"minimumSqlServerVersion\":\"2016\",\"requiresQueryStore\":false,\"requiredPermissions\":[],\"supportsAzureSql\":false,\"multipleResultSets\":false,\"defaultSeverity\":\"Warning\",\"timeoutSeconds\":30}"; }

        private static void AssertManifestFails(string json, bool createScript)
        {
            using (var directory = new TemporaryDirectory())
            {
                Directory.CreateDirectory(Path.Combine(directory.Path, "Performance"));
                if (createScript) File.WriteAllText(Path.Combine(directory.Path, "Performance", "a.sql"), "SELECT 1;");
                File.WriteAllText(Path.Combine(directory.Path, "manifest.json"), json);
                try { new ManifestDiagnosticRepository(directory.Path).GetAllAsync(CancellationToken.None).GetAwaiter().GetResult(); Assert.Fail("Expected manifest validation failure."); }
                catch (Exception exception) { Assert.IsTrue(exception is InvalidDataException || exception is FileNotFoundException); }
            }
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "SqlServerDiagnostics.sln"))) directory = directory.Parent;
            if (directory == null) Assert.Inconclusive("Repository root was not found.");
            return directory.FullName;
        }

        private sealed class FakeRepository : IDiagnosticRepository
        {
            private readonly List<DiagnosticDefinition> definitions; private readonly string script;
            public FakeRepository(DiagnosticDefinition definition, string script) : this(new List<DiagnosticDefinition> { definition }, script) { }
            public FakeRepository(List<DiagnosticDefinition> definitions, string script) { this.definitions = definitions; this.script = script; }
            public Task<IReadOnlyList<DiagnosticDefinition>> GetAllAsync(CancellationToken cancellationToken) { return Task.FromResult((IReadOnlyList<DiagnosticDefinition>)definitions.AsReadOnly()); }
            public Task<string> LoadScriptAsync(DiagnosticDefinition ignored, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(script); }
        }

        private sealed class FakeSqlServerService : ISqlServerService
        {
            private int concurrentExecutions;
            public SqlQueryResult Result { get; } = new SqlQueryResult(); public int ExecutionCount { get; private set; } public string LastCommandText { get; private set; }
            public Exception ExceptionToThrow { get; set; }
            public int MaximumConcurrentExecutions { get; private set; }
            public Task<SqlQueryResult> ExecuteQueryWithMultipleResultsAsync(DatabaseConnection connection, string commandText, IReadOnlyCollection<SqlQueryParameter> parameters, int timeout, int maximumRows, CancellationToken token) { token.ThrowIfCancellationRequested(); concurrentExecutions++; MaximumConcurrentExecutions = Math.Max(MaximumConcurrentExecutions, concurrentExecutions); try { ExecutionCount++; LastCommandText = commandText; if (ExceptionToThrow != null) throw ExceptionToThrow; return Task.FromResult(Result); } finally { concurrentExecutions--; } }
            public Task<SqlQueryResult> ExecuteQueryAsync(DatabaseConnection c, string s, IReadOnlyCollection<SqlQueryParameter> p, int t, int m, CancellationToken ct) { return ExecuteQueryWithMultipleResultsAsync(c, s, p, t, m, ct); }
            public Task<object> ExecuteScalarAsync(DatabaseConnection c, string s, IReadOnlyCollection<SqlQueryParameter> p, int t, CancellationToken ct) { throw new NotSupportedException(); }
            public Task<int> ExecuteNonQueryAsync(DatabaseConnection c, string s, IReadOnlyCollection<SqlQueryParameter> p, int t, CancellationToken ct) { throw new NotSupportedException(); }
            public Task<SqlServerInfo> GetServerInfoAsync(DatabaseConnection c, CancellationToken ct) { throw new NotSupportedException(); }
            public Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(DatabaseConnection c, CancellationToken ct) { throw new NotSupportedException(); }
            public Task<DatabaseInfo> GetDatabaseInfoAsync(DatabaseConnection c, CancellationToken ct) { throw new NotSupportedException(); }
        }

        private sealed class NullLogger : IApplicationLogger { public void Log(LogEvent logEvent, Exception exception = null) { } }
        private sealed class TemporaryDirectory : IDisposable { public TemporaryDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlServerDiagnostics-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); } public string Path { get; private set; } public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); } }
    }
}