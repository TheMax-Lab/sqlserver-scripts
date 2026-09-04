using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TheMaxLab.SqlServerDiagnostics.App.Presentation;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Controls
{
    public sealed class DiagnosticExplorerControl : UserControl
    {
        private readonly TextBox searchTextBox = new TextBox();
        private readonly ComboBox categoryComboBox = new ComboBox();
        private readonly ListView diagnosticsListView = new ListView();
        private readonly DiagnosticExplorerPresentation presentation;
        private IReadOnlyList<DiagnosticDefinition> diagnostics = new List<DiagnosticDefinition>().AsReadOnly();

        public DiagnosticExplorerControl(DiagnosticExplorerPresentation presentation)
        {
            this.presentation = presentation ?? throw new ArgumentNullException("presentation");
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var header = new Panel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(12, 10, 12, 8) };
            searchTextBox.Dock = DockStyle.Top; searchTextBox.Name = "diagnosticSearchTextBox"; searchTextBox.AccessibleName = "Search diagnostics"; searchTextBox.TabIndex = 0; searchTextBox.TextChanged += FilterChanged;
            categoryComboBox.Dock = DockStyle.Bottom; categoryComboBox.Name = "categoryFilterComboBox"; categoryComboBox.DropDownStyle = ComboBoxStyle.DropDownList; categoryComboBox.TabIndex = 1; categoryComboBox.SelectedIndexChanged += FilterChanged;
            header.Controls.Add(searchTextBox); header.Controls.Add(categoryComboBox);

            diagnosticsListView.Dock = DockStyle.Fill; diagnosticsListView.Name = "diagnosticsListView"; diagnosticsListView.View = View.Details; diagnosticsListView.FullRowSelect = true; diagnosticsListView.HideSelection = false; diagnosticsListView.MultiSelect = false; diagnosticsListView.BorderStyle = BorderStyle.None; diagnosticsListView.TabIndex = 2;
            diagnosticsListView.Columns.Add("Diagnostic", 220); diagnosticsListView.Columns.Add("Cost", 70); diagnosticsListView.Columns.Add("Scope", 75); diagnosticsListView.Columns.Add("Health", 60);
            diagnosticsListView.SelectedIndexChanged += SelectionChanged;
            Controls.Add(diagnosticsListView); Controls.Add(header);
        }

        public event EventHandler SelectedDiagnosticChanged;
        public DiagnosticDefinition SelectedDiagnostic { get { return diagnosticsListView.SelectedItems.Count == 0 ? null : diagnosticsListView.SelectedItems[0].Tag as DiagnosticDefinition; } }

        public void SetDiagnostics(IReadOnlyList<DiagnosticDefinition> value)
        {
            diagnostics = value ?? new List<DiagnosticDefinition>().AsReadOnly();
            PopulateCategories();
            ApplyFilter();
        }

        public void SelectCategory(DiagnosticCategory? category)
        {
            for (int index = 0; index < categoryComboBox.Items.Count; index++)
            {
                var item = categoryComboBox.Items[index] as CategoryItem;
                if (item != null && item.Category == category) { categoryComboBox.SelectedIndex = index; return; }
            }
        }

        private void PopulateCategories()
        {
            var counts = presentation.GetCategoryCounts(diagnostics);
            categoryComboBox.BeginUpdate(); categoryComboBox.Items.Clear();
            categoryComboBox.Items.Add(new CategoryItem(null, "All", diagnostics.Count));
            foreach (DiagnosticCategory category in Enum.GetValues(typeof(DiagnosticCategory))) categoryComboBox.Items.Add(new CategoryItem(category, category.ToString(), counts[category]));
            categoryComboBox.SelectedIndex = 0; categoryComboBox.EndUpdate();
        }

        private void FilterChanged(object sender, EventArgs e) { ApplyFilter(); }
        private void ApplyFilter()
        {
            DiagnosticDefinition selected = SelectedDiagnostic;
            var categoryItem = categoryComboBox.SelectedItem as CategoryItem;
            var filtered = presentation.Filter(diagnostics, categoryItem == null ? null : categoryItem.Category, searchTextBox.Text);
            diagnosticsListView.BeginUpdate(); diagnosticsListView.Items.Clear();
            foreach (DiagnosticDefinition definition in filtered)
            {
                var item = new ListViewItem(definition.Name) { Tag = definition, ToolTipText = definition.Description };
                item.SubItems.Add(definition.ExecutionCost.ToString()); item.SubItems.Add(definition.ExecutionScope.ToString()); item.SubItems.Add(definition.HealthCheckEnabled ? "Yes" : "No");
                diagnosticsListView.Items.Add(item);
                if (selected != null && selected.Id == definition.Id) item.Selected = true;
            }
            diagnosticsListView.EndUpdate();
            if (diagnosticsListView.SelectedItems.Count == 0 && diagnosticsListView.Items.Count > 0) diagnosticsListView.Items[0].Selected = true;
        }

        private void SelectionChanged(object sender, EventArgs e) { if (SelectedDiagnosticChanged != null) SelectedDiagnosticChanged(this, EventArgs.Empty); }

        private sealed class CategoryItem
        {
            public CategoryItem(DiagnosticCategory? category, string name, int count) { Category = category; Name = name; Count = count; }
            public DiagnosticCategory? Category { get; private set; }
            public string Name { get; private set; }
            public int Count { get; private set; }
            public override string ToString() { return string.Format("{0} ({1})", Name, Count); }
        }
    }
}