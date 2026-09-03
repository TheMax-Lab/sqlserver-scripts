# Contributing

Contributions are welcome, especially small SQL Server diagnostics that answer a real DBA troubleshooting question.

## Before opening a pull request

- Put the script in the most appropriate category.
- Follow `docs/SCRIPT_TEMPLATE.sql`.
- Keep diagnostics read-only by default.
- Do not execute destructive DDL/DML automatically.
- If you generate corrective SQL, return it as text and explain the risk.
- Quote dynamic object names with `QUOTENAME`.
- Document minimum SQL Server version, scope and permissions.
- Expose thresholds as variables near the top of the script.
- Prefer evidence-backed findings over universal rules.
- Test on a non-production database before submitting.
- Update the category README and root catalog.

## Script design principles

1. Diagnostics before automation.
2. Evidence over assumptions.
3. Small and understandable queries.
4. Conservative recommendations.
5. DBA judgment remains essential.

## Pull requests

Explain:
- the problem the script solves;
- SQL Server versions tested;
- expected output;
- query cost on large databases;
- permissions required;
- known limitations.
