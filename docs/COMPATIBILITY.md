# SQL Server Compatibility and Permissions

This guide summarizes the supported baseline and common permissions. The metadata header in each SQL file is the script-specific source of truth.

## Supported engines

| Platform | Support level | Notes |
|---|---|---|
| SQL Server 2016–2019 | Primary baseline | Most scripts use catalog views and DMVs available in SQL Server 2016. `statistics.sql` supports SQL Server 2012 SP1+. |
| SQL Server 2022 and later | Supported | Access to several performance DMVs moved to `VIEW SERVER PERFORMANCE STATE` or `VIEW DATABASE PERFORMANCE STATE`. |
| Azure SQL Managed Instance | Generally supported | Instance DMV and `msdb` behavior is closer to SQL Server, but permissions and managed-service behavior differ. |
| Azure SQL Database | Script-dependent | Database-scoped catalog, Query Store, index, integrity, and schema scripts are the best fit. Host memory, instance waits, `msdb` history, and file management differ or are unavailable. |

## Permission patterns

| Data source | SQL Server 2019 and earlier | SQL Server 2022 and later |
|---|---|---|
| Server-scoped performance DMVs | `VIEW SERVER STATE` | `VIEW SERVER PERFORMANCE STATE` for affected DMVs |
| Database-scoped performance DMVs | `VIEW DATABASE STATE` | `VIEW DATABASE PERFORMANCE STATE` for affected DMVs |
| Catalog views | Metadata visibility on the objects | Metadata visibility on the objects |
| User-table integrity scans | `SELECT` on participating tables | `SELECT` on participating tables |
| Backup history | Read access to `msdb` backup history and `sys.databases` | Same |

Grant only the least privilege appropriate for the environment.

## Runtime data limitations

- Plan-cache, index-usage, missing-index, wait, memory-grant, session, and request DMVs are transient.
- Restarts, failovers, plan eviction, recompilation, and engine operations can reset or remove evidence.
- Query Store depends on capture mode, retention, storage quota, aggregation intervals, and cleanup.
- Permissions can filter metadata or DMV rows, producing an incomplete result without an explicit error.
- Backup history can be purged or omitted by external tools. Only successful restore testing demonstrates recoverability.

## Cost classifications

- **Low:** catalog lookup or compact DMV query.
- **Medium:** broader DMV aggregation, plan XML inspection, or limited physical-statistics access.
- **Potentially high:** table scans, large Query Store aggregation, or sampled physical-statistics work.

Run medium- and high-cost diagnostics during a controlled period on large or busy systems.

## Validation status

The repository is statically reviewed for balanced SQL structure, read-only behavior, metadata consistency, quoted dynamic object names, documented permissions, and version-appropriate catalog/DMV usage. Because no SQL Server engine is bundled with the repository, compile and execute scripts against a representative non-production instance for the exact version, edition, collation, database compatibility level, and Azure service tier.

[← Main README](../README.md)