using System;
using System.Diagnostics;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Presentation
{
    public interface IExternalLinkLauncher
    {
        bool TryOpen(string url);
    }

    public sealed class ExternalLinkLauncher : IExternalLinkLauncher
    {
        private readonly IApplicationLogger logger;

        public ExternalLinkLauncher(IApplicationLogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException("logger");
        }

        public bool TryOpen(string url)
        {
            try
            {
                Uri destination;
                if (!Uri.TryCreate(url, UriKind.Absolute, out destination) || destination.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("Only HTTPS web links are supported.");
                Process.Start(new ProcessStartInfo { FileName = destination.AbsoluteUri, UseShellExecute = true });
                return true;
            }
            catch (Exception exception)
            {
                try { logger.Log(new LogEvent { Level = LogLevel.Warning, EventName = "application.external-link-failed", Message = "An external web page could not be opened." }, exception); }
                catch { }
                return false;
            }
        }
    }
}