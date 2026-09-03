/*******************************************************************************
Script Name: long_running_queries.sql
Purpose: Identifies active user requests running for at least 60 seconds in the selected database.
Scope: Current database; active requests
SQL Server: 2016+
Azure SQL: Azure SQL support varies for instance-level DMVs; see docs/COMPATIBILITY.md
Permissions: VIEW SERVER STATE or VIEW DATABASE STATE, depending on scope; SQL Server 2022+ may require the corresponding PERFORMANCE STATE permission
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/  
  
SELECT TOP (100)  
    CASE  
        WHEN r.total_elapsed_time >= 300000  
          OR r.cpu_time >= 300000  
          OR r.logical_reads >= 1000000  
            THEN 'High'  
        ELSE 'Medium'  
    END AS [Priority],  
  
    'Query' AS [Category],  
  
    CONCAT(  
        'session ',  
        r.session_id,  
        ' request ',  
        r.request_id  
    ) AS [Object],  
  
    CASE  
        WHEN r.command = N'KILLED/ROLLBACK'  
            THEN 'Long-running rollback operation'  
        WHEN r.blocking_session_id > 0  
            THEN 'Long-running query currently blocked by another session'  
        WHEN r.status = N'suspended'  
            THEN 'Long-running query waiting on a resource'  
        ELSE 'Long-running active query'  
    END AS [Finding],  
  
    CONCAT(  
        'database=', DB_NAME(r.database_id),  
        '; session=', r.session_id,  
        '; request=', r.request_id,  
        '; login=', COALESCE(s.login_name, N'<unknown>'),  
        '; host=', COALESCE(s.host_name, N'<unknown>'),  
        '; application=', COALESCE(s.program_name, N'<unknown>'),  
        '; start=', CONVERT(varchar(19), r.start_time, 120),  
        '; elapsed sec=',  
        CONVERT(decimal(18,2), r.total_elapsed_time / 1000.0),  
        '; CPU sec=',  
        CONVERT(decimal(18,2), r.cpu_time / 1000.0),  
        '; logical reads=', r.logical_reads,  
        '; physical reads=', r.reads,  
        '; writes=', r.writes,  
        '; status=', r.status,  
        '; command=', r.command,  
        '; wait type=', COALESCE(r.wait_type, N'<none>'),  
        '; last wait=', COALESCE(r.last_wait_type, N'<none>'),  
        '; wait ms=', r.wait_time,  
        '; blocker=', r.blocking_session_id,  
        '; percent complete=',  
        CONVERT(decimal(9,2), r.percent_complete),  
        '; estimated remaining sec=',  
        CONVERT(decimal(18,2), r.estimated_completion_time / 1000.0),  
        '; SQL=',  
        LEFT(  
            REPLACE(  
                REPLACE(  
                    COALESCE(st.text, N'<SQL text unavailable>'),  
                    CHAR(13),  
                    N' '  
                ),  
                CHAR(10),  
                N' '  
            ),  
            1500  
        )  
    ) AS [Evidence],  
  
    CASE  
        WHEN r.blocking_session_id > 0  
            THEN 'Run blocking_sessions.sql, identify the root blocker, and avoid tuning the blocked query before separating blocking time from execution time.'  
        WHEN LEFT(COALESCE(r.wait_type, N''), 6) = N'LCK_M_'  
            THEN 'Investigate locking, transaction duration, access order, and isolation level.'  
        WHEN r.status = N'suspended'  
            THEN 'Analyze the current wait type, execution plan, cardinality estimates, memory grant, I/O latency, and parallelism.'  
        ELSE  
            'Capture the actual execution plan and parameters; review statistics, indexes, cardinality, predicates, memory grants, and resource consumption.'  
    END AS [Recommendation],  
  
    CONCAT(  
        '-- Review the execution plan and wait type first; ',  
        'if cancellation is approved: KILL ',  
        r.session_id,  
        ';'  
    ) AS [SuggestedSql],  
  
    'High: query cancellation can cause rollback, application errors, partial workflow failure, or remove evidence required for tuning'  
        AS [Risk]  
  
FROM sys.dm_exec_requests AS r  
INNER JOIN sys.dm_exec_sessions AS s  
    ON s.session_id = r.session_id  
OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) AS st  
  
WHERE r.database_id = DB_ID()  
  AND r.session_id <> @@SPID  
  AND s.is_user_process = 1  
  AND r.total_elapsed_time >= 60000  
  
ORDER BY  
    r.total_elapsed_time DESC,  
    r.cpu_time DESC;  
