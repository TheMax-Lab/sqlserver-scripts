# SQL Server Performance Tuning Scripts

Read-only T-SQL scripts for SQL Server query performance, CPU, logical reads, execution plans, memory grants, Query Store, missing indexes, index usage, and fragmentation.

## Scripts

| Script | Data source | Purpose |
|---|---|---|
| [`expensive_queries.sql`](expensive_queries.sql) | Plan cache | Ranks cached queries by average and cumulative elapsed time, CPU, reads, and writes. |
| [`high_cpu_queries.sql`](high_cpu_queries.sql) | Plan cache | Focuses on statements with high average or cumulative CPU consumption. |
| [`index_analysis.sql`](index_analysis.sql) | Catalog + index DMVs | Correlates usage, duplicate keys, size, and physical fragmentation. |
| [`memory_grants.sql`](memory_grants.sql) | Active grant DMVs | Finds waiting, large, and potentially underused query memory grants. |
| [`missing_indexes.sql`](missing_indexes.sql) | Missing-index DMVs | Ranks index candidates and returns reviewable `CREATE INDEX` statements. |
| [`query_plan_candidates.sql`](query_plan_candidates.sql) | Cached XML plans | Finds implicit conversions, TempDB spills, and scans in costly statements. |
| [`query_store_regressions.sql`](query_store_regressions.sql) | Query Store | Compares recent average duration with an earlier weighted baseline. |

## Choosing a script

- Use `high_cpu_queries.sql` for CPU-specific investigations.
- Use `expensive_queries.sql` when elapsed time, reads, writes, or CPU may be responsible.
- Use `query_plan_candidates.sql` to search common plan symptoms; confirm every match in the full plan.
- Use `query_store_regressions.sql` when persistent history is available and a workload changed over time.
- Use `memory_grants.sql` for `RESOURCE_SEMAPHORE`, workspace-memory, sort, or hash concerns.
- Review `index_analysis.sql` before acting on `missing_indexes.sql` so candidates do not duplicate existing indexes.

## Limitations and safety

- Plan-cache and missing-index statistics reset after restart, failover, cache eviction, recompilation, and other events.
- Query Store results depend on capture policy, retention, aggregation intervals, and available history.
- XML plan searches and physical-statistics scans can consume noticeable resources on busy or large systems.
- An unused, duplicate, fragmented, or suggested index is not automatically a change recommendation.
- Test every generated statement against read performance, write overhead, storage, maintenance, and deployment constraints.

Review the [compatibility guide](../docs/COMPATIBILITY.md) and each script header before production use.

[← Main README](../README.md)