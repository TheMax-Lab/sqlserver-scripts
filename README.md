# sqlserver-scripts
# SQL Server Scripts & Utilities
A curated collection of SQL Server scripts, T-SQL utilities, and administrative tools for database management, performance tuning, and maintenance.


## 📊 Script Overview

### ⚡ Performance Tuning

| Script | Description | Primary DMVs / Target |
| --- | --- | --- |
| [`missing_indexes.sql`](https://www.google.com/search?q=./performance/missing_indexes.sql) | Identifies top missing nonclustered index candidates prioritized by impact. | `sys.dm_db_missing_index_*` |
| [`index_analysis.sql`](https://www.google.com/search?q=./performance/index_analysis.sql) | Detects duplicate indexes, unused indexes, and physical fragmentation. | `sys.dm_db_index_usage_stats`, `sys.dm_db_index_physical_stats` |

---

## 🔍 Detailed Script Information

Queries Dynamic Management Views (DMVs) to identify top missing nonclustered index candidates for the current database, prioritized by estimated performance impact.

* **Key Features:**
* **Priority Calculation:** Categorizes index recommendations as `High`, `Medium`, or `Low` based on seeking activity, user impact percentage, and average query cost.
* **Automated DDL Generation:** Provides a ready-to-use `CREATE NONCLUSTERED INDEX` statement (`SuggestedSql`) including equality, inequality, and included columns.
* **Risk Assessment:** Outlines potential trade-offs (storage growth, write overhead for DML statements) before applying recommendations.
* **Execution Evidence:** Shows total seeks, scans, impact percentage, and last seek timestamp.



> ⚠️ **Note:** Missing index DMVs reset upon SQL Server restart. Always validate suggested indexes against existing index strategies and test them under full workload conditions before creating them in production.

Evaluates nonclustered index health across three key areas: usage metrics, duplicate key definitions, and physical fragmentation levels.

* **Key Features:**
* **Duplicate Detection:** Identifies indexes that share identical key column signatures to help eliminate redundant write overhead.
* **Unused Index Identification:** Highlights write-heavy indexes that receive zero seek, scan, or lookup operations.
* **Fragmentation Analysis:** Scans physical fragmentation using `LIMITED` mode and generates target maintenance commands (`REORGANIZE` or `REBUILD`).
* **Safety First:** Does **not** auto-generate `DROP` statements for unused or duplicate indexes, forcing a manual review to prevent accidental removal of constraints or periodic workload dependencies.



> ⚠️ **Note:** Usage statistics (`sys.dm_db_index_usage_stats`) reset on SQL Server service restarts. Ensure the instance has been running under normal workload conditions before acting on unused index findings.


## 🚀 Getting Started
Feel free to clone this repository or copy individual scripts into SQL Server Management Studio (SSMS) or Azure Data Studio. Always test scripts in a non-production environment before applying them to live databases.
