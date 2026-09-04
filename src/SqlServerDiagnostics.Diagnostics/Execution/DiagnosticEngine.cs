using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Execution
{
    public sealed class DiagnosticEngine : IDiagnosticEngine
    {
        private readonly IDiagnosticRepository repository;
        private readonly ISqlServerService sqlServerService;
        private readonly DiagnosticResultNormalizer normalizer;
        private readonly DiagnosticErrorClassifier errorClassifier;
        private readonly IApplicationLogger logger;
        private readonly int maximumRowsPerResultSet;

        public DiagnosticEngine(IDiagnosticRepository repository, ISqlServerService sqlServerService, DiagnosticResultNormalizer normalizer, DiagnosticErrorClassifier errorClassifier, IApplicationLogger logger, int maximumRowsPerResultSet = 10000)
        {
            this.repository = repository ?? throw new ArgumentNullException("repository");
            this.sqlServerService = sqlServerService ?? throw new ArgumentNullException("sqlServerService");
            this.normalizer = normalizer ?? throw new ArgumentNullException("normalizer");
            this.errorClassifier = errorClassifier ?? throw new ArgumentNullException("errorClassifier");
            this.logger = logger ?? throw new ArgumentNullException("logger");
            if (maximumRowsPerResultSet <= 0) throw new ArgumentOutOfRangeException("maximumRowsPerResultSet");
            this.maximumRowsPerResultSet = maximumRowsPerResultSet;
        }

        public Task<IReadOnlyList<DiagnosticDefinition>> GetDiagnosticsAsync(CancellationToken cancellationToken) { return repository.GetAllAsync(cancellationToken); }

        public async Task<IReadOnlyList<DiagnosticDefinition>> GetHealthCheckCandidatesAsync(DiagnosticExecutionContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException("context");
            IReadOnlyList<DiagnosticDefinition> definitions = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            return definitions.Where(item => item.HealthCheckEnabled && item.ReadOnly && item.ExecutionCost != DiagnosticExecutionCost.High).ToList().AsReadOnly();
        }

        public async Task<DiagnosticResult> ExecuteAsync(DiagnosticDefinition definition, DiagnosticExecutionContext context, CancellationToken cancellationToken)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (context == null) throw new ArgumentNullException("context");
            var result = CreateResult(definition);
            var stopwatch = Stopwatch.StartNew();
            result.StartedAt = DateTimeOffset.UtcNow;
            result.Status = DiagnosticExecutionStatus.Running;
            Log(LogLevel.Information, "diagnostic.started", definition.Id, result.Status, TimeSpan.Zero, null);
            try
            {
                definition.Validate();
                DiagnosticFailureKind incompatibility;
                string reason;
                if (!context.Supports(definition, out incompatibility, out reason))
                {
                    result.Status = DiagnosticExecutionStatus.Skipped;
                    result.FailureKind = incompatibility;
                    result.UserMessage = reason;
                    return result;
                }

                string sql = await repository.LoadScriptAsync(definition, cancellationToken).ConfigureAwait(false);
                SqlQueryResult queryResult = await sqlServerService.ExecuteQueryWithMultipleResultsAsync(context.Connection, sql, new List<SqlQueryParameter>().AsReadOnly(), definition.TimeoutSeconds, maximumRowsPerResultSet, cancellationToken).ConfigureAwait(false);
                foreach (DiagnosticResultSet resultSet in queryResult.ResultSets) result.ResultSets.Add(resultSet);
                normalizer.Normalize(definition, result);
                result.Status = DiagnosticExecutionStatus.Succeeded;
                result.FailureKind = DiagnosticFailureKind.None;
                result.UserMessage = result.ResultSets.Any(item => item.IsTruncated) ? "Diagnostic completed. One or more result sets were truncated for presentation safety." : "Diagnostic completed.";
                return result;
            }
            catch (OperationCanceledException)
            {
                result.Status = DiagnosticExecutionStatus.Cancelled;
                result.FailureKind = DiagnosticFailureKind.Cancellation;
                result.UserMessage = errorClassifier.GetUserMessage(result.FailureKind);
                return result;
            }
            catch (Exception exception)
            {
                result.Status = DiagnosticExecutionStatus.Failed;
                result.FailureKind = errorClassifier.Classify(exception);
                result.UserMessage = errorClassifier.GetUserMessage(result.FailureKind);
                result.ErrorMessage = result.UserMessage;
                var sqlException = exception as SqlException;
                if (sqlException != null) result.SqlErrorNumber = sqlException.Number;
                Log(LogLevel.Error, "diagnostic.failed", definition.Id, result.Status, stopwatch.Elapsed, exception);
                return result;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.CompletedAt = DateTimeOffset.UtcNow;
                Log(result.Status == DiagnosticExecutionStatus.Failed ? LogLevel.Error : LogLevel.Information, "diagnostic.completed", definition.Id, result.Status, result.Duration, null);
            }
        }

        public async Task<HealthCheckReport> RunHealthCheckAsync(DiagnosticExecutionContext context, IProgress<DiagnosticProgress> progress, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException("context");
            IReadOnlyList<DiagnosticDefinition> candidates = await GetHealthCheckCandidatesAsync(context, cancellationToken).ConfigureAwait(false);
            List<DiagnosticDefinition> selected = candidates.ToList();
            var report = new HealthCheckReport { Server = context.Server, Database = context.Database, StartedAtUtc = DateTimeOffset.UtcNow };
            for (int index = 0; index < selected.Count; index++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                DiagnosticDefinition definition = selected[index];
                DiagnosticFailureKind failureKind;
                string reason;
                if (!context.IsHealthCheckEligible(definition, out failureKind, out reason))
                {
                    DiagnosticResult skipped = CreateResult(definition);
                    skipped.StartedAt = DateTimeOffset.UtcNow; skipped.CompletedAt = skipped.StartedAt; skipped.Status = DiagnosticExecutionStatus.Skipped; skipped.FailureKind = failureKind; skipped.UserMessage = reason;
                    report.Results.Add(skipped);
                    Report(progress, index + 1, selected.Count, definition, DiagnosticProgressStage.Skipped, reason);
                    continue;
                }
                Report(progress, index, selected.Count, definition, DiagnosticProgressStage.Started, "Diagnostic started.");
                Report(progress, index, selected.Count, definition, DiagnosticProgressStage.Executing, "Diagnostic executing.");
                DiagnosticResult result = await ExecuteAsync(definition, context, cancellationToken).ConfigureAwait(false);
                report.Results.Add(result);
                DiagnosticProgressStage stage = result.Status == DiagnosticExecutionStatus.Succeeded ? DiagnosticProgressStage.Completed : result.Status == DiagnosticExecutionStatus.Skipped ? DiagnosticProgressStage.Skipped : result.Status == DiagnosticExecutionStatus.Cancelled ? DiagnosticProgressStage.Cancelled : DiagnosticProgressStage.Failed;
                Report(progress, index + 1, selected.Count, definition, stage, result.UserMessage);
                if (result.Status == DiagnosticExecutionStatus.Cancelled) break;
            }
            report.CompletedAtUtc = DateTimeOffset.UtcNow;
            return report;
        }

        private static DiagnosticResult CreateResult(DiagnosticDefinition definition)
        {
            var result = new DiagnosticResult { DiagnosticId = definition.Id, DiagnosticName = definition.Name, Category = definition.Category, ExecutionScope = definition.ExecutionScope };
            foreach (string permission in definition.RequiredPermissions) result.RequiredPermissions.Add(permission);
            result.RequiredPermission = string.Join("; ", definition.RequiredPermissions);
            return result;
        }

        private static void Report(IProgress<DiagnosticProgress> progress, int completed, int total, DiagnosticDefinition definition, DiagnosticProgressStage stage, string message)
        {
            if (progress != null) progress.Report(new DiagnosticProgress { Completed = completed, Total = total, CurrentDiagnosticId = definition.Id, CurrentDiagnosticName = definition.Name, Stage = stage, Message = message });
        }

        private void Log(LogLevel level, string eventName, string diagnosticId, DiagnosticExecutionStatus status, TimeSpan duration, Exception exception)
        {
            var logEvent = new LogEvent { Level = level, EventName = eventName, Message = "Diagnostic lifecycle event." };
            logEvent.Properties["diagnosticId"] = diagnosticId;
            logEvent.Properties["status"] = status.ToString();
            logEvent.Properties["durationMs"] = ((long)duration.TotalMilliseconds).ToString();
            var sqlException = exception as SqlException;
            if (sqlException != null) logEvent.Properties["sqlErrorNumber"] = sqlException.Number.ToString();
            logger.Log(logEvent, exception);
        }
    }
}