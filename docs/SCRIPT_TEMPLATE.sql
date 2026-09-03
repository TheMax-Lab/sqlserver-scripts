/*******************************************************************************
Script Name: descriptive_script_name.sql
Purpose: Describes the single DBA question answered by this script.
Scope: Current database or SQL Server instance
SQL Server: 2016+
Azure SQL: State supported platforms and limitations
Permissions: State the least privileges required
Risk: Read-only; state expected query cost and generated-SQL risk
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

/*
Declare configurable thresholds here. Keep diagnostics read-only by default.
Return corrective commands as text and never execute them automatically.
*/

SELECT
    'Low' AS [Priority],
    'Category' AS [Category],
    QUOTENAME(DB_NAME()) AS [Object],
    'Finding supported by evidence' AS [Finding],
    'Relevant measurable evidence' AS [Evidence],
    'Conservative next step requiring validation' AS [Recommendation],
    '-- Reviewable SQL or investigation command; do not execute blindly.' AS [SuggestedSql],
    'Low: read-only diagnostic; describe the risk of any suggested change.' AS [Risk];