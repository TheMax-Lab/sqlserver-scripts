using System;
using System.Data.SqlClient;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Execution
{
    public sealed class DiagnosticErrorClassifier
    {
        public DiagnosticFailureKind Classify(Exception exception)
        {
            if (exception is OperationCanceledException) return DiagnosticFailureKind.Cancellation;
            if (exception is TimeoutException) return DiagnosticFailureKind.Timeout;
            var sqlException = exception as SqlException;
            if (sqlException == null) return DiagnosticFailureKind.SqlError;
            return ClassifySqlErrorNumber(sqlException.Number);
        }

        public DiagnosticFailureKind ClassifySqlErrorNumber(int number)
        {
            if (number == -2) return DiagnosticFailureKind.Timeout;
            if (IsPermissionError(number)) return DiagnosticFailureKind.PermissionDenied;
            if (IsDatabaseError(number)) return DiagnosticFailureKind.DatabaseUnavailable;
            if (IsConnectionError(number)) return DiagnosticFailureKind.ConnectionFailure;
            return DiagnosticFailureKind.SqlError;
        }

        public string GetUserMessage(DiagnosticFailureKind kind)
        {
            switch (kind)
            {
                case DiagnosticFailureKind.PermissionDenied: return "The connected account does not have a permission required by this diagnostic.";
                case DiagnosticFailureKind.Timeout: return "The diagnostic exceeded its configured command timeout.";
                case DiagnosticFailureKind.DatabaseUnavailable: return "The selected database is unavailable or cannot be opened by the connected account.";
                case DiagnosticFailureKind.ConnectionFailure: return "The SQL Server connection failed. Verify the server, authentication, network, and certificate settings.";
                case DiagnosticFailureKind.Cancellation: return "The diagnostic was cancelled.";
                default: return "SQL Server could not execute this diagnostic. Review the sanitized application log and required permissions.";
            }
        }

        private static bool IsPermissionError(int number) { return number == 229 || number == 230 || number == 262 || number == 297 || number == 300 || number == 15151; }
        private static bool IsDatabaseError(int number) { return number == 911 || number == 916 || number == 924 || number == 926 || number == 942 || number == 4060; }
        private static bool IsConnectionError(int number) { return number == -1 || number == 2 || number == 20 || number == 53 || number == 64 || number == 233 || number == 18456; }
    }
}