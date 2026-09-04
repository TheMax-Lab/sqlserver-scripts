using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Reporting
{
    public sealed class HealthReportService : IReportService
    {
        public Task ExportFindingsCsvAsync(HealthReport report, ReportOptions options, string filePath, CancellationToken cancellationToken)
        {
            return ExportAsync(report, options, filePath, cancellationToken, BuildFindingsCsv);
        }

        public Task ExportDiagnosticsCsvAsync(HealthReport report, ReportOptions options, string filePath, CancellationToken cancellationToken)
        {
            return ExportAsync(report, options, filePath, cancellationToken, BuildDiagnosticsCsv);
        }

        public Task ExportJsonAsync(HealthReport report, ReportOptions options, string filePath, CancellationToken cancellationToken)
        {
            return ExportBytesAsync(report, options, filePath, cancellationToken, BuildJson);
        }

        public Task ExportHtmlAsync(HealthReport report, ReportOptions options, string filePath, CancellationToken cancellationToken)
        {
            return ExportAsync(report, options, filePath, cancellationToken, BuildHtml);
        }

        public string CreateSafeFileName(HealthReport report, string extension)
        {
            if (report == null) throw new ArgumentNullException("report");
            string suffix = string.IsNullOrWhiteSpace(extension) ? "html" : extension.Trim().TrimStart('.').ToLowerInvariant();
            if (!new[] { "html", "json", "csv" }.Contains(suffix)) throw new ArgumentException("The report extension is not supported.", "extension");
            return "SqlServerHealth_" + report.GeneratedAt.LocalDateTime.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "." + suffix;
        }

        private static Task ExportAsync(HealthReport report, ReportOptions options, string path, CancellationToken token, Func<HealthReport, ReportOptions, CancellationToken, string> builder)
        {
            return ExportBytesAsync(report, options, path, token, (r, o, t) => new UTF8Encoding(true).GetBytes(builder(r, o, t)));
        }

        private static Task ExportBytesAsync(HealthReport report, ReportOptions options, string path, CancellationToken token, Func<HealthReport, ReportOptions, CancellationToken, byte[]> builder)
        {
            Validate(report, options, path);
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                byte[] content = builder(report, options, token);
                token.ThrowIfCancellationRequested();
                WriteAtomically(path, content, token);
            }, token);
        }

        private static void Validate(HealthReport report, ReportOptions options, string path)
        {
            if (report == null) throw new ArgumentNullException("report");
            if (options == null) throw new ArgumentNullException("options");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A report file path is required.", "path");
            if (report.Server == null || string.IsNullOrWhiteSpace(report.Server.ServerName)) throw new InvalidOperationException("The health report has no server metadata.");
            if (report.Database == null || string.IsNullOrWhiteSpace(report.Database.Name)) throw new InvalidOperationException("The health report has no database metadata.");
            if (report.HealthScore == null || report.Coverage == null) throw new InvalidOperationException("The health report is incomplete.");
        }

        private static void WriteAtomically(string path, byte[] content, CancellationToken token)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                token.ThrowIfCancellationRequested();
                File.WriteAllBytes(temporary, content);
                token.ThrowIfCancellationRequested();
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null); else File.Move(temporary, fullPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static byte[] BuildJson(HealthReport report, ReportOptions options, CancellationToken token)
        {
            ExportDocument document = Project(report, options, token);
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(ExportDocument), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true }).WriteObject(stream, document);
                return stream.ToArray();
            }
        }

        private static ExportDocument Project(HealthReport report, ReportOptions options, CancellationToken token)
        {
            var document = new ExportDocument
            {
                SchemaVersion = ApplicationInfo.ReportSchemaVersion,
                ReportType = ApplicationInfo.ReportType,
                ApplicationVersion = ApplicationInfo.ApplicationVersion,
                GeneratedAt = report.GeneratedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                Server = Clean(report.Server.ServerName),
                Database = Clean(report.Database.Name),
                SqlServerVersion = Clean(report.SqlServerVersion),
                Edition = Clean(report.Server.Edition),
                CompatibilityLevel = report.Database.CompatibilityLevel,
                AssessmentStatus = report.AssessmentStatus.ToString(),
                AssessmentMessage = Clean(report.AssessmentMessage),
                Score = report.HealthScore.Percentage,
                Grade = report.HealthScore.Grade,
                Confidence = report.HealthScore.Confidence.ToString(),
                Coverage = report.Coverage.CoveragePercentage,
                LogicalGroupsEvaluated = report.HealthScore.LogicalGroupsEvaluated,
                ScoreMaximumUnits = report.HealthScore.MaxScore,
                ScoreEarnedUnits = report.HealthScore.Score
            };
            document.Execution = ExecutionSummary.From(report);
            foreach (HealthScoreBreakdown item in report.HealthScore.Breakdown.OrderByDescending(x => x.Included).ThenBy(x => x.DiagnosticName, StringComparer.OrdinalIgnoreCase))
            { token.ThrowIfCancellationRequested(); document.ScoreBreakdown.Add(ScoreItem.From(item)); }
            foreach (DiagnosticFinding finding in OrderedFindings(report))
            { token.ThrowIfCancellationRequested(); document.Findings.Add(FindingItem.From(finding, options.IncludeSuggestedSql)); }
            foreach (LogicalHealthFinding logical in report.LogicalFindings.OrderByDescending(x => SeverityRank(x.Severity)).ThenBy(x => x.Group, StringComparer.OrdinalIgnoreCase))
            { token.ThrowIfCancellationRequested(); document.LogicalFindings.Add(LogicalItem.From(logical, options.IncludeSuggestedSql)); }
            if (options.IncludeDiagnosticDetails)
            {
                foreach (DiagnosticResult result in OrderedResults(report))
                {
                    token.ThrowIfCancellationRequested();
                    DiagnosticItem item = DiagnosticItem.From(result);
                    if (options.IncludeRawResults)
                    {
                        foreach (DiagnosticResultSet set in result.ResultSets.OrderBy(x => x.Index))
                        {
                            var raw = new RawResultItem { Index = set.Index, Name = set.Name, RowsRead = set.RowsRead, Truncated = set.IsTruncated };
                            raw.Columns.AddRange(set.Columns.OrderBy(x => x.Ordinal).Select(x => x.Name));
                            foreach (IReadOnlyDictionary<string, object> row in set.Rows)
                            {
                                token.ThrowIfCancellationRequested();
                                var values = new List<string>();
                                foreach (DiagnosticColumn column in set.Columns.OrderBy(x => x.Ordinal)) { object value; row.TryGetValue(string.IsNullOrEmpty(column.Key) ? column.Name : column.Key, out value); values.Add(Format(value)); }
                                raw.Rows.Add(values);
                            }
                            item.RawResults.Add(raw);
                        }
                    }
                    document.Diagnostics.Add(item);
                }
            }
            return document;
        }

        private static string BuildFindingsCsv(HealthReport report, ReportOptions options, CancellationToken token)
        {
            var text = new StringBuilder("Diagnostic,Category,Severity,Impact,Confidence,Title,Description,Evidence,Recommendation,ScoreContribution\r\n");
            var categories = report.Interpretations.ToDictionary(x => x.DiagnosticId, x => x.Category, StringComparer.OrdinalIgnoreCase);
            foreach (DiagnosticFinding finding in OrderedFindings(report))
            {
                token.ThrowIfCancellationRequested(); DiagnosticCategory category; categories.TryGetValue(finding.DiagnosticId ?? string.Empty, out category);
                AppendCsv(text, finding.DiagnosticId, category.ToString(), finding.Severity.ToString(), finding.Impact.ToString(), finding.Confidence.ToString(), finding.Title, finding.Description, finding.Description, finding.Recommendation, finding.ScoreContribution.ToString(CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        private static string BuildDiagnosticsCsv(HealthReport report, ReportOptions options, CancellationToken token)
        {
            var text = new StringBuilder("Diagnostic,Category,Status,FailureKind,Duration,Message\r\n");
            foreach (DiagnosticResult result in OrderedResults(report))
            { token.ThrowIfCancellationRequested(); AppendCsv(text, result.DiagnosticName, result.Category.ToString(), result.Status.ToString(), result.FailureKind.ToString(), result.Duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture), result.UserMessage); }
            return text.ToString();
        }

        private static string BuildHtml(HealthReport report, ReportOptions options, CancellationToken token)
        {
            Func<object, string> h = value => WebUtility.HtmlEncode(Format(value));
            var b = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>SQL Server Health Report</title><style>");
            b.Append("body{font:14px Segoe UI,Arial,sans-serif;color:#24313f;background:#f4f6f8;margin:0}.page{max-width:1100px;margin:auto;background:white;padding:32px}h1,h2{color:#17324d}.cards{display:flex;gap:14px;flex-wrap:wrap}.card{border:1px solid #d9e1e8;border-radius:6px;padding:16px;min-width:150px}.score{font-size:36px;font-weight:700}.banner{padding:14px;border-left:5px solid #d38b00;background:#fff7df;margin:18px 0}.complete{border-color:#2f7d32;background:#edf7ee}table{border-collapse:collapse;width:100%;margin:10px 0 24px}th,td{border:1px solid #d9e1e8;padding:8px;text-align:left;vertical-align:top}th{background:#edf2f6}code,pre{white-space:pre-wrap;word-break:break-word;background:#f3f5f7;padding:8px}.muted{color:#637282}.critical{color:#a51d26}.warning{color:#8a5a00}@media print{body{background:white}.page{max-width:none;padding:0}}</style></head><body><main class=\"page\">");
            b.Append("<h1>SQL SERVER HEALTH REPORT</h1><p><strong>Server:</strong> ").Append(h(report.Server.ServerName)).Append("<br><strong>Database:</strong> ").Append(h(report.Database.Name)).Append("<br><strong>SQL Server:</strong> ").Append(h(report.SqlServerVersion)).Append(" · ").Append(h(report.Server.Edition)).Append("<br><strong>Generated:</strong> ").Append(h(report.GeneratedAt.ToString("u"))).Append("<br><strong>Application:</strong> ").Append(h(ApplicationInfo.ApplicationVersion)).Append(" · <strong>Schema:</strong> ").Append(h(ApplicationInfo.ReportSchemaVersion)).Append("</p>");
            string bannerClass = report.AssessmentStatus == AssessmentStatus.Complete ? "banner complete" : "banner";
            b.Append("<div class=\"").Append(bannerClass).Append("\"><strong>").Append(h(report.AssessmentStatus)).Append("</strong><br>").Append(h(report.AssessmentMessage)).Append("</div><div class=\"cards\">");
            Card(b, "Health Score", report.HealthScore.Percentage.ToString("0.##") + " / 100", report.HealthScore.Grade, h); Card(b, "Coverage", report.Coverage.CoveragePercentage.ToString("0.##") + "%", report.Coverage.SuccessfulDiagnostics + " of " + report.Coverage.EligibleDiagnostics + " eligible", h); Card(b, "Confidence", report.HealthScore.Confidence.ToString(), "Evidence confidence", h); Card(b, "Unavailable", (report.Coverage.FailedDiagnostics + report.Coverage.SkippedDiagnostics).ToString(), report.Coverage.FailedDiagnostics + " failed, " + report.Coverage.SkippedDiagnostics + " skipped", h); b.Append("</div>");
            b.Append("<h2>Summary</h2><p>Critical: ").Append(report.HealthScore.CriticalFindings).Append(" · Warnings: ").Append(report.HealthScore.WarningFindings).Append(" · Information: ").Append(report.HealthScore.InformationFindings).Append("</p>");
            b.Append("<h2>Findings</h2><table><thead><tr><th>Severity</th><th>Diagnostic</th><th>Finding</th><th>Impact / Confidence</th><th>Recommendation</th></tr></thead><tbody>");
            foreach (DiagnosticFinding f in OrderedFindings(report)) { token.ThrowIfCancellationRequested(); b.Append("<tr><td>").Append(h(f.Severity)).Append("</td><td>").Append(h(f.DiagnosticId)).Append("</td><td><strong>").Append(h(f.Title)).Append("</strong><br>").Append(h(f.Description)); if (options.IncludeSuggestedSql && !string.IsNullOrWhiteSpace(f.SuggestedSql)) b.Append("<p><strong>Suggested SQL (text only; never executed)</strong></p><pre>").Append(h(f.SuggestedSql)).Append("</pre>"); b.Append("</td><td>").Append(h(f.Impact)).Append(" / ").Append(h(f.Confidence)).Append("</td><td>").Append(h(f.Recommendation)).Append("</td></tr>"); }
            if (report.Findings.Count == 0) b.Append("<tr><td colspan=\"5\">No interpreted findings.</td></tr>"); b.Append("</tbody></table>");
            b.Append("<h2>Score Breakdown</h2><table><thead><tr><th>Diagnostic/group</th><th>Included</th><th>Weight</th><th>Penalty</th><th>Explanation</th></tr></thead><tbody>");
            foreach (HealthScoreBreakdown x in report.HealthScore.Breakdown.OrderByDescending(x => x.Included).ThenBy(x => x.DiagnosticName, StringComparer.OrdinalIgnoreCase)) { token.ThrowIfCancellationRequested(); b.Append("<tr><td>").Append(h(x.DiagnosticName)).Append("</td><td>").Append(h(x.Included)).Append("</td><td>").Append(h(x.Weight)).Append("</td><td>").Append(h(x.Penalty)).Append("</td><td>").Append(h(x.Explanation)).Append("</td></tr>"); } b.Append("</tbody></table>");
            b.Append("<h2>Failed / Skipped Diagnostics</h2><table><thead><tr><th>Diagnostic</th><th>Status</th><th>Failure</th><th>Message</th><th>Required permissions</th></tr></thead><tbody>");
            var unavailable = OrderedResults(report).Where(x => x.Status != DiagnosticExecutionStatus.Succeeded).ToList(); foreach (DiagnosticResult x in unavailable) { token.ThrowIfCancellationRequested(); b.Append("<tr><td>").Append(h(x.DiagnosticName)).Append("</td><td>").Append(h(x.Status)).Append("</td><td>").Append(h(x.FailureKind)).Append("</td><td>").Append(h(x.UserMessage)).Append("</td><td>").Append(h(string.Join("; ", x.RequiredPermissions))).Append("</td></tr>"); } if (unavailable.Count == 0) b.Append("<tr><td colspan=\"5\">None.</td></tr>"); b.Append("</tbody></table>");
            if (options.IncludeDiagnosticDetails) { b.Append("<h2>Diagnostic Execution Summary</h2><table><thead><tr><th>Category</th><th>Diagnostic</th><th>Status</th><th>Duration</th><th>Message</th></tr></thead><tbody>"); foreach (DiagnosticResult x in OrderedResults(report)) { token.ThrowIfCancellationRequested(); b.Append("<tr><td>").Append(h(x.Category)).Append("</td><td>").Append(h(x.DiagnosticName)).Append("</td><td>").Append(h(x.Status)).Append("</td><td>").Append(h(x.Duration.TotalSeconds.ToString("0.###") + " s")).Append("</td><td>").Append(h(x.UserMessage)).Append("</td></tr>"); } b.Append("</tbody></table>"); }
            b.Append("<p class=\"muted\">This is a read-only assessment. Suggested SQL is report text only and is never executed by this report.</p></main></body></html>"); return b.ToString();
        }

        private static IEnumerable<DiagnosticFinding> OrderedFindings(HealthReport report) { return report.Findings.OrderByDescending(x => SeverityRank(x.Severity)).ThenByDescending(x => ImpactRank(x.Impact)).ThenBy(x => x.ScoreContribution).ThenBy(x => x.DiagnosticId, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase); }
        private static IEnumerable<DiagnosticResult> OrderedResults(HealthReport report) { return report.Results.OrderBy(x => x.Category).ThenBy(x => x.DiagnosticName, StringComparer.OrdinalIgnoreCase); }
        private static int SeverityRank(DiagnosticSeverity value) { return (int)value; }
        private static int ImpactRank(FindingImpact value) { return (int)value; }
        private static string Format(object value) { if (value == null || value == DBNull.Value) return string.Empty; var formattable = value as IFormattable; return Clean(formattable == null ? Convert.ToString(value, CultureInfo.InvariantCulture) : formattable.ToString(null, CultureInfo.InvariantCulture)); }
        private static string Clean(string value) { return Regex.Replace(value ?? string.Empty, "(?i)\\b(password|pwd)\\s*=\\s*(?:\"[^\"]*\"|'[^']*'|[^;\\s]+)", "$1=[REDACTED]"); }
        private static void Card(StringBuilder b, string title, string value, string note, Func<object, string> h) { b.Append("<section class=\"card\"><strong>").Append(h(title)).Append("</strong><div class=\"score\">").Append(h(value)).Append("</div><span class=\"muted\">").Append(h(note)).Append("</span></section>"); }
        private static void AppendCsv(StringBuilder b, params string[] values) { b.Append(string.Join(",", values.Select(Csv))).Append("\r\n"); }
        private static string Csv(string value) { string text = Clean(value); return "\"" + text.Replace("\"", "\"\"") + "\""; }

        [DataContract] private sealed class ExportDocument
        {
            public ExportDocument() { Findings = new List<FindingItem>(); LogicalFindings = new List<LogicalItem>(); Diagnostics = new List<DiagnosticItem>(); ScoreBreakdown = new List<ScoreItem>(); }
            [DataMember(Name="schemaVersion", Order=1)] public string SchemaVersion { get; set; } [DataMember(Name="reportType", Order=2)] public string ReportType { get; set; } [DataMember(Name="applicationVersion", Order=3)] public string ApplicationVersion { get; set; } [DataMember(Name="generatedAtUtc", Order=4)] public string GeneratedAt { get; set; }
            [DataMember(Name="server", Order=5)] public string Server { get; set; } [DataMember(Name="database", Order=6)] public string Database { get; set; } [DataMember(Name="sqlServerVersion", Order=7)] public string SqlServerVersion { get; set; } [DataMember(Name="edition", Order=8)] public string Edition { get; set; } [DataMember(Name="compatibilityLevel", Order=9)] public int CompatibilityLevel { get; set; }
            [DataMember(Name="assessmentStatus", Order=10)] public string AssessmentStatus { get; set; } [DataMember(Name="assessmentMessage", Order=11)] public string AssessmentMessage { get; set; } [DataMember(Name="healthScore", Order=12)] public decimal Score { get; set; } [DataMember(Name="grade", Order=13)] public string Grade { get; set; } [DataMember(Name="coveragePercentage", Order=14)] public decimal Coverage { get; set; } [DataMember(Name="confidence", Order=15)] public string Confidence { get; set; } [DataMember(Name="logicalGroupsEvaluated", Order=16)] public int LogicalGroupsEvaluated { get; set; } [DataMember(Name="scoreMaximumUnits", Order=17)] public decimal ScoreMaximumUnits { get; set; } [DataMember(Name="scoreEarnedUnits", Order=18)] public decimal ScoreEarnedUnits { get; set; }
            [DataMember(Name="executionSummary", Order=19)] public ExecutionSummary Execution { get; set; } [DataMember(Name="scoreBreakdown", Order=20)] public List<ScoreItem> ScoreBreakdown { get; private set; } [DataMember(Name="findings", Order=21)] public List<FindingItem> Findings { get; private set; } [DataMember(Name="logicalFindings", Order=22)] public List<LogicalItem> LogicalFindings { get; private set; } [DataMember(Name="diagnostics", Order=23)] public List<DiagnosticItem> Diagnostics { get; private set; }
        }
        [DataContract] private sealed class ExecutionSummary { [DataMember(Name="total")] public int Total { get; set; } [DataMember(Name="eligible")] public int Eligible { get; set; } [DataMember(Name="succeeded")] public int Succeeded { get; set; } [DataMember(Name="failed")] public int Failed { get; set; } [DataMember(Name="skipped")] public int Skipped { get; set; } [DataMember(Name="cancelled")] public int Cancelled { get; set; } public static ExecutionSummary From(HealthReport r) { return new ExecutionSummary { Total=r.DiagnosticsTotal, Eligible=r.Coverage.EligibleDiagnostics, Succeeded=r.Results.Count(x=>x.Status==DiagnosticExecutionStatus.Succeeded), Failed=r.Results.Count(x=>x.Status==DiagnosticExecutionStatus.Failed), Skipped=r.Results.Count(x=>x.Status==DiagnosticExecutionStatus.Skipped), Cancelled=r.Results.Count(x=>x.Status==DiagnosticExecutionStatus.Cancelled) }; } }
        [DataContract] private sealed class FindingItem { [DataMember(Name="id")] public string Id { get; set; } [DataMember(Name="diagnosticId")] public string DiagnosticId { get; set; } [DataMember(Name="severity")] public string Severity { get; set; } [DataMember(Name="impact")] public string Impact { get; set; } [DataMember(Name="confidence")] public string Confidence { get; set; } [DataMember(Name="title")] public string Title { get; set; } [DataMember(Name="description")] public string Description { get; set; } [DataMember(Name="recommendation")] public string Recommendation { get; set; } [DataMember(Name="suggestedSql", EmitDefaultValue=false)] public string SuggestedSql { get; set; } [DataMember(Name="scoreContribution")] public decimal ScoreContribution { get; set; } public static FindingItem From(DiagnosticFinding f,bool sql){return new FindingItem{Id=Clean(f.Id),DiagnosticId=Clean(f.DiagnosticId),Severity=f.Severity.ToString(),Impact=f.Impact.ToString(),Confidence=f.Confidence.ToString(),Title=Clean(f.Title),Description=Clean(f.Description),Recommendation=Clean(f.Recommendation),SuggestedSql=sql?Clean(f.SuggestedSql):null,ScoreContribution=f.ScoreContribution};} }
        [DataContract] private sealed class LogicalItem { public LogicalItem(){SupportingFindingIds=new List<string>();} [DataMember(Name="group")] public string Group { get; set; } [DataMember(Name="severity")] public string Severity { get; set; } [DataMember(Name="impact")] public string Impact { get; set; } [DataMember(Name="scoreContribution")] public decimal ScoreContribution { get; set; } [DataMember(Name="primaryFinding")] public FindingItem Primary { get; set; } [DataMember(Name="supportingFindingIds")] public List<string> SupportingFindingIds { get; private set; } public static LogicalItem From(LogicalHealthFinding x,bool sql){var v=new LogicalItem{Group=Clean(x.Group),Severity=x.Severity.ToString(),Impact=x.Impact.ToString(),ScoreContribution=x.ScoreContribution,Primary=x.PrimaryFinding==null?null:FindingItem.From(x.PrimaryFinding,sql)};v.SupportingFindingIds.AddRange(x.SupportingFindings.Select(f=>Clean(f.Id)));return v;} }
        [DataContract] private sealed class ScoreItem { [DataMember(Name="diagnosticId")] public string DiagnosticId { get; set; } [DataMember(Name="diagnosticName")] public string DiagnosticName { get; set; } [DataMember(Name="group")] public string Group { get; set; } [DataMember(Name="included")] public bool Included { get; set; } [DataMember(Name="severity")] public string Severity { get; set; } [DataMember(Name="weight")] public decimal Weight { get; set; } [DataMember(Name="penalty")] public decimal Penalty { get; set; } [DataMember(Name="explanation")] public string Explanation { get; set; } public static ScoreItem From(HealthScoreBreakdown x){return new ScoreItem{DiagnosticId=Clean(x.DiagnosticId),DiagnosticName=Clean(x.DiagnosticName),Group=Clean(x.DeduplicationGroup),Included=x.Included,Severity=x.Severity.ToString(),Weight=x.Weight,Penalty=x.Penalty,Explanation=Clean(x.Explanation)};} }
        [DataContract] private sealed class DiagnosticItem { public DiagnosticItem(){RequiredPermissions=new List<string>();RawResults=new List<RawResultItem>();} [DataMember(Name="id")] public string Id { get; set; } [DataMember(Name="name")] public string Name { get; set; } [DataMember(Name="category")] public string Category { get; set; } [DataMember(Name="status")] public string Status { get; set; } [DataMember(Name="failureKind")] public string FailureKind { get; set; } [DataMember(Name="durationMilliseconds")] public long Duration { get; set; } [DataMember(Name="message")] public string Message { get; set; } [DataMember(Name="sqlErrorNumber",EmitDefaultValue=false)] public int? SqlErrorNumber { get; set; } [DataMember(Name="requiredPermissions")] public List<string> RequiredPermissions { get; private set; } [DataMember(Name="rawResults",EmitDefaultValue=false)] public List<RawResultItem> RawResults { get; private set; } public static DiagnosticItem From(DiagnosticResult x){var v=new DiagnosticItem{Id=Clean(x.DiagnosticId),Name=Clean(x.DiagnosticName),Category=x.Category.ToString(),Status=x.Status.ToString(),FailureKind=x.FailureKind.ToString(),Duration=(long)x.Duration.TotalMilliseconds,Message=Clean(x.UserMessage),SqlErrorNumber=x.SqlErrorNumber};v.RequiredPermissions.AddRange(x.RequiredPermissions.Select(Clean));return v;} }
        [DataContract] private sealed class RawResultItem { public RawResultItem(){Columns=new List<string>();Rows=new List<List<string>>();} [DataMember(Name="index")] public int Index { get; set; } [DataMember(Name="name")] public string Name { get; set; } [DataMember(Name="rowsRead")] public long RowsRead { get; set; } [DataMember(Name="truncated")] public bool Truncated { get; set; } [DataMember(Name="columns")] public List<string> Columns { get; private set; } [DataMember(Name="rows")] public List<List<string>> Rows { get; private set; } }
    }
}