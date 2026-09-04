using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TheMaxLab.SqlServerDiagnostics.App.Forms
{
    public sealed class SqlViewerForm : Form
    {
        private readonly RichTextBox editor;

        public SqlViewerForm(string title, string sourcePath, string sql)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(640, 420); ClientSize = new Size(900, 650); Font = new Font("Segoe UI", 9F);
            var source = new Label { Dock = DockStyle.Top, Height = 34, Padding = new Padding(10, 8, 10, 4), Text = "Source: " + sourcePath, AutoEllipsis = true };
            var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
            toolbar.Items.Add("Copy", null, delegate { if (!string.IsNullOrEmpty(editor.Text)) Clipboard.SetText(editor.Text); });
            toolbar.Items.Add("Select All", null, delegate { editor.SelectAll(); editor.Focus(); });
            toolbar.Items.Add("Save...", null, delegate { SaveSql(); });
            editor = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, WordWrap = false, DetectUrls = false, BackColor = Color.FromArgb(248, 249, 250), Font = new Font("Consolas", 10F), Text = sql ?? string.Empty };
            Controls.Add(editor); Controls.Add(toolbar); Controls.Add(source);
        }

        private void SaveSql()
        {
            using (var dialog = new SaveFileDialog { Filter = "SQL files (*.sql)|*.sql|All files (*.*)|*.*", DefaultExt = "sql", AddExtension = true }) if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, editor.Text, new UTF8Encoding(true));
        }
    }
}