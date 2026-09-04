using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TheMaxLab.SqlServerDiagnostics.App.Forms;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Controls
{
    public sealed class HealthDashboardControl : UserControl
    {
        private readonly Label score = new Label(), grade = new Label(), context = new Label(), metrics = new Label(), assessment = new Label();
        private readonly ComboBox severity = new ComboBox(), category = new ComboBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly RichTextBox details = new RichTextBox();
        private readonly FlowLayoutPanel coverage = new FlowLayoutPanel();
        private readonly Button explain = new Button();
        private readonly DataGridView unavailableGrid = new DataGridView();
        private readonly RichTextBox unavailableDetails = new RichTextBox();
        private HealthReport report;

        public HealthDashboardControl()
        {
            Dock = DockStyle.Fill; BackColor = Color.White; Padding = new Padding(16); AccessibleName = "SQL Server Health Dashboard";
            var header = new Panel { Dock = DockStyle.Top, Height = 164, BackColor = Color.FromArgb(245, 248, 251) };
            score.SetBounds(18, 10, 190, 58); score.Font = new Font("Segoe UI", 30F, FontStyle.Bold);
            grade.SetBounds(22, 70, 180, 28); grade.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            context.SetBounds(220, 16, 700, 28); context.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            metrics.SetBounds(220, 48, 720, 48); metrics.ForeColor = Color.FromArgb(55, 65, 75);
            assessment.SetBounds(18, 112, 1068, 38); assessment.Padding = new Padding(10, 9, 10, 6); assessment.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            explain.SetBounds(950, 20, 135, 34); explain.Text = "Why this score?"; explain.Click += ExplainClick;
            header.Controls.AddRange(new Control[] { score, grade, context, metrics, assessment, explain });
            var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(0, 8, 0, 4) };
            filters.Controls.Add(new Label { Text = "Severity", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            severity.DropDownStyle = ComboBoxStyle.DropDownList; severity.Width = 130; severity.Items.AddRange(new object[] { "All", "Critical", "Warning", "Information", "Passed" }); severity.SelectedIndex = 0; severity.SelectedIndexChanged += FilterChanged; filters.Controls.Add(severity);
            filters.Controls.Add(new Label { Text = "Category", AutoSize = true, Margin = new Padding(16, 6, 6, 0) });
            category.DropDownStyle = ComboBoxStyle.DropDownList; category.Width = 130; category.Items.AddRange(new object[] { "All", "Performance", "Indexes", "Integrity", "Schema" }); category.SelectedIndex = 0; category.SelectedIndexChanged += FilterChanged; filters.Controls.Add(category);
            coverage.Dock = DockStyle.Top; coverage.Height = 58; coverage.WrapContents = false;
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 690 };
            grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns.Add("Severity", "Severity"); grid.Columns.Add("Impact", "Impact"); grid.Columns.Add("Diagnostic", "Diagnostic"); grid.Columns.Add("Finding", "Finding"); grid.SelectionChanged += FindingSelected;
            details.Dock = DockStyle.Fill; details.ReadOnly = true; details.BackColor = Color.White; details.BorderStyle = BorderStyle.None;
            split.Panel1.Controls.Add(grid); split.Panel2.Controls.Add(details);
            var findingsPage = new TabPage("Findings"); findingsPage.Controls.Add(split); var unavailablePage = new TabPage("Diagnostics requiring attention");
            var unavailableSplit = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 690 }; unavailableGrid.Dock = DockStyle.Fill; unavailableGrid.ReadOnly = true; unavailableGrid.AllowUserToAddRows = false; unavailableGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; unavailableGrid.MultiSelect = false; unavailableGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; unavailableGrid.Columns.Add("Diagnostic", "Diagnostic"); unavailableGrid.Columns.Add("Status", "Status"); unavailableGrid.Columns.Add("Failure", "Failure kind"); unavailableGrid.SelectionChanged += UnavailableSelected; unavailableDetails.Dock = DockStyle.Fill; unavailableDetails.ReadOnly = true; unavailableDetails.BackColor = Color.White; unavailableDetails.BorderStyle = BorderStyle.None; unavailableSplit.Panel1.Controls.Add(unavailableGrid); unavailableSplit.Panel2.Controls.Add(unavailableDetails); unavailablePage.Controls.Add(unavailableSplit);
            var tabs = new TabControl { Dock = DockStyle.Fill }; tabs.TabPages.Add(findingsPage); tabs.TabPages.Add(unavailablePage); Controls.Add(tabs); Controls.Add(coverage); Controls.Add(filters); Controls.Add(header); ClearReport();
        }

        public void ClearReport() { report = null; score.Text = "—"; grade.Text = "NO ASSESSMENT"; context.Text = string.Empty; metrics.Text = "Run Health Check to generate a semantic health report."; assessment.Text = "No assessment is available."; assessment.BackColor = Color.FromArgb(235, 240, 245); grid.Rows.Clear(); details.Clear(); unavailableGrid.Rows.Clear(); unavailableDetails.Clear(); coverage.Controls.Clear(); explain.Enabled = false; }

        public void ShowReport(HealthReport value)
        {
            report = value; if (value == null || value.HealthScore == null) { ClearReport(); return; }
            score.Text = value.HealthScore.Percentage.ToString("0") + " / 100"; grade.Text = value.HealthScore.Grade.ToUpperInvariant();
            context.Text = string.Format("{0} / {1} · {2:g}", value.Server == null ? "Unknown server" : value.Server.ServerName, value.Database == null ? "Unknown database" : value.Database.Name, value.GeneratedAt.LocalDateTime);
            metrics.Text = string.Format("Health Score: based on {0} evaluated logical groups\r\nCoverage: {1:0.#}%     Confidence: {2}     Critical: {3}     Warning: {4}     Information: {5}", value.HealthScore.LogicalGroupsEvaluated, value.Coverage.CoveragePercentage, value.HealthScore.Confidence, value.HealthScore.CriticalFindings, value.HealthScore.WarningFindings, value.HealthScore.InformationFindings);
            assessment.Text = value.AssessmentStatus + " — " + value.AssessmentMessage; assessment.BackColor = value.AssessmentStatus == AssessmentStatus.Complete ? Color.FromArgb(224, 242, 226) : Color.FromArgb(255, 244, 214);
            coverage.Controls.Clear(); foreach (DiagnosticCategory c in new[] { DiagnosticCategory.Performance, DiagnosticCategory.Indexes, DiagnosticCategory.Integrity, DiagnosticCategory.Schema }) { var rows = value.Results.Where(x => x.Category == c).ToList(); coverage.Controls.Add(new Label { Text = c + ": " + rows.Count(x => x.Status == DiagnosticExecutionStatus.Succeeded) + "/" + rows.Count + " successful", Width = 220, Height = 36, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(235, 240, 245), Margin = new Padding(0, 8, 8, 0) }); }
            unavailableGrid.Rows.Clear(); foreach (DiagnosticResult result in value.Results.Where(x => x.Status != DiagnosticExecutionStatus.Succeeded).OrderBy(x => x.DiagnosticName, StringComparer.OrdinalIgnoreCase)) { int row = unavailableGrid.Rows.Add(result.DiagnosticName, result.Status, result.FailureKind); unavailableGrid.Rows[row].Tag = result; } if (unavailableGrid.Rows.Count > 0) unavailableGrid.Rows[0].Selected = true;
            ApplyFilters(); explain.Enabled = true;
        }

        private void FilterChanged(object sender, EventArgs e) { ApplyFilters(); }
        private void ApplyFilters()
        {
            grid.Rows.Clear(); details.Clear(); if (report == null) return;
            var values = report.Interpretations.SelectMany(i => i.Findings.Select(f => new { I = i, F = f })); string s = Convert.ToString(severity.SelectedItem), c = Convert.ToString(category.SelectedItem);
            if (s != "All") values = values.Where(x => x.F.Severity.ToString() == s); if (c != "All") values = values.Where(x => x.I.Category.ToString() == c);
            foreach (var item in values.OrderByDescending(x => x.F.Severity).ThenBy(x => x.I.DiagnosticName)) { int row = grid.Rows.Add(item.F.Severity, item.F.Impact, item.I.DiagnosticName, item.F.Title); grid.Rows[row].Tag = item.F; }
            if (grid.Rows.Count > 0) grid.Rows[0].Selected = true;
        }

        private void FindingSelected(object sender, EventArgs e)
        {
            var f = grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].Tag as DiagnosticFinding; if (f == null) { details.Clear(); return; }
            var t = new StringBuilder().AppendLine("Severity: " + f.Severity).AppendLine("Impact: " + f.Impact).AppendLine("Confidence: " + f.Confidence).AppendLine("Diagnostic: " + f.DiagnosticId).AppendLine().AppendLine(f.Title).AppendLine();
            if (!string.IsNullOrWhiteSpace(f.Description)) t.AppendLine("Evidence").AppendLine(f.Description).AppendLine(); if (!string.IsNullOrWhiteSpace(f.Recommendation)) t.AppendLine("Recommendation").AppendLine(f.Recommendation).AppendLine();
            t.AppendLine(f.ScoreContribution == 0 ? "This finding is informational or deduplicated and does not affect the Health Score." : "Score contribution: " + f.ScoreContribution.ToString("0.##") + " logical units"); if (!string.IsNullOrWhiteSpace(f.SuggestedSql)) t.AppendLine().AppendLine("Suggested SQL (view/copy only; never executed)").AppendLine(f.SuggestedSql); details.Text = t.ToString();
        }
        private void UnavailableSelected(object sender, EventArgs e)
        {
            var result = unavailableGrid.SelectedRows.Count == 0 ? null : unavailableGrid.SelectedRows[0].Tag as DiagnosticResult; if (result == null) { unavailableDetails.Clear(); return; }
            var text = new StringBuilder().AppendLine(result.DiagnosticName).AppendLine().AppendLine("Status: " + result.Status).AppendLine("Failure kind: " + result.FailureKind).AppendLine("Message: " + (result.UserMessage ?? "No controlled message was provided.")); if (result.SqlErrorNumber.HasValue) text.AppendLine("SQL error number: " + result.SqlErrorNumber.Value); if (result.RequiredPermissions.Count > 0) text.AppendLine().AppendLine("Required permissions:").AppendLine(string.Join(Environment.NewLine, result.RequiredPermissions)); unavailableDetails.Text = text.ToString();
        }
        private void ExplainClick(object sender, EventArgs e) { if (report != null) using (var dialog = new ScoreExplanationForm(report)) dialog.ShowDialog(this); }
    }
}