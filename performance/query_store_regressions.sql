/*******************************************************************************
Script Name: query_store_regressions.sql
Purpose: Finds queries whose recent Query Store average duration is materially
         worse than their earlier baseline.
Scope: Current database
SQL Server: 2016+
Requirement: Query Store must be enabled and contain runtime history.
Permissions: VIEW DATABASE STATE
Risk: Read-only
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

DECLARE @RecentHours int = 24;
DECLARE @BaselineDays int = 7;
DECLARE @MinimumRecentExecutions bigint = 5;
DECLARE @MinimumRecentAvgDurationMs decimal(18,2) = 100.0;
DECLARE @RegressionFactor decimal(18,2) = 1.50;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_query_store_options
    WHERE actual_state_desc IN (N'READ_WRITE', N'READ_ONLY')
)
BEGIN
    SELECT
        'Info' AS [Priority],
        'Query Store' AS [Category],
        QUOTENAME(DB_NAME()) AS [Object],
        'Query Store is not available or not active for analysis' AS [Finding],
        'No Query Store runtime history can be analyzed.' AS [Evidence],
        'Enable Query Store only after reviewing storage, capture mode and operational policy.' AS [Recommendation],
        '-- ALTER DATABASE CURRENT SET QUERY_STORE = ON;' AS [SuggestedSql],
        'Medium: enabling Query Store changes database behavior and storage usage.' AS [Risk];
    RETURN;
END;

;WITH runtime AS
(
    SELECT
        q.query_id,
        qt.query_sql_text,
        p.plan_id,
        rsi.start_time,
        rsi.end_time,
        rs.count_executions,
        rs.avg_duration / 1000.0 AS avg_duration_ms,
        rs.avg_cpu_time / 1000.0 AS avg_cpu_ms,
        rs.avg_logical_io_reads
    FROM sys.query_store_query AS q
    INNER JOIN sys.query_store_query_text AS qt
        ON qt.query_text_id = q.query_text_id
    INNER JOIN sys.query_store_plan AS p
        ON p.query_id = q.query_id
    INNER JOIN sys.query_store_runtime_stats AS rs
        ON rs.plan_id = p.plan_id
    INNER JOIN sys.query_store_runtime_stats_interval AS rsi
        ON rsi.runtime_stats_interval_id = rs.runtime_stats_interval_id
    WHERE rsi.start_time >= DATEADD(day, -@BaselineDays, SYSUTCDATETIME())
),
agg AS
(
    SELECT
        query_id,
        MAX(query_sql_text) AS query_sql_text,
        SUM(CASE WHEN start_time >= DATEADD(hour, -@RecentHours, SYSUTCDATETIME())
                 THEN count_executions ELSE 0 END) AS recent_executions,
        SUM(CASE WHEN start_time >= DATEADD(hour, -@RecentHours, SYSUTCDATETIME())
                 THEN avg_duration_ms * count_executions ELSE 0 END)
            / NULLIF(SUM(CASE WHEN start_time >= DATEADD(hour, -@RecentHours, SYSUTCDATETIME())
                              THEN count_executions ELSE 0 END), 0) AS recent_avg_duration_ms,
        SUM(CASE WHEN start_time < DATEADD(hour, -@RecentHours, SYSUTCDATETIME())
                 THEN avg_duration_ms * count_executions ELSE 0 END)
            / NULLIF(SUM(CASE WHEN start_time < DATEADD(hour, -@RecentHours, SYSUTCDATETIME())
                              THEN count_executions ELSE 0 END), 0) AS baseline_avg_duration_ms,
        SUM(CASE WHEN start_time >= DATEADD(hour, -@RecentHours, SYSUTCDATETIME())
                 THEN avg_cpu_ms * count_executions ELSE 0 END)
            / NULLIF(SUM(CASE WHEN start_time >= DATEADD(hour, -@RecentHours, SYSUTCDATETIME())
                              THEN count_executions ELSE 0 END), 0) AS recent_avg_cpu_ms,
        SUM(CASE WHEN start_time >= DATEADD(hour, -@RecentHours, SYSUTCDATETIME())
                 THEN avg_logical_io_reads * count_executions ELSE 0 END)
            / NULLIF(SUM(CASE WHEN start_time >= DATEADD(hour, -@RecentHours, SYSUTCDATETIME())
                              THEN count_executions ELSE 0 END), 0) AS recent_avg_reads
    FROM runtime
    GROUP BY query_id
)
SELECT TOP (100)
    CASE
        WHEN recent_avg_duration_ms / NULLIF(baseline_avg_duration_ms, 0) >= 3 THEN 'High'
        ELSE 'Medium'
    END AS [Priority],
    'Query Store' AS [Category],
    CONCAT('query_id=', query_id) AS [Object],
    'Recent average duration is worse than the Query Store baseline' AS [Finding],
    CONCAT(
        'recent executions=', recent_executions,
        '; recent avg duration ms=', CONVERT(decimal(18,2), recent_avg_duration_ms),
        '; baseline avg duration ms=', CONVERT(decimal(18,2), baseline_avg_duration_ms),
        '; regression x=', CONVERT(decimal(18,2), recent_avg_duration_ms / NULLIF(baseline_avg_duration_ms,0)),
        '; recent avg CPU ms=', CONVERT(decimal(18,2), recent_avg_cpu_ms),
        '; recent avg logical reads=', CONVERT(decimal(18,2), recent_avg_reads),
        '; SQL=', LEFT(REPLACE(REPLACE(query_sql_text, CHAR(13), N' '), CHAR(10), N' '), 1500)
    ) AS [Evidence],
    'Compare recent plans with earlier plans, deployments, statistics, parameter sensitivity, data growth and configuration changes. Validate whether the regression is workload-driven before forcing a plan.' AS [Recommendation],
    CONCAT(
        'SELECT p.plan_id, p.is_forced_plan, p.query_plan ',
        'FROM sys.query_store_plan AS p WHERE p.query_id = ', query_id, ';'
    ) AS [SuggestedSql],
    'Low for inspection; High for plan forcing or application/query changes because a forced plan can later become suboptimal.' AS [Risk]
FROM agg
WHERE recent_executions >= @MinimumRecentExecutions
  AND recent_avg_duration_ms >= @MinimumRecentAvgDurationMs
  AND baseline_avg_duration_ms IS NOT NULL
  AND recent_avg_duration_ms >= baseline_avg_duration_ms * @RegressionFactor
ORDER BY
    recent_avg_duration_ms / NULLIF(baseline_avg_duration_ms,0) DESC,
    recent_avg_duration_ms DESC
OPTION (RECOMPILE);
