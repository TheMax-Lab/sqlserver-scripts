using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Core.Interfaces
{
    public interface ISqlServerService
    {
        Task<SqlServerInfo> GetServerInfoAsync(DatabaseConnection connection, CancellationToken cancellationToken);
        Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(DatabaseConnection connection, CancellationToken cancellationToken);
        Task<DatabaseInfo> GetDatabaseInfoAsync(DatabaseConnection connection, CancellationToken cancellationToken);
        Task<SqlQueryResult> ExecuteQueryAsync(DatabaseConnection connection, string commandText, IReadOnlyCollection<SqlQueryParameter> parameters, int commandTimeoutSeconds, int maximumRowsPerResultSet, CancellationToken cancellationToken);
        Task<SqlQueryResult> ExecuteQueryWithMultipleResultsAsync(DatabaseConnection connection, string commandText, IReadOnlyCollection<SqlQueryParameter> parameters, int commandTimeoutSeconds, int maximumRowsPerResultSet, CancellationToken cancellationToken);
        Task<object> ExecuteScalarAsync(DatabaseConnection connection, string commandText, IReadOnlyCollection<SqlQueryParameter> parameters, int commandTimeoutSeconds, CancellationToken cancellationToken);
        Task<int> ExecuteNonQueryAsync(DatabaseConnection connection, string commandText, IReadOnlyCollection<SqlQueryParameter> parameters, int commandTimeoutSeconds, CancellationToken cancellationToken);
    }
}