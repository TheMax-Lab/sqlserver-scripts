/*******************************************************************************
Script Name: memory_grants.sql
Purpose: Finds active and waiting query memory grants in the current database.
Scope: Current database, active requests
SQL Server: 2016+
Permissions:
- SQL Server 2019 and earlier: VIEW SERVER STATE
- SQL Server 2022 and later: VIEW SERVER PERFORMANCE STATE
Risk: Read-only
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

DECLARE @MinimumRequestedMB decimal(18,2) = 32.0;

SELECT TOP (100)
    CASE
        WHEN mg.grant_time IS NULL AND mg.wait_time_ms >= 5000 THEN 'High'
        WHEN mg.requested_memory_kb / 1024.0 >= 1024 THEN 'High'
        ELSE 'Medium'
    END AS [Priority],
    'Memory Grant' AS [Category],
    CONCAT('session ', mg.session_id, '; request ', mg.request_id) AS [Object],
    CASE
        WHEN mg.grant_time IS NULL THEN 'Query is waiting for a memory grant'
        WHEN mg.used_memory_kb > mg.granted_memory_kb THEN 'Used memory exceeds granted memory'
        WHEN mg.granted_memory_kb > 0 AND mg.used_memory_kb * 1.0 / mg.granted_memory_kb < 0.25
             AND mg.granted_memory_kb >= 262144
             THEN 'Large grant with low current utilization'
        ELSE 'Large active query memory grant'
    END AS [Finding],
    CONCAT(
        'requested MB=', CONVERT(decimal(18,2), mg.requested_memory_kb / 1024.0),
        '; granted MB=', CONVERT(decimal(18,2), mg.granted_memory_kb / 1024.0),
        '; required MB=', CONVERT(decimal(18,2), mg.required_memory_kb / 1024.0),
        '; used MB=', CONVERT(decimal(18,2), mg.used_memory_kb / 1024.0),
        '; max used MB=', CONVERT(decimal(18,2), mg.max_used_memory_kb / 1024.0),
        '; wait ms=', mg.wait_time_ms,
        '; queue=', COALESCE(CONVERT(varchar(20), mg.queue_id), 'n/a'),
        '; dop=', COALESCE(CONVERT(varchar(20), r.dop), 'n/a'),
        '; SQL=', LEFT(REPLACE(REPLACE(COALESCE(st.text, N'<unavailable>'), CHAR(13), N' '), CHAR(10), N' '), 1500)
    ) AS [Evidence],
    'Inspect the actual/cached plan for sorts, hashes, cardinality errors and parameter sensitivity. Correlate with concurrency and RESOURCE_SEMAPHORE waits before changing indexes, queries, memory settings or workload concurrency.' AS [Recommendation],
    '-- Inspect the execution plan and compare requested/granted/used memory over repeated executions.' AS [SuggestedSql],
    'Low for inspection; High for query, index, Resource Governor or memory-configuration changes.' AS [Risk]
FROM sys.dm_exec_query_memory_grants AS mg
LEFT JOIN sys.dm_exec_requests AS r
    ON r.session_id = mg.session_id
   AND r.request_id = mg.request_id
OUTER APPLY sys.dm_exec_sql_text(mg.sql_handle) AS st
WHERE (r.database_id = DB_ID() OR r.database_id IS NULL)
  AND mg.requested_memory_kb / 1024.0 >= @MinimumRequestedMB
ORDER BY
    CASE WHEN mg.grant_time IS NULL THEN 0 ELSE 1 END,
    mg.wait_time_ms DESC,
    mg.requested_memory_kb DESC
OPTION (RECOMPILE);
