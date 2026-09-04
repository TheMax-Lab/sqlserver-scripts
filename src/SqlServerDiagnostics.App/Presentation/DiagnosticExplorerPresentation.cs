using System;
using System.Collections.Generic;
using System.Linq;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Presentation
{
    public sealed class DiagnosticExplorerPresentation
    {
        public IReadOnlyList<DiagnosticDefinition> Filter(IEnumerable<DiagnosticDefinition> diagnostics, DiagnosticCategory? category, string searchText)
        {
            if (diagnostics == null) throw new ArgumentNullException("diagnostics");
            string search = (searchText ?? string.Empty).Trim();
            return diagnostics.Where(item =>
                (!category.HasValue || item.Category == category.Value) &&
                (search.Length == 0 || Contains(item.Name, search) || Contains(item.Description, search) || item.Tags.Any(tag => Contains(tag, search))))
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList().AsReadOnly();
        }

        public IReadOnlyDictionary<DiagnosticCategory, int> GetCategoryCounts(IEnumerable<DiagnosticDefinition> diagnostics)
        {
            if (diagnostics == null) throw new ArgumentNullException("diagnostics");
            return Enum.GetValues(typeof(DiagnosticCategory)).Cast<DiagnosticCategory>().ToDictionary(category => category, category => diagnostics.Count(item => item.Category == category));
        }

        private static bool Contains(string value, string search) { return !string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0; }
    }
}