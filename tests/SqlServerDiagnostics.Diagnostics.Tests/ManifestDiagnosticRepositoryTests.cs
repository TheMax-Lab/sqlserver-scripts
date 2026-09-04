using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Repositories;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Tests
{
    [TestClass]
    public sealed class ManifestDiagnosticRepositoryTests
    {
        [TestMethod]
        public void GetAllAsync_LoadsEmptyManifestWithoutLiveSqlServer()
        {
            using (var directory = new TemporaryDirectory())
            {
                File.WriteAllText(Path.Combine(directory.Path, "manifest.json"), "{\"schemaVersion\":1,\"diagnostics\":[]}");
                var repository = new ManifestDiagnosticRepository(directory.Path);

                var definitions = repository.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult();

                Assert.AreEqual(0, definitions.Count);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidDataException))]
        public void LoadScriptAsync_RejectsPathOutsideDiagnosticsRoot()
        {
            using (var directory = new TemporaryDirectory())
            {
                var repository = new ManifestDiagnosticRepository(directory.Path);
                var definition = new DiagnosticDefinition { Id = "unsafe", Name = "Unsafe", ScriptPath = "..\\unsafe.sql", ReadOnly = true, ExecutionCost = DiagnosticExecutionCost.Low };
                repository.LoadScriptAsync(definition, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlServerDiagnostics-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; private set; }
            public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
        }
    }
}