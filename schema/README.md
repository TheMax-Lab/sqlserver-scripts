# SQL Server Schema Diagnostics

T-SQL scripts for identifying database schema patterns that may deserve further investigation.

The goal is not to enforce a single database design, but to highlight potentially interesting objects and patterns.

## Scripts

| Script | Purpose |
|---|---|
| [`schema_type_patterns.sql`](schema_type_patterns.sql) | Identifies schema patterns involving Primary Keys, heaps, legacy data types, and MAX data types |

## `schema_type_patterns.sql`

Analyzes SQL Server metadata to identify potentially interesting schema patterns.

### Current checks

#### Missing Primary Keys

Identifies tables without a defined Primary Key.

A table without a Primary Key is not necessarily incorrect, but it may deserve review depending on its purpose and workload.

#### Large Heaps

Identifies tables without a clustered index above the configured row threshold.

Heaps can be appropriate for certain workloads, so findings should be evaluated in context.

#### Legacy Data Types

Identifies deprecated SQL Server data types such as:

- `text`
- `ntext`
- `image`

These types may be candidates for modernization.

#### MAX Data Types

Identifies columns using:

- `varchar(max)`
- `nvarchar(max)`
- `varbinary(max)`

These data types are not inherently problematic, but their usage may deserve review depending on the application and data model.

## Important

The script reports **patterns and candidates for investigation**.

It does not automatically classify a schema design as correct or incorrect.

Database design decisions should consider:

- application requirements
- workload
- data size
- query patterns
- compatibility requirements
- SQL Server version

[View script](schema_type_patterns.sql)

[← Back to the main README](../README.md)
