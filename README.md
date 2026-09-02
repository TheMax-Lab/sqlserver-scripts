# sqlserver-scripts
# SQL Server Scripts & Utilities
A curated collection of SQL Server scripts, T-SQL utilities, and administrative tools for database management, performance tuning, and maintenance.

## 📌 Contents

### 🔍 Find Missing Indexes (`missing_indexes.sql`)

Queries Dynamic Management Views (DMVs) to identify top missing nonclustered index candidates for the current database, prioritized by estimated performance impact.

* **Key Features:**
  * **Priority Calculation:** Categorizes index recommendations as `High`, `Medium`, or `Low` based on seeking activity, user impact percentage, and average query cost.
  * **Automated DDL Generation:** Provides a ready-to-use `CREATE NONCLUSTERED INDEX` statement (`SuggestedSql`) including equality, inequality, and included columns.
  * **Risk Assessment:** Outlines potential trade-offs (storage growth, write overhead for DML statements) before applying recommendations.
  * **Execution Evidence:** Shows total seeks, scans, impact percentage, and last seek timestamp.

> ⚠️ **Note:** Missing index DMVs reset upon SQL Server restart. Always validate suggested indexes against existing index strategies and test them under full workload conditions before creating them in production.


## 🚀 Getting Started
Feel free to clone this repository or copy individual scripts into SQL Server Management Studio (SSMS) or Azure Data Studio. Always test scripts in a non-production environment before applying them to live databases.
