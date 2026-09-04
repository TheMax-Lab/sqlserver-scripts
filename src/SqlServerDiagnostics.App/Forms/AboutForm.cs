using System;
using System.Drawing;
using System.Windows.Forms;
using TheMaxLab.SqlServerDiagnostics.App.Presentation;

namespace TheMaxLab.SqlServerDiagnostics.App.Forms
{
    public sealed class AboutForm : Form
    {
        private readonly IExternalLinkLauncher linkLauncher;

        public AboutForm(IExternalLinkLauncher linkLauncher)
        {
            this.linkLauncher = linkLauncher ?? throw new ArgumentNullException("linkLauncher");
            Text = "About " + AboutPresentation.ApplicationName;
            Name = "aboutForm";
            AccessibleName = "About SQL Server Diagnostics";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(620, 560);
            Font = new Font("Segoe UI", 9F);

            var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 18), ColumnCount = 1, RowCount = 12 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int index = 0; index < content.RowCount; index++) content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            content.Controls.Add(CreateLabel("applicationNameLabel", AboutPresentation.ApplicationName, 19F, FontStyle.Bold, Color.FromArgb(31, 45, 61)), 0, 0);
            content.Controls.Add(CreateParagraph("descriptionLabel", AboutPresentation.Description), 0, 1);
            content.Controls.Add(CreateParagraph("detailLabel", AboutPresentation.Detail), 0, 2);

            var metadata = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 14, 0, 10) };
            metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115F));
            metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            AddMetadata(metadata, 0, "Author", AboutPresentation.Author, "authorValueLabel");
            AddMetadata(metadata, 1, "Version", AboutPresentation.Version, "versionValueLabel");
            AddMetadata(metadata, 2, "Open Source", AboutPresentation.RepositoryName, "repositoryValueLabel");
            AddMetadata(metadata, 3, "License", AboutPresentation.License, "licenseValueLabel");
            content.Controls.Add(metadata, 0, 3);

            content.Controls.Add(CreateSectionLabel("Open Source"), 0, 4);
            var repositoryButton = new Button { Name = "githubRepositoryButton", AccessibleName = "Open GitHub repository", Text = "GitHub Repository", AutoSize = true, MinimumSize = new Size(175, 34), Anchor = AnchorStyles.Left, TabIndex = 0 };
            repositoryButton.Click += delegate { OpenLink(AboutPresentation.RepositoryUrl); };
            content.Controls.Add(repositoryButton, 0, 5);

            content.Controls.Add(CreateSectionLabel("Support the Project"), 0, 6);
            content.Controls.Add(CreateParagraph("donationDescriptionLabel", "If you find the project useful, you may optionally support its continued development with a voluntary donation."), 0, 7);
            var donationButton = new Button { Name = "donatePayPalButton", AccessibleName = "Donate via PayPal", Text = "Donate via PayPal", AutoSize = true, MinimumSize = new Size(175, 34), Anchor = AnchorStyles.Left, TabIndex = 1 };
            donationButton.Click += delegate { OpenLink(AboutPresentation.DonationUrl); };
            content.Controls.Add(donationButton, 0, 8);

            var spacer = new Panel { Dock = DockStyle.Fill, MinimumSize = new Size(1, 12) };
            content.Controls.Add(spacer, 0, 9);
            content.RowStyles[9] = new RowStyle(SizeType.Percent, 100F);
            var okButton = new Button { Name = "okButton", AccessibleName = "Close About dialog", Text = "OK", DialogResult = DialogResult.OK, Size = new Size(90, 32), Anchor = AnchorStyles.Right, TabIndex = 2 };
            content.Controls.Add(okButton, 0, 10);

            Controls.Add(content);
            AcceptButton = okButton;
            CancelButton = okButton;
        }

        private void OpenLink(string url)
        {
            if (!linkLauncher.TryOpen(url)) MessageBox.Show(this, "Unable to open the selected web page. Please open the link manually in your browser.", "Open web page", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static Label CreateLabel(string name, string text, float size, FontStyle style, Color color)
        {
            return new Label { Name = name, AccessibleName = text, Text = text, AutoSize = true, Font = new Font("Segoe UI", size, style), ForeColor = color, Margin = new Padding(0, 0, 0, 14) };
        }

        private static Label CreateParagraph(string name, string text)
        {
            return new Label { Name = name, Text = text, AutoSize = true, MaximumSize = new Size(555, 0), Margin = new Padding(0, 0, 0, 12) };
        }

        private static Label CreateSectionLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(0, 10, 0, 6) };
        }

        private static void AddMetadata(TableLayoutPanel table, int row, string name, string value, string valueControlName)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = name, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, 3, 8, 3) }, 0, row);
            table.Controls.Add(new Label { Name = valueControlName, Text = value, AutoSize = true, Margin = new Padding(0, 3, 0, 3) }, 1, row);
        }
    }
}