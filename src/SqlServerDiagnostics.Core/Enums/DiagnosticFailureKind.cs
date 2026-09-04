namespace TheMaxLab.SqlServerDiagnostics.Core.Enums
{
    public enum DiagnosticFailureKind { None = 0, PermissionDenied = 1, SqlError = 2, Timeout = 3, UnsupportedVersion = 4, QueryStoreUnavailable = 5, DatabaseUnavailable = 6, ConnectionFailure = 7, InvalidDefinition = 8, Cancellation = 9 }
}