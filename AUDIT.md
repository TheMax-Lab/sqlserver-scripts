# Repository Validation Report

Validation date: 2026-09-03

## Summary

The repository contains 26 focused SQL Server scripts across five categories. Every script has an English metadata header, a documented purpose, scope, supported SQL Server baseline, Azure SQL note, permissions, risk, output, author, version, and license.

| Area | Status |
|---|---|
| Root catalog | Complete; all 26 scripts are linked and described |
| Category catalogs | Complete for diagnostics, performance, integrity, maintenance, and schema |
| Header consistency | Standardized across all scripts |
| Default behavior | Read-only for user databases |
| Generated corrective SQL | Returned as text; not automatically executed |
| Compatibility guidance | Documented in [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md) |
| Contribution template | Available in [`docs/SCRIPT_TEMPLATE.sql`](docs/SCRIPT_TEMPLATE.sql) |
| Repository image | `sqlserver-scripts.jpg`, linked from the root README |

## Validation performed

- Inventoried all SQL and Markdown files.
- Checked that every `.sql` file is listed in the root and category catalogs.
- Checked local Markdown links and image references.
- Checked required SQL header fields and filename/header agreement.
- Reviewed parentheses, comments, strings, `BEGIN`/`END`, dynamic SQL, and mutating statement patterns.
- Reviewed SQL Server version-sensitive DMVs and documented permission changes introduced in SQL Server 2022.
- Confirmed that generated DDL and maintenance commands are emitted as text rather than executed.
- Confirmed that `orphaned_records.sql` modifies only a local temporary table while dynamically executing read-only orphan counts.
- Confirmed the repository image is a valid JPEG asset.

## Important limitation

Static review cannot prove runtime behavior for every SQL Server edition, compatibility level, collation, permission model, database state, or Azure service tier. No SQL Server Database Engine instance is bundled with this repository. Before production use, compile and execute each selected script on a representative non-production instance and inspect its actual execution plan and runtime impact.

## Safety observations

- Catalog and compact DMV checks are generally low cost.
- Cached XML plan searches, Query Store aggregation, and physical-statistics scans can have noticeable cost.
- `orphaned_records.sql` can scan every participating child table and should run in a controlled window.
- Missing-index, index-removal, constraint-validation, statistics, fragmentation, and file-growth suggestions require DBA review and workload testing.
- Backup history reports recency, not recoverability; scheduled restore testing remains essential.

## Recommended future coverage

- Deadlock Extended Events reader
- Transaction log reuse and VLF health
- SQL Server Agent failure and duration diagnostics
- Availability Group health checks
- Security and permission auditing

[← Main README](README.md)