# SQL Server Scripts & Utilities
A practical collection of SQL Server scripts for DBAs, developers and data engineers — performance tuning, index analysis, query troubleshooting, database integrity and schema health checks.

---

## 🗂️ Repository Structure

```text
.
├── LICENSE
├── README.md
├── performance/
│   ├── missing_indexes.sql
│   ├── index_analysis.sql
│   └── query_plan_candidates.sql
├── integrity/
│   └── fk_analysis.sql
└── schema/
    └── schema_type_patterns.sql
```

---

## 📊 Script Overview

### ⚡ Performance Tuning

| Script | Description | Primary DMVs / Target |
| --- | --- | --- |
| [`missing_indexes.sql`](https://www.google.com/search?q=./performance/missing_indexes.sql) | Identifies top missing nonclustered index candidates prioritized by impact. | `sys.dm_db_missing_index_*` |
| [`index_analysis.sql`](https://www.google.com/search?q=./performance/index_analysis.sql) | Detects duplicate indexes, unused indexes, and physical fragmentation. | `sys.dm_db_index_usage_stats`, `sys.dm_db_index_physical_stats` |
| [`query_plan_candidates.sql`](https://www.google.com/search?q=./performance/query_plan_candidates.sql) | Analyzes plan cache for implicit conversions, tempdb spills, and heavy scans. | `sys.dm_exec_query_stats`, `sys.dm_exec_query_plan` |

### 🔒 Integrity & Constraints

| Script | Description | Primary DMVs / Target |
| --- | --- | --- |
| [`fk_analysis.sql`](https://www.google.com/search?q=./integrity/fk_analysis.sql) | Identifies unindexed, untrusted, or disabled Foreign Key constraints. | `sys.foreign_keys`, `sys.indexes` |

### 🏗️ Schema & Data Types

| Script | Description | Primary DMVs / Target |
| --- | --- | --- |
| [`schema_type_patterns.sql`](https://www.google.com/search?q=./schema/schema_type_patterns.sql) | Flags tables lacking Primary Keys, large Heaps, and legacy/unbounded LOB types. | `sys.tables`, `sys.columns`, `sys.types` |

---
## 🚀 Getting Started
Feel free to clone this repository or copy individual scripts into SQL Server Management Studio (SSMS) or Azure Data Studio. Always test scripts in a non-production environment before applying them to live databases.
