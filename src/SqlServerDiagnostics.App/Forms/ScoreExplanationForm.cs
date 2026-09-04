using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Forms
{
    public sealed class ScoreExplanationForm : Form
    {
        public ScoreExplanationForm(HealthReport report)
        {
            Text = "Why this score?"; StartPosition = FormStartPosition.CenterParent; Size = new Size(850, 620); Font = new Font("Segoe UI", 9F);
            var text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9F), Text = BuildText(report) };
            var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.OK }; Controls.Add(text); Controls.Add(close); AcceptButton = close; CancelButton = close;
        }
        private static string BuildText(HealthReport report)
        {
            HealthScore s = report.HealthScore; var t = new StringBuilder().AppendLine("HEALTH SCORE EXPLANATION").AppendLine().AppendLine("Assessment status: " + report.AssessmentStatus).AppendLine(report.AssessmentMessage).AppendLine().AppendLine("Earned logical units: " + s.Score.ToString("0.##")).AppendLine("Maximum evaluated units: " + s.MaxScore.ToString("0.##")).AppendLine("Displayed score: " + s.Percentage.ToString("0.##") + " / 100 (" + s.Grade + ")").AppendLine("Evaluated logical groups: " + s.LogicalGroupsEvaluated).AppendLine("Coverage: " + report.Coverage.CoveragePercentage.ToString("0.##") + "% (" + report.Coverage.SuccessfulDiagnostics + " / " + report.Coverage.EligibleDiagnostics + ")").AppendLine("Confidence: " + s.Confidence).AppendLine("Failed diagnostics: " + report.Coverage.FailedDiagnostics).AppendLine("Skipped diagnostics: " + report.Coverage.SkippedDiagnostics).AppendLine();
            foreach (string e in s.Explanations) t.AppendLine("• " + e); t.AppendLine().AppendLine("BREAKDOWN"); foreach (HealthScoreBreakdown x in s.Breakdown.OrderByDescending(x => x.Included).ThenByDescending(x => x.Penalty)) t.AppendLine(string.Format("{0,-34} {1,8}  {2}", x.DiagnosticName, x.Included ? (-x.Penalty).ToString("0.##") : "excluded", x.Explanation));
            return t.AppendLine().AppendLine("Skipped/failed diagnostics reduce coverage and confidence, not health points.").ToString();
        }
    }
}