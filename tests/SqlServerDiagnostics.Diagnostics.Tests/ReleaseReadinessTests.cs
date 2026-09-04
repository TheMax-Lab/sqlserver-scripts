using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Tests
{
    [TestClass]
    public sealed class ReleaseReadinessTests
    {
        [TestMethod]
        public void ApplicationStartup_CreatesMainWindowWithoutUnhandledException()
        {
            string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SqlServerDiagnostics.exe");
            Assert.IsTrue(File.Exists(executable), "The application executable was not copied to the test output.");
            using (Process process = Process.Start(new ProcessStartInfo { FileName = executable, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory, UseShellExecute = false }))
            {
                try
                {
                    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                    while (!process.HasExited && process.MainWindowHandle == IntPtr.Zero && DateTime.UtcNow < deadline)
                    {
                        System.Threading.Thread.Sleep(100);
                        process.Refresh();
                    }
                    Assert.IsFalse(process.HasExited, "The application exited during startup with code " + (process.HasExited ? process.ExitCode.ToString() : "unknown") + ".");
                    Assert.AreNotEqual(IntPtr.Zero, process.MainWindowHandle, "The application did not create its main window.");
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                        if (!process.WaitForExit(5000)) { process.Kill(); process.WaitForExit(); }
                    }
                }
            }
        }

        [TestMethod]
        public void PortablePackage_ContainsOnlyValidatedRuntimeFiles()
        {
            string root = FindRepositoryRoot();
            string destination = Path.Combine(Path.GetTempPath(), "SqlServerDiagnostics-PackageTest-" + Guid.NewGuid().ToString("N"));
            try
            {
                string script = Path.Combine(root, "release", "New-PortablePackage.ps1");
                var start = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -SkipBuild -Destination \"" + destination + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process process = Process.Start(start))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    Assert.AreEqual(0, process.ExitCode, output + error);
                }

                Assert.IsTrue(File.Exists(Path.Combine(destination, "SqlServerDiagnostics.exe")));
                Assert.IsTrue(File.Exists(Path.Combine(destination, "SqlServerDiagnostics.exe.config")));
                Assert.IsTrue(File.Exists(Path.Combine(destination, "diagnostics", "manifest.json")));
                Assert.AreEqual(26, Directory.GetFiles(Path.Combine(destination, "diagnostics"), "*.sql", SearchOption.AllDirectories).Length);
                string[] forbidden = Directory.GetFiles(destination, "*", SearchOption.AllDirectories)
                    .Where(path => new[] { ".pdb", ".cs", ".csproj", ".trx", ".log", ".credential" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    .ToArray();
                Assert.AreEqual(0, forbidden.Length, string.Join(Environment.NewLine, forbidden));
            }
            finally
            {
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
            }
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "SqlServerDiagnostics.sln"))) directory = directory.Parent;
            if (directory == null) Assert.Inconclusive("Repository root was not found.");
            return directory.FullName;
        }
    }
}