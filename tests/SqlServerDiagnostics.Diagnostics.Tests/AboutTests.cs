using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TheMaxLab.SqlServerDiagnostics.App.Forms;
using TheMaxLab.SqlServerDiagnostics.App.Presentation;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Tests
{
    [TestClass]
    public sealed class AboutTests
    {
        [TestMethod]
        public void AboutMetadata_UsesAuthoritativeVersionAndExactProjectLinks()
        {
            Assert.AreEqual(ApplicationInfo.ProductName, AboutPresentation.ApplicationName);
            Assert.AreEqual(ApplicationInfo.ApplicationVersion, AboutPresentation.Version);
            Assert.AreEqual("TheMax-Lab", AboutPresentation.Author);
            Assert.AreEqual("https://github.com/TheMax-Lab/sqlserver-scripts", AboutPresentation.RepositoryUrl);
            Assert.AreEqual("https://paypal.me/TheMaxLab", AboutPresentation.DonationUrl);
            Assert.AreEqual("MIT License", AboutPresentation.License);
            StringAssert.Contains(AboutPresentation.Description, "Windows desktop application");
            StringAssert.Contains(AboutPresentation.Detail, "read-only diagnostic queries");
        }

        [TestMethod]
        public void AboutDialog_ContainsIdentityLinksVersionAndKeyboardCloseBehavior()
        {
            RunSta(() =>
            {
                var launcher = new FakeLinkLauncher();
                using (var form = new AboutForm(launcher))
                {
                    form.Show();
                    Assert.AreEqual("About SQL Server Diagnostics", form.Text);
                    Assert.AreEqual(FormBorderStyle.FixedDialog, form.FormBorderStyle);
                    Assert.AreSame(form.AcceptButton, form.CancelButton);
                    Assert.AreEqual(ApplicationInfo.ApplicationVersion, FindControl<Label>(form, "versionValueLabel").Text);
                    Assert.AreEqual("TheMax-Lab", FindControl<Label>(form, "authorValueLabel").Text);
                    Button repository = FindControl<Button>(form, "githubRepositoryButton");
                    Button donation = FindControl<Button>(form, "donatePayPalButton");
                    Assert.AreEqual("GitHub Repository", repository.Text);
                    Assert.AreEqual("Donate via PayPal", donation.Text);
                    repository.PerformClick();
                    donation.PerformClick();
                    CollectionAssert.AreEqual(new[] { AboutPresentation.RepositoryUrl, AboutPresentation.DonationUrl }, launcher.Urls.ToArray());
                    FindControl<Button>(form, "okButton").PerformClick();
                    Assert.AreEqual(DialogResult.OK, form.DialogResult, "The OK button must complete the modal About dialog.");
                    form.Close();
                }
            });
        }

        [TestMethod]
        public void ExternalLinkLauncher_RejectsNonHttpsWithoutLaunchingAndLogsControlledFailure()
        {
            var logger = new RecordingLogger();
            var launcher = new ExternalLinkLauncher(logger);
            Assert.IsFalse(launcher.TryOpen("http://example.invalid"));
            Assert.AreEqual(1, logger.Events.Count);
            Assert.AreEqual("application.external-link-failed", logger.Events[0].EventName);
            Assert.AreEqual("An external web page could not be opened.", logger.Events[0].Message);
            Assert.AreEqual(typeof(InvalidOperationException), logger.ExceptionType);
        }

        [TestMethod]
        public void MainMenu_DeclaresAboutSqlServerDiagnosticsCommand()
        {
            string root = FindRepositoryRoot();
            string designer = File.ReadAllText(Path.Combine(root, "src", "SqlServerDiagnostics.App", "Forms", "MainForm.Designer.cs"));
            StringAssert.Contains(designer, "new System.Windows.Forms.ToolStripMenuItem(\"&Help\")");
            StringAssert.Contains(designer, "&About SQL Server Diagnostics...");
            StringAssert.Contains(designer, "aboutMenuItem.Click += AboutClick");
            Assert.IsFalse(designer.Contains("aboutMenuItem.ShortcutKeys"));
        }

        private static T FindControl<T>(Control parent, string name) where T : Control
        {
            T match = parent.Controls.Find(name, true).OfType<T>().SingleOrDefault();
            Assert.IsNotNull(match, "Control was not found: " + name);
            return match;
        }

        private static void RunSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "SqlServerDiagnostics.sln"))) directory = directory.Parent;
            if (directory == null) Assert.Inconclusive("Repository root was not found.");
            return directory.FullName;
        }

        private sealed class FakeLinkLauncher : IExternalLinkLauncher
        {
            public FakeLinkLauncher() { Urls = new List<string>(); }
            public IList<string> Urls { get; private set; }
            public bool TryOpen(string url) { Urls.Add(url); return true; }
        }

        private sealed class RecordingLogger : IApplicationLogger
        {
            public RecordingLogger() { Events = new List<LogEvent>(); }
            public IList<LogEvent> Events { get; private set; }
            public Type ExceptionType { get; private set; }
            public void Log(LogEvent logEvent, Exception exception = null) { Events.Add(logEvent); ExceptionType = exception == null ? null : exception.GetType(); }
        }
    }
}