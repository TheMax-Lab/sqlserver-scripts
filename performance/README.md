# SQL Server Performance Diagnostics

SQL Server and T-SQL scripts for investigating query performance, indexes, execution plans, CPU usage, logical reads, and other common performance-related issues.

These scripts are designed for **diagnostics and investigation**. They do not automatically modify your database.

## Scripts

| Script | Purpose |
|---|---|
| [`missing_indexes.sql`](missing_indexes.sql) | Finds potential missing nonclustered indexes |
| [`index_analysis.sql`](index_analysis.sql) | Analyzes index usage, duplicates, and fragmentation |
| [`query_plan_candidates.sql`](query_plan_candidates.sql) | Finds potentially expensive queries and execution-plan patterns |

## `missing_indexes.sql`

Identifies potential missing nonclustered indexes using SQL Server missing-index DMVs.

Useful for:

- identifying high-impact index candidates
- reviewing equality and inequality columns
- analyzing included columns
- understanding missing-index recommendations

> Missing-index DMVs provide recommendations, not guaranteed solutions.

Always validate proposed indexes against the existing indexing strategy and real workload.

[View script](missing_indexes.sql)

## `index_analysis.sql`

Analyzes nonclustered indexes for:

- index usage
- duplicate definitions
- fragmentation
- read/write activity
- maintenance candidates

The script does not automatically drop indexes.

An apparently unused index may still be required by an infrequent workload, reporting process, maintenance task, or other database operation.

[View script](index_analysis.sql)

## `query_plan_candidates.sql`

Searches the SQL Server plan cache for potentially problematic execution patterns, including:

- implicit conversions
- TempDB spills
- expensive scans
- high CPU usage
- high logical reads

The results should be treated as candidates for further investigation.

[View script](query_plan_candidates.sql)

## Recommended Workflow

1. Run the diagnostic script.
2. Review the findings.
3. Validate the evidence.
4. Check the existing database design.
5. Test potential changes.
6. Measure the impact.
7. Apply changes only when justified.

## Important

These scripts inspect SQL Server metadata and runtime information. Some DMV data is transient and may be reset after a SQL Server restart or other events.

Always test changes before applying them to production.

[← Back to the main README](../README.md)
