# SQL Server Scripts & Utilities
A curated collection of SQL Server scripts, T-SQL utilities, and administrative tools for database management, performance tuning, and maintenance.

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

## 🔍 Detailed Script Information

### missing_indexes.sql
Queries Dynamic Management Views (DMVs) to identify top missing nonclustered index candidates for the current database, prioritized by estimated performance impact.

* **Key Features:**
  * **Priority Calculation:** Categorizes index recommendations as `High`, `Medium`, or `Low` based on seeking activity, user impact percentage, and average query cost.
  * **Automated DDL Generation:** Provides a ready-to-use `CREATE NONCLUSTERED INDEX` statement (`SuggestedSql`) including equality, inequality, and included columns.
  * **Risk Assessment:** Outlines potential trade-offs (storage growth, write overhead for DML statements) before applying recommendations.
  * **Execution Evidence:** Shows total seeks, scans, impact percentage, and last seek timestamp.

> ⚠️ **Note:** Missing index DMVs reset upon SQL Server restart. Always validate suggested indexes against existing index strategies and test them under full workload conditions before creating them in production.

### index_analysis.sql
Evaluates nonclustered index health across three key areas: usage metrics, duplicate key definitions, and physical fragmentation levels.

* **Key Features:**
  * **Duplicate Detection:** Identifies indexes that share identical key column signatures to help eliminate redundant write overhead.
  * **Unused Index Identification:** Highlights write-heavy indexes that receive zero seek, scan, or lookup operations.
  * **Fragmentation Analysis:** Scans physical fragmentation using `LIMITED` mode and generates target maintenance commands (`REORGANIZE` or `REBUILD`).
  * **Safety First:** Does **not** auto-generate `DROP` statements for unused or duplicate indexes, forcing a manual review to prevent accidental removal of constraints or periodic workload dependencies.

> ⚠️ **Note:** Usage statistics (`sys.dm_db_index_usage_stats`) reset on SQL Server service restarts. Ensure the instance has been running under normal workload conditions before acting on unused index findings.

### query_plan_candidates.sql
Searches the query plan cache for resource-intensive queries exhibiting performance anti-patterns.

* **Key Features:**
  * **Anti-Pattern Detection:** Flags implicit data type conversions (`CONVERT_IMPLICIT`), TempDB spills (`SpillToTempDb`), and costly scans.
  * **Metric Aggregation:** Displays total executions, average CPU time (ms), and average logical reads.
  * **Safe Recommendations:** Does not auto-generate code fixes, encouraging manual execution plan XML inspection.

> ⚠️ **Note:** Plan cache statistics are transient and cleared during service restarts or memory pressure.


### fk_analysis.sql
Inspects Foreign Keys across the database to detect coverage, integrity, and status issues.

* **Key Features:**
  * **Unindexed FK Detection:** Finds Foreign Keys that lack a supporting index with matching leading columns (reducing child table lock contention).
  * **Constraint Validation Check:** Identifies disabled constraints or untrusted constraints (`WITH NOCHECK`).
  * **Auto-Fix Generation:** Supplies `ALTER TABLE ... WITH CHECK CHECK CONSTRAINT` statements for untrusted or disabled FKs.



### schema_type_patterns.sql
Scans database schema design to identify potential structural issues impacting performance or data integrity.

* **Key Features:**
  * **Missing Primary Keys:** Flags tables without a defined Primary Key.
  * **Large Heaps:** Highlights tables without a Clustered Index that exceed 10,000 rows.
  * **Legacy & Unbounded LOB Types:** Flags columns using deprecated types (`text`, `ntext`, `image`) or unbounded max length types (`varchar(max)`, `nvarchar(max)`, `varbinary(max)`).

---
## 🚀 Getting Started
Feel free to clone this repository or copy individual scripts into SQL Server Management Studio (SSMS) or Azure Data Studio. Always test scripts in a non-production environment before applying them to live databases.
