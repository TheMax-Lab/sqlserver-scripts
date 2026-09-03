# SQL Server Schema Analysis Scripts

Read-only T-SQL checks for primary keys, heaps, forwarded records, deprecated data types, and unbounded large-object columns. Findings are design-review candidates, not universal rules.

## Scripts

| Script | Cost | Purpose |
|---|---|---|
| [`heap_analysis.sql`](heap_analysis.sql) | Potentially high | Assesses heap size, forwarded records, page density, extent fragmentation, and nonclustered indexes. |
| [`missing_primary_keys.sql`](missing_primary_keys.sql) | Low | Finds user tables without primary keys and reports rows, storage, unique indexes, and incoming foreign keys. |
| [`schema_type_patterns.sql`](schema_type_patterns.sql) | Low | Provides a broad scan for missing keys, large heaps, deprecated types, and `MAX` columns. |

## What the findings mean

- **Missing primary key:** may affect identity, relationships, tooling, and maintainability, but staging or append-only designs can be intentional.
- **Heap:** can be appropriate for some loading patterns; forwarded records and access patterns are stronger evidence than heap status alone.
- **Deprecated type:** `text`, `ntext`, and `image` deserve modernization planning and application compatibility testing.
- **Unbounded type:** `varchar(max)`, `nvarchar(max)`, and `varbinary(max)` are valid types, but should match real data and query requirements.

Run `schema_type_patterns.sql` for broad discovery, then use `heap_analysis.sql` or `missing_primary_keys.sql` for deeper evidence. Physical statistics can be expensive on large databases, even in `SAMPLED` mode.

Review the [compatibility guide](../docs/COMPATIBILITY.md) and each script header before production use.

[← Main README](../README.md)