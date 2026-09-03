# Security Policy

## Reporting a security issue

Please do not open a public issue for a vulnerability that could expose credentials, sensitive database information, or create an unsafe destructive workflow.

Use GitHub's private vulnerability reporting feature when enabled for this repository.

## Scope

This repository primarily contains read-only diagnostic T-SQL. Security-relevant issues may include:

- generated SQL that can target the wrong object;
- unsafe dynamic SQL;
- identifier injection;
- accidental exposure of secrets or sensitive query text;
- destructive statements executed automatically;
- misleading permission requirements.

Never include production credentials, connection strings, customer data, access tokens, or sensitive query text in issue reports.
