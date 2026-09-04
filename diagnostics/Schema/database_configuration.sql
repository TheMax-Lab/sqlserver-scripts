/*******************************************************************************
Script Name: database_configuration.sql
Purpose: Reviews current-database configuration choices that commonly deserve DBA review.
Scope: Current database
SQL Server: 2016+
Azure SQL: Azure SQL support varies for instance-level DMVs; see docs/COMPATIBILITY.md
Permissions: VIEW SERVER STATE or VIEW DATABASE STATE, depending on scope; SQL Server 2022+ may require the corresponding PERFORMANCE STATE permission
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

DECLARE @DatabaseName sysname = DB_NAME();

SELECT
    CASE
        WHEN d.is_auto_close_on = 1 OR d.is_auto_shrink_on = 1 THEN 'High'
        WHEN d.page_verify_option_desc <> N'CHECKSUM' THEN 'Medium'
        ELSE 'Low'
    END AS [Priority],
    'Configuration' AS [Category],
    QUOTENAME(d.name) AS [Object],
    CASE
        WHEN d.is_auto_close_on = 1 THEN 'AUTO_CLOSE is enabled'
        WHEN d.is_auto_shrink_on = 1 THEN 'AUTO_SHRINK is enabled'
        WHEN d.page_verify_option_desc <> N'CHECKSUM' THEN 'PAGE_VERIFY is not CHECKSUM'
        WHEN d.is_auto_create_stats_on = 0 THEN 'AUTO_CREATE_STATISTICS is disabled'
        WHEN d.is_auto_update_stats_on = 0 THEN 'AUTO_UPDATE_STATISTICS is disabled'
        ELSE 'Core database configuration review'
    END AS [Finding],
    CONCAT(
        'state=', d.state_desc,
        '; recovery=', d.recovery_model_desc,
        '; compatibility=', d.compatibility_level,
        '; page verify=', d.page_verify_option_desc,
        '; auto close=', d.is_auto_close_on,
        '; auto shrink=', d.is_auto_shrink_on,
        '; auto create stats=', d.is_auto_create_stats_on,
        '; auto update stats=', d.is_auto_update_stats_on,
        '; auto update stats async=', d.is_auto_update_stats_async_on,
        '; read committed snapshot=', d.is_read_committed_snapshot_on,
        '; snapshot isolation=', d.snapshot_isolation_state_desc
    ) AS [Evidence],
    'Review settings against workload, HA/DR, application concurrency, maintenance strategy and SQL Server version. Configuration changes should be tested before production deployment.' AS [Recommendation],
    CONCAT(
        '-- Example review commands only:', CHAR(13), CHAR(10),
        '-- ALTER DATABASE ', QUOTENAME(d.name), ' SET AUTO_CLOSE OFF;', CHAR(13), CHAR(10),
        '-- ALTER DATABASE ', QUOTENAME(d.name), ' SET AUTO_SHRINK OFF;', CHAR(13), CHAR(10),
        '-- ALTER DATABASE ', QUOTENAME(d.name), ' SET PAGE_VERIFY CHECKSUM;'
    ) AS [SuggestedSql],
    'Medium: ALTER DATABASE changes can affect concurrency, recovery behavior, maintenance and application compatibility.' AS [Risk]
FROM sys.databases AS d
WHERE d.database_id = DB_ID()
OPTION (RECOMPILE);
