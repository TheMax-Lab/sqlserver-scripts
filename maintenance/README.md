# SQL Server Maintenance Scripts

Read-only T-SQL diagnostics for SQL Server backups, database and file capacity, index fragmentation, and statistics maintenance.

## Scripts

| Script | Scope | Purpose |
|---|---|---|
| [`backup_health.sql`](backup_health.sql) | Instance + `msdb` | Reviews the latest non-copy-only full, differential, and log backups. |
| [`database_sizes.sql`](database_sizes.sql) | Current database | Reports allocated file size, data-file free space, log use, maximum size, and autogrowth. |
| [`file_space.sql`](file_space.sql) | Current database | Highlights low data-file free space and percentage or small fixed autogrowth. |
| [`fragmentation.sql`](fragmentation.sql) | Current database | Finds fragmented rowstore indexes and generates `REORGANIZE` or `REBUILD` candidates. |
| [`statistics.sql`](statistics.sql) | Current database | Finds uninitialized or highly modified statistics and generates update commands. |

## Recommended workflow

- Use `backup_health.sql` as a history check, then validate jobs, media, retention, encryption, and restore tests.
- Use `database_sizes.sql` for the fuller file/log report; use `file_space.sql` for a compact capacity and growth review.
- Correlate `fragmentation.sql` with workload, page count, page density, storage behavior, and maintenance windows.
- Correlate `statistics.sql` with query regressions and cardinality estimates; age alone does not make statistics stale.

## Important considerations

- `msdb` history can be absent, purged, or bypassed by some third-party/VSS tooling. Backup history is not proof of recoverability.
- `file_space.sql` calculates used/free space for data files; use `database_sizes.sql` for database-wide transaction-log utilization.
- Generated `ALTER INDEX`, `UPDATE STATISTICS`, and file-growth statements are returned as text only.
- Index rebuilds and statistics updates can consume CPU, I/O, TempDB, log space, and blocking time.
- Azure SQL manages backups and files differently; check the platform notes before use.

Review the [compatibility guide](../docs/COMPATIBILITY.md) and each script header before production use.

[← Main README](../README.md)