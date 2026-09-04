using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Infrastructure.Connections
{
    public sealed class SqlServerService : ISqlServerService
    {
        private const int MetadataTimeoutSeconds = 15;
        private readonly SecureConnectionStringFactory connectionStringFactory;

        public SqlServerService(SecureConnectionStringFactory connectionStringFactory)
        {
            this.connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException("connectionStringFactory");
        }

        public async Task<SqlServerInfo> GetServerInfoAsync(DatabaseConnection connection, CancellationToken cancellationToken)
        {
            const string sql = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')), CONVERT(nvarchar(128), SERVERPROPERTY('Edition')), CONVERT(nvarchar(128), SERVERPROPERTY('ProductLevel')), CONVERT(int, SERVERPROPERTY('EngineEdition'));";
            string connectionString = await connectionStringFactory.CreateAsync(connection, cancellationToken).ConfigureAwait(false);
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var command = CreateCommand(sqlConnection, sql, MetadataTimeoutSeconds, null))
            {
                await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("SQL Server did not return server metadata.");
                    string serverName = reader.IsDBNull(0) ? connection.ServerName : reader.GetString(0);
                    string productVersion = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    string edition = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    string productLevel = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    int engineEdition = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                    return new SqlServerInfo
                    {
                        ServerName = serverName,
                        ProductVersion = productVersion,
                        MajorVersion = SqlServerVersion.Parse(productVersion).Major,
                        Edition = edition,
                        ProductLevel = productLevel,
                        EngineType = MapEngineType(engineEdition)
                    };
                }
            }
        }

        public async Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(DatabaseConnection connection, CancellationToken cancellationToken)
        {
            const string sql = "SELECT database_id, name, compatibility_level, state_desc, CONVERT(bit, HAS_DBACCESS(name)) FROM sys.databases WHERE HAS_DBACCESS(name) = 1 ORDER BY name;";
            var masterConnection = CloneForDatabase(connection, "master");
            string connectionString = await connectionStringFactory.CreateAsync(masterConnection, cancellationToken).ConfigureAwait(false);
            var databases = new List<DatabaseInfo>();
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var command = CreateCommand(sqlConnection, sql, MetadataTimeoutSeconds, null))
            {
                await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        databases.Add(new DatabaseInfo { Id = reader.GetInt32(0), Name = reader.GetString(1), CompatibilityLevel = Convert.ToInt32(reader.GetValue(2)), State = reader.GetString(3), IsAccessible = reader.GetBoolean(4), IsQueryStoreEnabled = false });
                    }
                }
            }
            return databases.AsReadOnly();
        }

        public async Task<DatabaseInfo> GetDatabaseInfoAsync(DatabaseConnection connection, CancellationToken cancellationToken)
        {
            const string sql = "SELECT d.database_id, d.name, d.compatibility_level, d.state_desc, CONVERT(bit, HAS_DBACCESS(d.name)), CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM sys.database_query_store_options WHERE actual_state_desc IN (N'READ_WRITE', N'READ_ONLY')) THEN 1 ELSE 0 END) FROM sys.databases AS d WHERE d.database_id = DB_ID();";
            string connectionString = await connectionStringFactory.CreateAsync(connection, cancellationToken).ConfigureAwait(false);
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var command = CreateCommand(sqlConnection, sql, MetadataTimeoutSeconds, null))
            {
                await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("SQL Server did not return database metadata.");
                    return new DatabaseInfo { Id = reader.GetInt32(0), Name = reader.GetString(1), CompatibilityLevel = Convert.ToInt32(reader.GetValue(2)), State = reader.GetString(3), IsAccessible = reader.GetBoolean(4), IsQueryStoreEnabled = reader.GetBoolean(5) };
                }
            }
        }

        public Task<SqlQueryResult> ExecuteQueryAsync(DatabaseConnection connection, string commandText, IReadOnlyCollection<SqlQueryParameter> parameters, int commandTimeoutSeconds, int maximumRowsPerResultSet, CancellationToken cancellationToken)
        {
            return ExecuteQueryWithMultipleResultsAsync(connection, commandText, parameters, commandTimeoutSeconds, maximumRowsPerResultSet, cancellationToken);
        }

        public async Task<SqlQueryResult> ExecuteQueryWithMultipleResultsAsync(DatabaseConnection connection, string commandText, IReadOnlyCollection<SqlQueryParameter> parameters, int commandTimeoutSeconds, int maximumRowsPerResultSet, CancellationToken cancellationToken)
        {
            ValidateCommand(commandText, commandTimeoutSeconds);
            if (maximumRowsPerResultSet <= 0) throw new ArgumentOutOfRangeException("maximumRowsPerResultSet");
            string connectionString = await connectionStringFactory.CreateAsync(connection, cancellationToken).ConfigureAwait(false);
            var result = new SqlQueryResult();
            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = CreateCommand(sqlConnection, commandText, commandTimeoutSeconds, parameters))
                {
                    await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using (SqlDataReader reader = await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false))
                    {
                        int resultSetIndex = 0;
                        do
                        {
                            if (reader.FieldCount <= 0) continue;
                            var resultSet = new DiagnosticResultSet { Index = resultSetIndex++, Name = "Result " + resultSetIndex };
                            var columnKeys = new string[reader.FieldCount];
                            for (int index = 0; index < reader.FieldCount; index++)
                            {
                                string name = reader.GetName(index);
                                string key = GetUniqueColumnKey(columnKeys, index, name);
                                columnKeys[index] = key;
                                resultSet.Columns.Add(new DiagnosticColumn { Name = name, Key = key, DataTypeName = reader.GetDataTypeName(index), DataType = reader.GetFieldType(index), Ordinal = index });
                            }
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                resultSet.RowsRead++;
                                if (resultSet.Rows.Count >= maximumRowsPerResultSet) { resultSet.IsTruncated = true; continue; }
                                var row = new Dictionary<string, object>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
                                for (int index = 0; index < reader.FieldCount; index++) row[columnKeys[index]] = reader.IsDBNull(index) ? null : reader.GetValue(index);
                                resultSet.Rows.Add(row);
                            }
                            result.ResultSets.Add(resultSet);
                        }
                        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));
                    }
                }
            }
            catch (SqlException exception) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException("The SQL command was cancelled.", exception, cancellationToken); }
            return result;
        }

        public async Task<object> ExecuteScalarAsync(DatabaseConnection connection, string commandText, IReadOnlyCollection<SqlQueryParameter> parameters, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            ValidateCommand(commandText, commandTimeoutSeconds);
            string connectionString = await connectionStringFactory.CreateAsync(connection, cancellationToken).ConfigureAwait(false);
            try { using (var sqlConnection = new SqlConnection(connectionString)) using (var command = CreateCommand(sqlConnection, commandText, commandTimeoutSeconds, parameters)) { await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false); using (cancellationToken.Register(command.Cancel)) return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false); } }
            catch (SqlException exception) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException("The SQL command was cancelled.", exception, cancellationToken); }
        }

        public async Task<int> ExecuteNonQueryAsync(DatabaseConnection connection, string commandText, IReadOnlyCollection<SqlQueryParameter> parameters, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            ValidateCommand(commandText, commandTimeoutSeconds);
            string connectionString = await connectionStringFactory.CreateAsync(connection, cancellationToken).ConfigureAwait(false);
            try { using (var sqlConnection = new SqlConnection(connectionString)) using (var command = CreateCommand(sqlConnection, commandText, commandTimeoutSeconds, parameters)) { await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false); using (cancellationToken.Register(command.Cancel)) return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); } }
            catch (SqlException exception) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException("The SQL command was cancelled.", exception, cancellationToken); }
        }

        private static async Task<SqlDataReader> ExecuteReaderAsync(SqlCommand command, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(command.Cancel)) return await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
        }

        private static SqlCommand CreateCommand(SqlConnection connection, string text, int timeout, IReadOnlyCollection<SqlQueryParameter> parameters)
        {
            var command = new SqlCommand(text, connection) { CommandType = CommandType.Text, CommandTimeout = timeout };
            if (parameters != null)
            {
                foreach (SqlQueryParameter parameter in parameters)
                {
                    if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name)) throw new ArgumentException("Every SQL parameter requires a name.", "parameters");
                    SqlParameter sqlParameter = parameter.Size.HasValue ? command.Parameters.Add(parameter.Name, parameter.Type, parameter.Size.Value) : command.Parameters.Add(parameter.Name, parameter.Type);
                    sqlParameter.Value = parameter.Value ?? DBNull.Value;
                }
            }
            return command;
        }

        private static string GetUniqueColumnKey(string[] previousKeys, int currentIndex, string name)
        {
            string baseName = string.IsNullOrWhiteSpace(name) ? "Column" + currentIndex : name;
            string candidate = baseName;
            int suffix = 2;
            for (int index = 0; index < currentIndex; index++)
            {
                if (!string.Equals(previousKeys[index], candidate, StringComparison.OrdinalIgnoreCase)) continue;
                candidate = baseName + "_" + suffix++;
                index = -1;
            }
            return candidate;
        }

        private static void ValidateCommand(string commandText, int timeout)
        {
            if (string.IsNullOrWhiteSpace(commandText)) throw new ArgumentException("SQL command text is required.", "commandText");
            if (timeout <= 0) throw new ArgumentOutOfRangeException("timeout");
        }

        private static DatabaseConnection CloneForDatabase(DatabaseConnection source, string database)
        {
            return new DatabaseConnection { ServerName = source.ServerName, DatabaseName = database, AuthenticationType = source.AuthenticationType, UserName = source.UserName, CredentialKey = source.CredentialKey, Encrypt = source.Encrypt, TrustServerCertificate = source.TrustServerCertificate, ConnectionTimeoutSeconds = source.ConnectionTimeoutSeconds };
        }

        private static SqlServerEngineType MapEngineType(int engineEdition)
        {
            if (engineEdition == 5) return SqlServerEngineType.AzureSqlDatabase;
            if (engineEdition == 8) return SqlServerEngineType.AzureSqlManagedInstance;
            return engineEdition > 0 ? SqlServerEngineType.SqlServer : SqlServerEngineType.Unknown;
        }
    }
}