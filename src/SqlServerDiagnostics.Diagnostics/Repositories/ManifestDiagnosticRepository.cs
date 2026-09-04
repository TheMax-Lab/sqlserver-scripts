using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Repositories
{
    public sealed class ManifestDiagnosticRepository : IDiagnosticRepository
    {
        private readonly string diagnosticsRoot;

        public ManifestDiagnosticRepository(string diagnosticsRoot)
        {
            if (string.IsNullOrWhiteSpace(diagnosticsRoot)) throw new ArgumentException("A diagnostics root path is required.", "diagnosticsRoot");
            this.diagnosticsRoot = Path.GetFullPath(diagnosticsRoot);
        }

        public string ManifestPath { get { return Path.Combine(diagnosticsRoot, "manifest.json"); } }

        public Task<IReadOnlyList<DiagnosticDefinition>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run<IReadOnlyList<DiagnosticDefinition>>(() => LoadAndValidate(cancellationToken), cancellationToken);
        }

        public Task<string> LoadScriptAsync(DiagnosticDefinition definition, CancellationToken cancellationToken)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            definition.Validate();
            string scriptPath = ResolveScriptPath(definition.ScriptPath);
            if (!File.Exists(scriptPath)) throw new FileNotFoundException("The diagnostic SQL file was not found.", scriptPath);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() => File.ReadAllText(scriptPath), cancellationToken);
        }

        private IReadOnlyList<DiagnosticDefinition> LoadAndValidate(CancellationToken cancellationToken)
        {
            if (!File.Exists(ManifestPath)) throw new FileNotFoundException("The diagnostic manifest was not found.", ManifestPath);
            DiagnosticManifestDto manifest;
            try
            {
                using (FileStream stream = File.OpenRead(ManifestPath)) manifest = new DataContractJsonSerializer(typeof(DiagnosticManifestDto)).ReadObject(stream) as DiagnosticManifestDto;
            }
            catch (SerializationException exception) { throw new InvalidDataException("The diagnostic manifest is not valid JSON metadata.", exception); }

            if (manifest == null || manifest.SchemaVersion != 1) throw new InvalidDataException("The diagnostic manifest schemaVersion must be 1.");
            if (manifest.Diagnostics == null) throw new InvalidDataException("The diagnostic manifest must contain a diagnostics array.");

            var definitions = new List<DiagnosticDefinition>();
            var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DiagnosticDefinitionDto dto in manifest.Diagnostics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DiagnosticDefinition definition = dto == null ? null : dto.ToModel();
                if (definition == null) throw new InvalidDataException("The diagnostic manifest contains an empty definition.");
                try { definition.Validate(); }
                catch (Exception exception) when (exception is InvalidOperationException || exception is FormatException) { throw new InvalidDataException("Diagnostic '" + (definition.Id ?? "<unknown>") + "' is invalid: " + exception.Message, exception); }
                if (!identifiers.Add(definition.Id)) throw new InvalidDataException("Duplicate diagnostic ID: " + definition.Id);
                string scriptPath = ResolveScriptPath(definition.ScriptPath);
                if (!string.Equals(Path.GetExtension(scriptPath), ".sql", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Diagnostic '" + definition.Id + "' must reference a .sql file.");
                if (!File.Exists(scriptPath)) throw new FileNotFoundException("Diagnostic '" + definition.Id + "' references a missing SQL file.", scriptPath);
                definitions.Add(definition);
            }
            return definitions.AsReadOnly();
        }

        private string ResolveScriptPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) throw new InvalidDataException("Diagnostic script paths must be non-empty relative paths.");
            string root = diagnosticsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(diagnosticsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Diagnostic script paths must remain inside the diagnostics directory.");
            return path;
        }

        private static TEnum ParseEnum<TEnum>(string value, string field, string id) where TEnum : struct
        {
            TEnum parsed;
            if (string.IsNullOrWhiteSpace(value) || !Enum.TryParse(value, true, out parsed) || !Enum.IsDefined(typeof(TEnum), parsed)) throw new InvalidDataException("Diagnostic '" + (id ?? "<unknown>") + "' has invalid " + field + ".");
            return parsed;
        }

        [DataContract]
        private sealed class DiagnosticManifestDto
        {
            [DataMember(Name = "schemaVersion", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "diagnostics", IsRequired = true)] public List<DiagnosticDefinitionDto> Diagnostics { get; set; }
        }

        [DataContract]
        private sealed class DiagnosticDefinitionDto
        {
            [DataMember(Name = "id", IsRequired = true)] public string Id { get; set; }
            [DataMember(Name = "name", IsRequired = true)] public string Name { get; set; }
            [DataMember(Name = "description", IsRequired = true)] public string Description { get; set; }
            [DataMember(Name = "category", IsRequired = true)] public string Category { get; set; }
            [DataMember(Name = "scriptPath", IsRequired = true)] public string ScriptPath { get; set; }
            [DataMember(Name = "originalPath")] public string OriginalPath { get; set; }
            [DataMember(Name = "readOnly", IsRequired = true)] public bool ReadOnly { get; set; }
            [DataMember(Name = "healthCheckEnabled", IsRequired = true)] public bool HealthCheckEnabled { get; set; }
            [DataMember(Name = "executionCost", IsRequired = true)] public string ExecutionCost { get; set; }
            [DataMember(Name = "executionScope", IsRequired = true)] public string ExecutionScope { get; set; }
            [DataMember(Name = "minimumSqlServerVersion")] public string MinimumSqlServerVersion { get; set; }
            [DataMember(Name = "maximumSqlServerVersion")] public string MaximumSqlServerVersion { get; set; }
            [DataMember(Name = "requiresQueryStore", IsRequired = true)] public bool RequiresQueryStore { get; set; }
            [DataMember(Name = "requiredPermissions", IsRequired = true)] public List<string> RequiredPermissions { get; set; }
            [DataMember(Name = "supportsAzureSql", IsRequired = true)] public bool SupportsAzureSql { get; set; }
            [DataMember(Name = "multipleResultSets", IsRequired = true)] public bool MultipleResultSets { get; set; }
            [DataMember(Name = "defaultSeverity", IsRequired = true)] public string DefaultSeverity { get; set; }
            [DataMember(Name = "deduplicationGroup")] public string DeduplicationGroup { get; set; }
            [DataMember(Name = "timeoutSeconds", IsRequired = true)] public int TimeoutSeconds { get; set; }
            [DataMember(Name = "tags")] public List<string> Tags { get; set; }
            [DataMember(Name = "compatibilityNotes")] public string CompatibilityNotes { get; set; }
            [DataMember(Name = "resultInterpretation", IsRequired = true)] public ResultInterpretationDto ResultInterpretation { get; set; }
            [DataMember(Name = "scorePolicy", IsRequired = true)] public ScorePolicyDto ScorePolicy { get; set; }

            public DiagnosticDefinition ToModel()
            {
                var definition = new DiagnosticDefinition
                {
                    Id = Id, Name = Name, Description = Description,
                    Category = ParseEnum<DiagnosticCategory>(Category, "category", Id),
                    ScriptPath = ScriptPath, OriginalPath = OriginalPath, ReadOnly = ReadOnly,
                    HealthCheckEnabled = HealthCheckEnabled,
                    ExecutionCost = ParseEnum<DiagnosticExecutionCost>(ExecutionCost, "executionCost", Id),
                    ExecutionScope = ParseEnum<DiagnosticScope>(ExecutionScope, "executionScope", Id),
                    MinimumSqlServerVersion = MinimumSqlServerVersion, MaximumSqlServerVersion = MaximumSqlServerVersion,
                    RequiresQueryStore = RequiresQueryStore, SupportsAzureSql = SupportsAzureSql,
                    MultipleResultSets = MultipleResultSets,
                    DefaultSeverity = ParseEnum<DiagnosticSeverity>(DefaultSeverity, "defaultSeverity", Id),
                    DeduplicationGroup = DeduplicationGroup, TimeoutSeconds = TimeoutSeconds, CompatibilityNotes = CompatibilityNotes,
                    ResultInterpretation = ResultInterpretation == null ? null : ResultInterpretation.ToModel(Id),
                    ScorePolicy = ScorePolicy == null ? null : ScorePolicy.ToModel()
                };
                foreach (string permission in RequiredPermissions ?? Enumerable.Empty<string>()) definition.RequiredPermissions.Add(permission);
                foreach (string tag in Tags ?? Enumerable.Empty<string>()) definition.Tags.Add(tag);
                return definition;
            }
        }

        [DataContract]
        private sealed class ResultInterpretationDto
        {
            [DataMember(Name = "mode", IsRequired = true)] public string Mode { get; set; }
            [DataMember(Name = "emptyResultMeaning", IsRequired = true)] public string EmptyResultMeaning { get; set; }
            [DataMember(Name = "impact", IsRequired = true)] public string Impact { get; set; }
            [DataMember(Name = "confidence", IsRequired = true)] public string Confidence { get; set; }
            [DataMember(Name = "metric")] public string Metric { get; set; }
            [DataMember(Name = "warningThreshold")] public decimal? WarningThreshold { get; set; }
            [DataMember(Name = "criticalThreshold")] public decimal? CriticalThreshold { get; set; }
            [DataMember(Name = "higherIsWorse")] public bool HigherIsWorse { get; set; }

            public ResultInterpretationPolicy ToModel(string id)
            {
                return new ResultInterpretationPolicy
                {
                    Mode = ParseEnum<InterpretationMode>(Mode, "resultInterpretation.mode", id),
                    EmptyResultMeaning = ParseEnum<EmptyResultMeaning>(EmptyResultMeaning, "resultInterpretation.emptyResultMeaning", id),
                    Impact = ParseEnum<FindingImpact>(Impact, "resultInterpretation.impact", id),
                    Confidence = ParseEnum<InterpretationConfidence>(Confidence, "resultInterpretation.confidence", id),
                    Metric = Metric, WarningThreshold = WarningThreshold, CriticalThreshold = CriticalThreshold,
                    HigherIsWorse = HigherIsWorse
                };
            }
        }

        [DataContract]
        private sealed class ScorePolicyDto
        {
            [DataMember(Name = "scoreEligible", IsRequired = true)] public bool ScoreEligible { get; set; }
            [DataMember(Name = "weight", IsRequired = true)] public decimal Weight { get; set; }
            [DataMember(Name = "criticalPenaltyFraction", IsRequired = true)] public decimal CriticalPenaltyFraction { get; set; }
            [DataMember(Name = "warningPenaltyFraction", IsRequired = true)] public decimal WarningPenaltyFraction { get; set; }
            [DataMember(Name = "informationPenaltyFraction", IsRequired = true)] public decimal InformationPenaltyFraction { get; set; }
            public DiagnosticScorePolicy ToModel() { return new DiagnosticScorePolicy { ScoreEligible = ScoreEligible, Weight = Weight, CriticalPenaltyFraction = CriticalPenaltyFraction, WarningPenaltyFraction = WarningPenaltyFraction, InformationPenaltyFraction = InformationPenaltyFraction }; }
        }
    }
}