# SQL Server Database Integrity Diagnostics

SQL Server and T-SQL diagnostics for identifying potential Foreign Key and referential integrity issues.

These scripts are designed to help DBAs and database developers identify objects that may require further investigation.

## Scripts

| Script | Purpose |
|---|---|
| [`fk_analysis.sql`](fk_analysis.sql) | Analyzes Foreign Keys, supporting indexes, disabled constraints, and untrusted constraints |

## `fk_analysis.sql`

Analyzes Foreign Key constraints in the current database.

The script can identify:

- Foreign Keys without supporting indexes
- Disabled Foreign Keys
- Untrusted Foreign Keys
- Potential referential integrity issues

### Why Foreign Key indexes matter

Foreign Keys can have performance implications when SQL Server validates relationships between parent and child tables.

Supporting indexes may be particularly important for workloads involving:

- `DELETE`
- `UPDATE`
- large parent tables
- large child tables
- frequent referential integrity checks

### Important

A missing Foreign Key index is not automatically a performance problem.

Review the workload, existing indexes, table size, query patterns, and maintenance strategy before creating a new index.

[View script](fk_analysis.sql)

## Recommended Workflow

1. Run the analysis.
2. Review the Foreign Key findings.
3. Check existing indexes.
4. Consider workload and table size.
5. Test any proposed changes.
6. Validate the impact before production deployment.

[← Back to the main README](../README.md)
