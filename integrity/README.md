# SQL Server Scripts & Utilities
A practical collection of SQL Server scripts for DBAs, developers and data engineers — performance tuning, index analysis, query troubleshooting, database integrity and schema health checks.

---

## 📊 Script Overview

### 🔒 Integrity & Constraints

| Script | Description | Primary DMVs / Target |
| --- | --- | --- |
| [`fk_analysis.sql`](https://www.google.com/search?q=./integrity/fk_analysis.sql) | Identifies unindexed, untrusted, or disabled Foreign Key constraints. | `sys.foreign_keys`, `sys.indexes` |

---

## 🔍 Detailed Script Information

### fk_analysis.sql
Inspects Foreign Keys across the database to detect coverage, integrity, and status issues.

* **Key Features:**
  * **Unindexed FK Detection:** Finds Foreign Keys that lack a supporting index with matching leading columns (reducing child table lock contention).
  * **Constraint Validation Check:** Identifies disabled constraints or untrusted constraints (`WITH NOCHECK`).
  * **Auto-Fix Generation:** Supplies `ALTER TABLE ... WITH CHECK CHECK CONSTRAINT` statements for untrusted or disabled FKs.

---
## 🚀 Getting Started
Feel free to clone this repository or copy individual scripts into SQL Server Management Studio (SSMS) or Azure Data Studio. Always test scripts in a non-production environment before applying them to live databases.
