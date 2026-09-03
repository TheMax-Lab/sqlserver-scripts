# SQL Server Diagnostic Scripts

Read-only T-SQL diagnostics for SQL Server database health, blocking, active requests, transactions, memory, TempDB, configuration, and wait statistics.

## Scripts

| Script | Scope | Purpose |
|---|---|---|
| [`blocking_sessions.sql`](blocking_sessions.sql) | Current database / instance sessions | Shows blocked requests, direct blockers, waits, SQL text, and session context. |
| [`database_configuration.sql`](database_configuration.sql) | Current database | Reviews database options that commonly deserve DBA attention. |
| [`database_health.sql`](database_health.sql) | Current database + `msdb` | Checks configuration, backup history, and transaction log conditions. |
| [`long_running_queries.sql`](long_running_queries.sql) | Current database / active requests | Finds requests running for at least 60 seconds and reports resource use. |
| [`memory_pressure.sql`](memory_pressure.sql) | Instance | Summarizes SQL Server/OS memory signals and top memory clerks. |
| [`open_transactions.sql`](open_transactions.sql) | Current database / instance sessions | Finds old or sleeping transactions, log use, blocking, and SQL text. |
| [`tempdb_usage.sql`](tempdb_usage.sql) | Instance / TempDB | Reports TempDB allocation and its highest-consuming sessions. |
| [`wait_stats.sql`](wait_stats.sql) | Instance | Ranks meaningful cumulative waits by time and percentage. |

## Common investigations

- **Blocking:** run `blocking_sessions.sql`, then correlate blockers with `open_transactions.sql`.
- **General slowness:** begin with `database_health.sql` and `wait_stats.sql`, then choose a focused performance script.
- **Memory symptoms:** compare `memory_pressure.sql` with [`../performance/memory_grants.sql`](../performance/memory_grants.sql).
- **TempDB pressure:** use `tempdb_usage.sql` to separate file, version-store, internal-object, and user-session consumption.

## Important considerations

- Instance-level DMV visibility normally requires `VIEW SERVER STATE`; SQL Server 2022 and later may require `VIEW SERVER PERFORMANCE STATE`.
- DMV results are snapshots or cumulative values and can reset after restart, failover, or other engine events.
- `database_health.sql` reads backup history from `msdb`; this is not supported in the same way by Azure SQL Database.
- No returned rows can mean no threshold was exceeded; it does not prove that the database is healthy.

Review the [compatibility guide](../docs/COMPATIBILITY.md) and each script header before production use.

[← Main README](../README.md)