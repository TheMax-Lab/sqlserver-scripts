# Full Audit — TheMax-Lab/sqlserver-scripts

Audit date: 2026-09-03

## Executive summary

The repository has a strong foundation: the scripts are small, readable, diagnostic-first, MIT licensed, and several already use a useful common output vocabulary (`Priority`, `Category`, `Object`, `Finding`, `Evidence`, `Recommendation`, `SuggestedSql`, `Risk`).

The main weaknesses are not the core idea, but **coverage, consistency, documentation drift, contribution infrastructure, compatibility documentation, and discoverability**.

### Overall score

| Area | Score | Main finding |
|---|---:|---|
| Script usefulness | 8/10 | Good DBA-first diagnostics, practical output |
| Safety | 8.5/10 | Mostly read-only; generated corrective SQL is generally presented cautiously |
| Consistency | 6.5/10 | Headers, permissions, thresholds, naming, result shape and author spelling are not fully standardized |
| Coverage | 6/10 | Strong basics; important DBA areas are still missing |
| README / docs | 5.5/10 | Root README is useful but out of sync with the actual repository |
| GitHub project structure | 4.5/10 | Missing contribution/security/templates/CI metadata |
| Discoverability | 5/10 | Good repository name and description, but topics/readme/navigation need work |
| Growth potential | 8/10 | Clear niche, reusable scripts, easy to share/search |

## Highest-priority fixes

1. Update the root README to reflect all current folders and scripts.
2. Add README files to `diagnostics/` and `maintenance/`.
3. Add the missing scripts documented in this package:
   - `diagnostics/wait_stats.sql`
   - `diagnostics/database_configuration.sql`
   - `diagnostics/memory_pressure.sql`
   - `performance/memory_grants.sql`
   - `performance/query_store_regressions.sql`
   - `maintenance/backup_health.sql`
   - `maintenance/file_space.sql`
4. Standardize every SQL file header.
5. Add a compatibility/permissions matrix.
6. Add `.github` issue forms, PR template, `CONTRIBUTING.md`, `SECURITY.md`, `.editorconfig`, `.gitattributes`.
7. Fix the GitHub topic typo `query-opitmization` → `query-optimization`.
8. Replace the root README's old tree with an automatically maintainable script catalog.
9. Publish releases/tags after meaningful batches (for example `v1.0.0` after the structure + coverage pass).
10. Add a social preview image and an explicit “Top scripts / Start here” section.

---

# Audit of the existing 19 scripts

The classifications below focus on purpose, overlap, operational risk, documentation, likely runtime cost, and how the script fits the toolkit.

| Script | Assessment | Priority fixes |
|---|---|---|
| `diagnostics/blocking_sessions.sql` | **Strong.** Good direct-blocker context, SQL text, negative blocker handling, caution around `KILL`. | Add root-blocker chain/level in a future enhancement; document required permission; consider transaction age for blocker prioritization. |
| `diagnostics/database_health.sql` | **Useful but broad.** A good “first look” script, but large all-in-one diagnostics can become harder to reason about over time. | Make every check independently labeled; document version requirements; avoid duplicating specialist scripts too deeply. |
| `diagnostics/long_running_queries.sql` | **Useful.** Appropriate incident-response script. | Parameterize duration threshold; include wait category, blocking session, transaction age, query plan availability, and percent_complete where relevant. |
| `diagnostics/open_transactions.sql` | **High-value.** Important for log growth/blocking investigations. | Surface transaction age prominently; distinguish user/system/distributed transactions; document impact of sleeping sessions with open transactions. |
| `diagnostics/tempdb_usage.sql` | **High-value.** TempDB diagnostics are frequently needed. | Separate instance/file pressure from per-session/task usage; include file-size skew and version-store signals if not already present. |
| `performance/expensive_queries.sql` | **Strong.** Good multi-metric plan-cache triage. | Parameterize thresholds; explain that `st.dbid = DB_ID()` can omit some ad-hoc/prepared cases; reduce overlap with `high_cpu_queries.sql`. |
| `performance/high_cpu_queries.sql` | **Strong specialist view.** Good CPU/parallelism context. | Keep because it is searchable/use-case oriented, but share common CTE/threshold conventions with `expensive_queries.sql`. |
| `performance/index_analysis.sql` | **Useful but potentially expensive.** Combining usage, duplicate definitions, and physical stats is practical but can become costly on large databases. | Use `LIMITED` mode by default for physical stats; add minimum page count; document that usage DMVs reset; be conservative with “unused”. |
| `performance/missing_indexes.sql` | **Good diagnostic.** Correctly framed as candidates rather than commands. | Add duplicate/overlap suppression against existing indexes; cap included-column width; highlight DMV reset behavior and write cost. |
| `performance/query_plan_candidates.sql` | **High-value concept.** Plan-cache pattern detection is a differentiator. | Make XML predicates namespace-safe; document plan-cache limitations; consider splitting specialized detections later if the query becomes expensive. |
| `integrity/fk_analysis.sql` | **Strong.** Useful combination of FK trust/state/supporting indexes. | Ensure composite FK index matching respects ordered leading columns; include parent/child row counts as evidence. |
| `integrity/orphaned_records.sql` | **Potentially very valuable, but highest review risk.** Orphan discovery often requires dynamic SQL and can be expensive. | Keep generated checks read-only; quote identifiers with `QUOTENAME`; avoid unbounded full scans by default; document NULL semantics and FK state. |
| `integrity/untrusted_constraints.sql` | **Useful.** Good standalone searchable check even if there is overlap with FK analysis. | Include CHECK constraints as well as FKs where appropriate; emphasize validation can be expensive and can fail on existing data. |
| `maintenance/database_sizes.sql` | **Useful capacity view.** | Distinguish allocated size, used space, free space, growth settings, and log reuse reason; avoid implying file shrink as normal maintenance. |
| `maintenance/fragmentation.sql` | **Useful but must be workload-aware.** | Use page-count threshold; default to `LIMITED`; never make 5/30 percent folklore appear universal; account for columnstore separately. |
| `maintenance/statistics.sql` | **High-value.** Statistics diagnostics are more useful than blind scheduled updates. | Use modification counters where supported; document sampled/fullscan trade-offs; avoid fixed thresholds without table size context. |
| `schema/heap_analysis.sql` | **Useful.** Heaps are worth surfacing, especially large ones. | Add forwarded-record evidence where available; distinguish staging/ETL heaps from OLTP candidates. |
| `schema/missing_primary_keys.sql` | **Useful schema signal.** | Keep wording neutral; consider unique constraints/indexes as evidence before recommending a PK. |
| `schema/schema_type_patterns.sql` | **Good modernization check.** | Avoid duplicating missing-PK/heap logic if dedicated scripts now exist; focus it on data-type/schema patterns. |

## Cross-script technical findings

### 1. Standardize headers

Use the same metadata in every script:

```text
Script Name:
Purpose:
Scope: current database / instance / msdb
SQL Server:
Azure SQL:
Permissions:
Risk:
Output:
Author: TheMax-Lab
Version:
License: MIT
```

There is already inconsistent author capitalization (`TheMaxLab`, `TheMAxLab`). Standardize on **TheMax-Lab**.

### 2. Standardize thresholds

Hard-coded thresholds are fine for defaults, but they should be declared at the top:

```sql
DECLARE @MinimumDurationMs bigint = 1000;
DECLARE @MinimumPageCount bigint = 1000;
```

Benefits:
- easier reuse;
- clearer documentation;
- safer tuning;
- easier contribution review.

### 3. Clarify scope

Every script should immediately tell the user whether it is:
- current-database scoped;
- instance scoped;
- `msdb` dependent;
- Query Store dependent;
- plan-cache dependent.

This is especially important because `sys.dm_os_*` views are instance-wide while many catalog views are database-local.

### 4. Permissions matrix

Some runtime DMVs require:
- SQL Server 2019 and earlier: typically `VIEW SERVER STATE`;
- SQL Server 2022+: some performance DMVs use `VIEW SERVER PERFORMANCE STATE`.

Not every script has the same requirement, so document this per file and in `docs/COMPATIBILITY.md`.

### 5. Cost/risk labels

Add a second metadata dimension beyond business risk:

- **Query cost: Low / Medium / Potentially High**
- **Change risk: None / Generated SQL only / Potentially destructive if copied**

`sys.dm_db_index_physical_stats`, orphan scans, Query Store aggregates, XML plan searches, and cross-database checks deserve explicit cost notes.

### 6. Avoid recommendation folklore

Do not encode rules such as:
- “fragmentation > 30 = rebuild”
- “unused index = drop”
- “missing index = create”
- “heap = bad”
- “no PK = broken”

Your current README philosophy already moves in the right direction; carry that wording into every script.

---

# Coverage gap analysis

## Missing high-value areas

| Area | Why it matters | Added in this pack |
|---|---|---|
| Wait statistics | Fastest way to understand instance-level bottleneck categories | ✅ `wait_stats.sql` |
| Query Store regressions | Persistent performance history survives plan-cache churn | ✅ `query_store_regressions.sql` |
| Memory grants | Finds waiting/oversized grants and workspace-memory pressure | ✅ `memory_grants.sql` |
| Memory pressure | Helps distinguish OS/SQL memory pressure signals | ✅ `memory_pressure.sql` |
| Backup health | Core DBA operational check | ✅ `backup_health.sql` |
| Database configuration | Finds risky defaults/settings drift | ✅ `database_configuration.sql` |
| File free space / autogrowth | Capacity and incident prevention | ✅ `file_space.sql` |
| Deadlocks | Critical concurrency troubleshooting | Next recommended script |
| Log reuse / VLF health | Common log-growth/root-cause area | Next recommended script |
| Availability Groups | Important for HA estates | Optional category later |
| Agent jobs | Operational monitoring | Optional category later |
| Security/permissions | Valuable, but deserves its own carefully scoped category | Later |
| Extended Events | Powerful, but heavier than the current “small query” philosophy | Later |

---

# Repository information architecture

Recommended target:

```text
sqlserver-scripts/
├── .github/
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.yml
│   │   └── script_request.yml
│   └── pull_request_template.md
├── diagnostics/
│   ├── README.md
│   ├── blocking_sessions.sql
│   ├── database_configuration.sql
│   ├── database_health.sql
│   ├── long_running_queries.sql
│   ├── memory_pressure.sql
│   ├── open_transactions.sql
│   ├── tempdb_usage.sql
│   └── wait_stats.sql
├── performance/
│   ├── README.md
│   ├── expensive_queries.sql
│   ├── high_cpu_queries.sql
│   ├── index_analysis.sql
│   ├── memory_grants.sql
│   ├── missing_indexes.sql
│   ├── query_plan_candidates.sql
│   └── query_store_regressions.sql
├── integrity/
│   ├── README.md
│   ├── fk_analysis.sql
│   ├── orphaned_records.sql
│   └── untrusted_constraints.sql
├── maintenance/
│   ├── README.md
│   ├── backup_health.sql
│   ├── database_sizes.sql
│   ├── file_space.sql
│   ├── fragmentation.sql
│   └── statistics.sql
├── schema/
│   ├── README.md
│   ├── heap_analysis.sql
│   ├── missing_primary_keys.sql
│   └── schema_type_patterns.sql
├── docs/
│   ├── COMPATIBILITY.md
│   └── SCRIPT_TEMPLATE.sql
├── .editorconfig
├── .gitattributes
├── CONTRIBUTING.md
├── SECURITY.md
├── LICENSE
└── README.md
```

---

# Discoverability / SEO audit

## Current strengths

- Repository name contains the exact high-intent phrase `sqlserver-scripts`.
- Public MIT repository.
- Root README uses high-value terms such as SQL Server, T-SQL, DBA, performance tuning, indexes, database diagnostics.
- Folder names map naturally to user intent.

## Current weaknesses

- The live root README still shows an outdated, incomplete folder tree.
- Category README files do not cover all files.
- `diagnostics/` and `maintenance/` have no README in the live repository.
- Topic typo: `query-opitmization`.
- No release/tag signals.
- No contribution/security metadata.
- No compatibility matrix.
- No “Start here” section mapping symptoms to scripts.
- No social preview.
- No visible badge for current release / script count / license (avoid fake build badges).

## Recommended repository description

> SQL Server DBA diagnostics and T-SQL scripts for performance tuning, blocking, Query Store, indexes, TempDB, integrity, backups, statistics, schema and database health.

## Recommended GitHub topics

Use up to 20, all lowercase/hyphenated:

```text
sql-server
sqlserver
t-sql
tsql
mssql
dba
database
database-administration
database-diagnostics
database-performance
performance-tuning
query-optimization
sql-performance
indexing
query-store
tempdb
database-maintenance
database-monitoring
database-troubleshooting
sql-scripts
```

## Search-oriented README phrases to include naturally

- SQL Server scripts for DBAs
- T-SQL performance tuning scripts
- SQL Server blocking query
- SQL Server wait statistics
- SQL Server expensive queries
- SQL Server missing indexes
- SQL Server Query Store regression
- SQL Server TempDB usage
- SQL Server backup health
- SQL Server fragmentation and statistics
- SQL Server database health check

Do not stuff keywords. Use them in headings, script descriptions and problem-to-script navigation.

## Growth strategy

The strongest growth loop is **problem → searchable script → useful output → star/share**.

Create a “Find the script for your problem” table near the top of README:

| Problem | Script |
|---|---|
| SQL Server is slow | `database_health.sql`, `wait_stats.sql` |
| Blocking | `blocking_sessions.sql`, `open_transactions.sql` |
| High CPU | `high_cpu_queries.sql` |
| Slow query regression | `query_store_regressions.sql` |
| TempDB pressure | `tempdb_usage.sql`, `memory_grants.sql` |
| Backup risk | `backup_health.sql` |
| Index questions | `index_analysis.sql`, `missing_indexes.sql` |

This is more discoverable and more useful than a pure folder tree.

---

# Release plan

## v1.0.0 — Foundation
- Complete README catalog
- Category READMEs
- 7 new scripts
- Compatibility matrix
- Contribution/security/templates
- Topic cleanup
- Social preview

## v1.1.0 — Concurrency & log
- `deadlock_xe_reader.sql`
- `log_reuse_and_vlf.sql`
- blocking-chain/root-blocker enhancement

## v1.2.0 — Operational DBA
- Agent job failures/duration
- database owner/trustworthy checks
- recovery model / backup policy enhancements
- optional AG diagnostics

## v2.0.0 — Curated toolkit
- stable output contract
- automated lint/docs checks
- tested compatibility matrix
- release notes and changelog
