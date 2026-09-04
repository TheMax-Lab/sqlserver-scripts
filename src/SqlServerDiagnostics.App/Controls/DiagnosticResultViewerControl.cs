using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TheMaxLab.SqlServerDiagnostics.App.Forms;
using TheMaxLab.SqlServerDiagnostics.App.Presentation;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Controls
{
    public sealed class DiagnosticResultViewerControl : UserControl
    {
        private readonly Label statusLabel = new Label();
        private readonly Label messageLabel = new Label();
        private readonly TabControl tabs = new TabControl();
        private readonly DiagnosticResultPresentation presentation;
        private DiagnosticResult currentResult;

        public DiagnosticResultViewerControl(DiagnosticResultPresentation presentation)
        {
            this.presentation = presentation ?? throw new ArgumentNullException("presentation");
            Dock = DockStyle.Fill; BackColor = Color.White;
            var header = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(12, 8, 12, 4) };
            statusLabel.Dock = DockStyle.Top; statusLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold); statusLabel.Height = 25; statusLabel.Text = "No results";
            messageLabel.Dock = DockStyle.Fill; messageLabel.AutoEllipsis = true; messageLabel.ForeColor = Color.DimGray;
            header.Controls.Add(messageLabel); header.Controls.Add(statusLabel);
            tabs.Dock = DockStyle.Fill; tabs.Name = "resultSetsTabControl";
            Controls.Add(tabs); Controls.Add(header);
        }

        public void ClearResults() { currentResult = null; tabs.TabPages.Clear(); statusLabel.Text = "No results"; messageLabel.Text = string.Empty; }

        public void ShowResult(DiagnosticResult result)
        {
            currentResult = result; tabs.TabPages.Clear();
            if (result == null) { ClearResults(); return; }
            statusLabel.Text = string.Format("{0} — {1} — {2:0.00}s", result.DiagnosticName, presentation.GetDisplayStatus(result), result.Duration.TotalSeconds);
            messageLabel.Text = BuildMessage(result);
            if (result.Findings.Count > 0) tabs.TabPages.Add(CreateFindingsTab(result));
            foreach (DiagnosticResultSet set in result.ResultSets) tabs.TabPages.Add(CreateResultSetTab(set));
            if (tabs.TabPages.Count == 0) tabs.TabPages.Add(new TabPage("Details") { Controls = { new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = "No result rows were returned. This is not automatically interpreted as Passed." } } });
        }

        private TabPage CreateFindingsTab(DiagnosticResult result)
        {
            var page = new TabPage(string.Format("Findings ({0})", result.Findings.Count));
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 290 };
            var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            foreach (DiagnosticFinding finding in result.Findings) list.Items.Add(new FindingItem(finding));
            var details = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9F) };
            var copySql = new Button { Dock = DockStyle.Bottom, Height = 32, Text = "Copy Suggested SQL", Enabled = false };
            var viewSql = new Button { Dock = DockStyle.Bottom, Height = 32, Text = "View Suggested SQL", Enabled = false };
            list.SelectedIndexChanged += delegate
            {
                var item = list.SelectedItem as FindingItem; if (item == null) return;
                details.Text = FormatFinding(item.Finding); bool hasSql = !string.IsNullOrWhiteSpace(item.Finding.SuggestedSql); copySql.Enabled = hasSql; viewSql.Enabled = hasSql;
            };
            copySql.Click += delegate { var item = list.SelectedItem as FindingItem; if (item != null && !string.IsNullOrWhiteSpace(item.Finding.SuggestedSql)) Clipboard.SetText(item.Finding.SuggestedSql); };
            viewSql.Click += delegate { var item = list.SelectedItem as FindingItem; if (item != null) using (var viewer = new SqlViewerForm("Suggested SQL — review only", "Generated result data; this window cannot execute SQL.", item.Finding.SuggestedSql)) viewer.ShowDialog(this); };
            split.Panel1.Controls.Add(list); split.Panel2.Controls.Add(details); split.Panel2.Controls.Add(viewSql); split.Panel2.Controls.Add(copySql); page.Controls.Add(split);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            return page;
        }

        private TabPage CreateResultSetTab(DiagnosticResultSet set)
        {
            var page = new TabPage(presentation.GetResultSetTitle(set));
            var grid = CreateGrid(set);
            var banner = new Label { Dock = DockStyle.Top, Height = set.IsTruncated ? 36 : 25, Padding = new Padding(8, 5, 8, 4), Text = set.IsTruncated ? presentation.GetTruncationMessage(set) : string.Format("{0:N0} rows", set.RowCount), BackColor = set.IsTruncated ? Color.FromArgb(255, 244, 204) : Color.FromArgb(240, 244, 248) };
            page.Controls.Add(grid); page.Controls.Add(banner); return page;
        }

        private DataGridView CreateGrid(DiagnosticResultSet set)
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText, SelectionMode = DataGridViewSelectionMode.CellSelect };
            foreach (DiagnosticColumn column in set.Columns) grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "column" + column.Ordinal, HeaderText = column.Name, ValueType = column.DataType, SortMode = DataGridViewColumnSortMode.Automatic });
            foreach (IReadOnlyDictionary<string, object> row in set.Rows) grid.Rows.Add(set.Columns.Select(column => GetValue(row, column)).ToArray());
            var menu = new ContextMenuStrip();
            menu.Items.Add("Copy Cell", null, delegate { CopyCurrentCell(grid); }); menu.Items.Add("Copy Row", null, delegate { CopyCurrentRow(grid); }); menu.Items.Add("Copy Selected Rows", null, delegate { CopySelectedRows(grid); }); menu.Items.Add("Copy Table", null, delegate { Clipboard.SetText(ToDelimited(set, '\t')); }); menu.Items.Add("Save CSV...", null, delegate { SaveCsv(set); });
            grid.ContextMenuStrip = menu; return grid;
        }

        private static object GetValue(IReadOnlyDictionary<string, object> row, DiagnosticColumn column)
        {
            object value; string key = string.IsNullOrWhiteSpace(column.Key) ? column.Name : column.Key;
            return row.TryGetValue(key, out value) ? value : null;
        }

        private static string BuildMessage(DiagnosticResult result)
        {
            var text = new StringBuilder(result.UserMessage ?? string.Empty);
            if (result.RequiredPermissions.Count > 0 && !result.Success) text.Append(" Required permissions: ").Append(string.Join("; ", result.RequiredPermissions));
            if (result.FailureKind != Core.Enums.DiagnosticFailureKind.None) text.Append(" Technical details: ").Append(result.FailureKind);
            if (result.SqlErrorNumber.HasValue) text.Append(" (SQL error ").Append(result.SqlErrorNumber.Value).Append(')');
            return text.ToString();
        }

        private static string FormatFinding(DiagnosticFinding finding)
        {
            var text = new StringBuilder(); text.Append('[').Append(finding.Severity).Append("] ").AppendLine(finding.Title).AppendLine();
            if (!string.IsNullOrWhiteSpace(finding.Description)) text.AppendLine("Evidence").AppendLine(finding.Description).AppendLine();
            if (!string.IsNullOrWhiteSpace(finding.Recommendation)) text.AppendLine("Recommendation").AppendLine(finding.Recommendation).AppendLine();
            if (!string.IsNullOrWhiteSpace(finding.SuggestedSql)) text.AppendLine("Suggested SQL (review only — never executed automatically)").AppendLine(finding.SuggestedSql);
            return text.ToString();
        }

        private static void CopyCurrentCell(DataGridView grid) { if (grid.CurrentCell != null && grid.CurrentCell.Value != null) Clipboard.SetText(Convert.ToString(grid.CurrentCell.Value, CultureInfo.CurrentCulture)); }
        private static void CopyCurrentRow(DataGridView grid) { if (grid.CurrentRow != null) Clipboard.SetText(string.Join("\t", grid.CurrentRow.Cells.Cast<DataGridViewCell>().Select(cell => Convert.ToString(cell.Value, CultureInfo.CurrentCulture)))); }
        private static void CopySelectedRows(DataGridView grid)
        {
            var rows = grid.SelectedCells.Cast<DataGridViewCell>().Select(cell => cell.OwningRow).Distinct().OrderBy(row => row.Index).ToList();
            if (rows.Count == 0 && grid.CurrentRow != null) rows.Add(grid.CurrentRow);
            if (rows.Count > 0) Clipboard.SetText(string.Join(Environment.NewLine, rows.Select(row => string.Join("\t", row.Cells.Cast<DataGridViewCell>().Select(cell => Convert.ToString(cell.Value, CultureInfo.CurrentCulture))))));
        }

        private static void SaveCsv(DiagnosticResultSet set)
        {
            using (var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", DefaultExt = "csv", AddExtension = true })
            {
                if (dialog.ShowDialog() == DialogResult.OK) File.WriteAllText(dialog.FileName, ToDelimited(set, ','), new UTF8Encoding(true));
            }
        }

        private static string ToDelimited(DiagnosticResultSet set, char delimiter)
        {
            var text = new StringBuilder(); text.AppendLine(string.Join(delimiter.ToString(), set.Columns.Select(column => Escape(column.Name, delimiter))));
            foreach (IReadOnlyDictionary<string, object> row in set.Rows) text.AppendLine(string.Join(delimiter.ToString(), set.Columns.Select(column => Escape(Convert.ToString(GetValue(row, column), CultureInfo.CurrentCulture), delimiter))));
            return text.ToString();
        }

        private static string Escape(string value, char delimiter) { string text = value ?? string.Empty; return text.IndexOfAny(new[] { delimiter, '"', '\r', '\n' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text; }
        private sealed class FindingItem { public FindingItem(DiagnosticFinding finding) { Finding = finding; } public DiagnosticFinding Finding { get; private set; } public override string ToString() { return "[" + Finding.Severity + "] " + Finding.Title; } }
    }
}