# SQL Server Database Diagnostics Toolkit

[![SQL Server](https://img.shields.io/badge/SQL%20Server-T--SQL-red?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![License](https://img.shields.io/github/license/TheMax-Lab/sqlserver-scripts)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/TheMax-Lab/sqlserver-scripts?style=flat)](https://github.com/TheMax-Lab/sqlserver-scripts/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/TheMax-Lab/sqlserver-scripts?style=flat)](https://github.com/TheMax-Lab/sqlserver-scripts/network/members)

Practical **SQL Server and T-SQL diagnostic scripts** for investigating database performance, indexing, query execution, integrity, and schema design.

Built for **DBAs, database developers, data engineers, and SQL Server professionals** who need focused queries to identify potential issues and turn database metadata into actionable findings.

> **Find the issue → understand the evidence → review the recommendation → decide what to do.**

---

## 🔎 What Does It Detect?

The toolkit currently includes diagnostics for:

- 🔍 Missing index candidates
- 📊 Index usage and physical fragmentation
- ♻️ Duplicate indexes
- 💤 Potentially unused indexes
- ⚡ Expensive query patterns
- 🔄 Implicit data type conversions
- 💾 TempDB spills
- 🔎 Heavy table and index scans
- 🔗 Unindexed Foreign Keys
- ⚠️ Untrusted or disabled Foreign Keys
- 🔑 Tables without Primary Keys
- 🧱 Large heaps
- 🗃️ Legacy SQL Server data types
- 📦 Unbounded `MAX` data types

The scripts are designed primarily for **investigation and diagnostics**. They do not blindly apply database changes.

---

## 📂 Repository Structure

```text
sqlserver-scripts/
│
├── performance/
│   ├── README.md
│   ├── missing_indexes.sql
│   ├── index_analysis.sql
│   └── query_plan_candidates.sql
│
├── integrity/
│   ├── README.md
│   └── fk_analysis.sql
│
├── schema/
│   ├── README.md
│   └── schema_type_patterns.sql
│
├── LICENSE
└── README.md
```

---

## 🚀 Quick Start

Clone the repository:

```bash
git clone https://github.com/TheMax-Lab/sqlserver-scripts.git
```

Choose the diagnostic category that matches the problem you are investigating.

For example:

```text
performance/missing_indexes.sql
```

Open the script in **SQL Server Management Studio (SSMS)** or another SQL Server-compatible query tool and execute it against the target database.

### Recommended workflow

```text
Run diagnostic
     ↓
Review findings
     ↓
Inspect evidence
     ↓
Evaluate recommendation
     ↓
Validate against workload
     ↓
Apply changes only when appropriate
```

These scripts are intended to support database investigation — **not replace DBA judgment**.

---

# ⚡ Performance Diagnostics

## `missing_indexes.sql`

[Open script](performance/missing_indexes.sql)

Identifies potentially useful **nonclustered index candidates** using SQL Server missing-index Dynamic Management Views (DMVs).

### What it provides

- Priority classification
- Estimated impact
- User seeks and scans
- Average query cost
- Last seek timestamp
- Equality columns
- Inequality columns
- Included columns
- Suggested `CREATE NONCLUSTERED INDEX` statements
- Risk information

### Why it is useful

Missing-index recommendations can provide useful clues when investigating query performance problems.

However, SQL Server's missing-index DMVs should be treated as **recommendations, not instructions**.

> ⚠️ Missing-index statistics are transient and can be reset after a SQL Server restart.

Always compare recommendations with the existing indexing strategy and test changes against realistic workloads before applying them in production.

---

## `index_analysis.sql`

[Open script](performance/index_analysis.sql)

Analyzes nonclustered indexes across three areas:

- Index usage
- Duplicate definitions
- Physical fragmentation

### What it can identify

- Potentially duplicate indexes
- Write-heavy indexes with no recorded reads
- Index usage statistics
- Index fragmentation
- Potential maintenance candidates
- `REORGANIZE` candidates
- `REBUILD` candidates

The script also provides evidence and recommendations to help guide further investigation.

### Safety by design

The script does **not** automatically generate `DROP INDEX` statements for unused or duplicate indexes.

An index that appears unused may still be required by:

- Infrequent workloads
- Maintenance operations
- Periodic reports
- Application-specific execution paths
- Constraints or other database requirements

> ⚠️ Index usage statistics are transient and should be evaluated over a representative workload period.

---

## `query_plan_candidates.sql`

[Open script](performance/query_plan_candidates.sql)

Searches the SQL Server **plan cache** for queries exhibiting potentially problematic execution patterns.

### Current detections include

- `CONVERT_IMPLICIT`
- TempDB spills
- Expensive scans
- High CPU consumption
- High logical reads

### Metrics

The output includes information such as:

- Query text
- Execution count
- Average CPU time
- Average logical reads
- Query plan information

This makes the script useful as a **first-pass query troubleshooting tool**.

> ⚠️ This script analyzes information currently available in the plan cache. Plan cache data is transient and can be cleared by events such as SQL Server restarts or memory pressure.

The results should therefore be considered **candidates for investigation**, rather than a complete representation of all query activity.

---

# 🔒 Integrity Diagnostics

## `fk_analysis.sql`

[Open script](integrity/fk_analysis.sql)

Analyzes Foreign Key constraints across the current database.

### Detects

- Foreign Keys without supporting indexes
- Disabled Foreign Keys
- Untrusted Foreign Keys

### Why unindexed Foreign Keys matter

Foreign Keys can have important performance implications during:

- `DELETE`
- `UPDATE`
- Parent/child table operations
- Referential integrity checks

The script identifies Foreign Keys whose leading columns are not appropriately covered by an index.

### Constraint validation

For applicable findings, the script can provide SQL such as:

```sql
ALTER TABLE ...
WITH CHECK CHECK CONSTRAINT ...
```

The generated statement should always be reviewed before execution.

---

# 🏗️ Schema Diagnostics

## `schema_type_patterns.sql`

[Open script](schema/schema_type_patterns.sql)

Scans database metadata for schema patterns that may deserve further investigation.

### Current checks include

#### Missing Primary Keys

Identifies tables that do not have a defined Primary Key.

A missing Primary Key is not automatically an error, but it is often worth reviewing.

#### Large Heaps

Identifies tables without a clustered index that exceed the configured row threshold.

Heaps can be perfectly valid in some workloads, so findings should be evaluated according to actual access patterns.

#### Legacy Data Types

Identifies deprecated SQL Server data types such as:

```text
text
ntext
image
```

#### Unbounded MAX Types

Identifies columns using:

```text
varchar(max)
nvarchar(max)
varbinary(max)
```

These types are not inherently wrong, but their usage can be worth reviewing depending on the data model and workload.

> ⚠️ Schema findings are **signals for investigation**, not automatic evidence of a design defect.

---

# 📊 Diagnostic Output

A key design principle of this repository is to make diagnostic results easier to understand and act upon.

Where appropriate, scripts use a common finding-oriented output model:

| Field | Purpose |
|---|---|
| `Priority` | Indicates the relative importance of the finding |
| `Category` | Groups the type of issue detected |
| `Object` | Identifies the affected database object |
| `Finding` | Describes what was detected |
| `Evidence` | Provides supporting metrics or metadata |
| `Recommendation` | Suggests what should be investigated |
| `SuggestedSql` | Provides SQL that can be reviewed |
| `Risk` | Highlights potential trade-offs |

The intention is to move beyond raw metadata queries and provide a more useful diagnostic workflow:

```text
Finding
   ↓
Evidence
   ↓
Recommendation
   ↓
Risk
   ↓
Human review
```

---

# 🛡️ Safety First

These scripts are designed primarily for **read-only diagnostics**, although some scripts can generate SQL statements intended to modify database objects or constraints.

Generated SQL is **not a command to execute blindly**.

Before applying any recommendation:

1. Review the finding.
2. Understand the evidence.
3. Check the existing database design.
4. Consider the application workload.
5. Test the change outside production.
6. Measure the impact.
7. Apply the change only when justified.

In particular, be careful with:

- Creating indexes
- Rebuilding indexes
- Reorganizing indexes
- Revalidating constraints
- Removing or modifying database objects

---

# 🔐 Permissions

Some SQL Server Dynamic Management Views require additional permissions.

Depending on the script and SQL Server version, access to database or server-level metadata may be required.

If a script returns a permissions error, check the documentation and the permissions required for the specific DMV being queried.

Run diagnostic scripts using an account with the **minimum permissions necessary** for the investigation.

---

# 🧩 Compatibility

The scripts target **Microsoft SQL Server** and use SQL Server-specific catalog views and Dynamic Management Views.

Because SQL Server capabilities and permissions can vary between versions and deployment models, compatibility should be validated against the target environment before using a script in production.

The repository will document version-specific requirements as scripts evolve.

---

# 💡 Design Principles

This project follows a few simple principles.

### 1. Diagnostics before automation

The scripts should help you understand a problem before making a change.

### 2. Evidence over assumptions

A recommendation should be accompanied by measurable evidence whenever possible.

### 3. No blind destructive operations

Diagnostic scripts should not casually generate destructive commands.

### 4. Practical over theoretical

The goal is to answer real SQL Server troubleshooting questions.

### 5. DBA judgment remains essential

A diagnostic finding is a starting point for investigation — not an automatic verdict.

---

# 🤝 Contributing

Contributions are welcome.

If you have a useful **SQL Server diagnostic query, T-SQL utility, performance investigation technique, integrity check, or schema analysis script**, consider contributing it to the project.

### Good contributions include

- SQL Server performance diagnostics
- Index analysis scripts
- Query troubleshooting utilities
- Database integrity checks
- Schema analysis tools
- DMV-based diagnostics
- Documentation improvements
- Bug fixes
- Compatibility improvements
- Better diagnostic output

### Before submitting a Pull Request

Please make sure that:

- The script has a clear and descriptive name.
- Its purpose is documented.
- SQL Server-specific requirements are documented.
- Required permissions are documented when relevant.
- Potential risks are explained.
- Destructive operations are not executed automatically.
- Generated SQL is clearly identified.
- The script follows the existing T-SQL style.

---

# 💬 Suggestions & Issues

Have an idea for a useful SQL Server diagnostic?

Open an issue and describe:

- The problem you want to investigate
- The expected output
- Your SQL Server version
- Relevant example or sample data
- Why the diagnostic would be useful

Real-world DBA problems and production troubleshooting scenarios are especially valuable.

---

# 🗺️ Roadmap

The project will gradually expand into a broader SQL Server diagnostic toolkit.

Potential future areas include:

- Blocking and lock analysis
- Long-running transactions
- Wait statistics
- Query Store diagnostics
- Top CPU queries
- Top logical-read queries
- Database file analysis
- Backup health checks
- Statistics analysis
- TempDB diagnostics
- Database configuration checks
- Additional schema and integrity diagnostics

The focus will remain on **small, understandable, reusable T-SQL scripts** rather than building a large opaque automation framework.

---

# ⭐ Support the Project

If this repository helps you investigate a SQL Server problem, save it for later or use one of the scripts in your environment, consider giving it a ⭐.

Stars help other SQL Server professionals discover the project and provide motivation to keep expanding the toolkit.

**Find a problem → Run a diagnostic → Learn from the evidence → Share the improvement**

---

# 📚 Topics

This project is focused on:

`SQL Server` · `T-SQL` · `SQL` · `DBA` · `Database Administration` · `SQL Server Performance` · `Performance Tuning` · `Query Optimization` · `Index Analysis` · `Database Diagnostics` · `Database Integrity` · `Database Troubleshooting`

---

# 📄 License

This project is licensed under the **MIT License**.

See [LICENSE](LICENSE) for details.

---

<p align="center">
  <b>Built for SQL Server professionals who prefer actionable diagnostics over guesswork.</b>
</p>
