# SQL Server Scripts & Utilities
A practical collection of SQL Server scripts for DBAs, developers and data engineers — performance tuning, index analysis, query troubleshooting, database integrity and schema health checks.

---

## 📊 Script Overview

### 🏗️ Schema & Data Types

| Script | Description | Primary DMVs / Target |
| --- | --- | --- |
| [`schema_type_patterns.sql`](https://www.google.com/search?q=./schema/schema_type_patterns.sql) | Flags tables lacking Primary Keys, large Heaps, and legacy/unbounded LOB types. | `sys.tables`, `sys.columns`, `sys.types` |

---

## 🔍 Detailed Script Information

### schema_type_patterns.sql
Scans database schema design to identify potential structural issues impacting performance or data integrity.

* **Key Features:**
  * **Missing Primary Keys:** Flags tables without a defined Primary Key.
  * **Large Heaps:** Highlights tables without a Clustered Index that exceed 10,000 rows.
  * **Legacy & Unbounded LOB Types:** Flags columns using deprecated types (`text`, `ntext`, `image`) or unbounded max length types (`varchar(max)`, `nvarchar(max)`, `varbinary(max)`).

---
## 🚀 Getting Started
Feel free to clone this repository or copy individual scripts into SQL Server Management Studio (SSMS) or Azure Data Studio. Always test scripts in a non-production environment before applying them to live databases.
