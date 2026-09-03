# SQL Server Database Integrity Scripts

T-SQL diagnostics for foreign-key indexing, constraint state, trust, and orphaned data. These scripts report evidence and do not repair user data automatically.

## Scripts

| Script | Cost | Purpose |
|---|---|---|
| [`fk_analysis.sql`](fk_analysis.sql) | Low | Finds foreign keys without a supporting index and reports disabled or untrusted keys. |
| [`orphaned_records.sql`](orphaned_records.sql) | Potentially high | Scans every user foreign-key relationship for child rows without a matching parent. |
| [`untrusted_constraints.sql`](untrusted_constraints.sql) | Low | Reports disabled or untrusted foreign-key and check constraints with validation SQL. |

## Recommended workflow

1. Run `fk_analysis.sql` to inspect foreign-key support and state.
2. Run `untrusted_constraints.sql` to identify constraints the optimizer cannot trust.
3. Run `orphaned_records.sql` in a controlled window when validation is required.
4. Investigate application rules, replication behavior, loading processes, and historical data fixes.
5. Correct data only through an approved, tested remediation plan.
6. Re-enable and validate constraints only after confirming existing rows satisfy them.

## Important considerations

- A foreign key does not always need its own index; consider table size, workload, column order, and existing indexes.
- An untrusted constraint can be enabled while still not trusted. Validation may scan and lock large tables.
- `orphaned_records.sql` uses quoted dynamic object names and read-only `COUNT_BIG` queries, but can perform substantial I/O.
- Never delete orphaned rows automatically. The correct action may be restoring parent data, correcting child data, or documenting an intentional exception.

Review the [compatibility guide](../docs/COMPATIBILITY.md) and each script header before production use.

[← Main README](../README.md)