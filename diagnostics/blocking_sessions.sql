/*******************************************************************************
Script Name: blocking_sessions.sql
Purpose: Identifies currently blocked requests in the selected database and reports their direct blocking session.
Scope: Current database; active requests and their instance-level sessions
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
        WHEN r.wait_time >= 60000 THEN 'High'  
        ELSE 'Medium'  
    END AS [Priority],  
  
    'Concurrency' AS [Category],  
  
    CONCAT(  
        'session ',  
        r.session_id,  
        ' blocked by ',  
        r.blocking_session_id  
    ) AS [Object],  
  
    CASE r.blocking_session_id  
        WHEN -2 THEN 'Blocked by an orphaned distributed transaction'  
        WHEN -3 THEN 'Blocked by a deferred recovery transaction'  
        WHEN -4 THEN 'Blocking session could not be identified because of an internal latch transition'  
        WHEN -5 THEN 'Blocking session is not tracked for this asynchronous latch type'  
        ELSE  
            CASE  
                WHEN bs.status = N'sleeping'  
                 AND COALESCE(bs.open_transaction_count, 0) > 0  
                    THEN 'Blocked by a sleeping session with an open transaction'  
                ELSE 'Blocking session detected'  
            END  
    END AS [Finding],  
  
    CONCAT(  
        'database=', DB_NAME(r.database_id),  
        '; blocked session=', r.session_id,  
        '; blocked login=', COALESCE(s.login_name, N'<unknown>'),  
        '; blocked host=', COALESCE(s.host_name, N'<unknown>'),  
        '; blocked application=', COALESCE(s.program_name, N'<unknown>'),  
        '; blocked status=', r.status,  
        '; wait ms=', r.wait_time,  
        '; wait type=', COALESCE(r.wait_type, N'<none>'),  
        '; wait resource=', COALESCE(r.wait_resource, N'<none>'),  
        '; blocker=', r.blocking_session_id,  
        '; blocker status=', COALESCE(bs.status, N'<special or unavailable>'),  
        '; blocker login=', COALESCE(bs.login_name, N'<special or unavailable>'),  
        '; blocker host=', COALESCE(bs.host_name, N'<special or unavailable>'),  
        '; blocker application=', COALESCE(bs.program_name, N'<special or unavailable>'),  
        '; blocker open transactions=', COALESCE(bs.open_transaction_count, 0),  
        '; blocked SQL=',  
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
            1200  
        ),  
        '; blocker SQL=',  
        LEFT(  
            REPLACE(  
                REPLACE(  
                    COALESCE(bst.text, N'<SQL text unavailable>'),  
                    CHAR(13),  
                    N' '  
                ),  
                CHAR(10),  
                N' '  
            ),  
            1200  
        )  
    ) AS [Evidence],  
  
    CASE  
        WHEN r.blocking_session_id = -2  
            THEN 'Inspect MS DTC and the orphaned transaction UOW. Resolve it only after confirming application and distributed transaction state.'  
        WHEN r.blocking_session_id < 0  
            THEN 'Investigate the internal owner, SQL Server error log, recovery state, and related waits before taking corrective action.'  
        ELSE  
            'Identify the root blocker, transaction owner, lock resource, and business operation. Prefer completing or rolling back the transaction in the owning application.'  
    END AS [Recommendation],  
  
    CASE  
        WHEN r.blocking_session_id > 0  
            THEN CONCAT(  
                '-- Validate the root blocker first; if cancellation is approved: KILL ',  
                r.blocking_session_id,  
                ';'  
            )  
        ELSE  
            '-- Special blocker: do not issue KILL using the negative blocking_session_id'  
    END AS [SuggestedSql],  
  
    'High: KILL can trigger a long rollback, interrupt business operations, and leave the application in an inconsistent workflow state'  
        AS [Risk]  
  
FROM sys.dm_exec_requests AS r  
INNER JOIN sys.dm_exec_sessions AS s  
    ON s.session_id = r.session_id  
LEFT JOIN sys.dm_exec_sessions AS bs  
    ON bs.session_id = r.blocking_session_id  
  
OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) AS st  
  
OUTER APPLY  
(  
    SELECT TOP (1)  
        br.sql_handle,  
        br.request_id,  
        br.status,  
        br.command  
    FROM sys.dm_exec_requests AS br  
    WHERE br.session_id = r.blocking_session_id  
    ORDER BY br.request_id  
) AS br  
  
OUTER APPLY  
(  
    SELECT TOP (1)  
        bc.most_recent_sql_handle  
    FROM sys.dm_exec_connections AS bc  
    WHERE bc.session_id = r.blocking_session_id  
    ORDER BY bc.connect_time DESC  
) AS bc  
  
OUTER APPLY sys.dm_exec_sql_text  
(  
    COALESCE(br.sql_handle, bc.most_recent_sql_handle)  
) AS bst  
  
WHERE r.database_id = DB_ID()  
  AND r.session_id <> @@SPID  
  AND r.blocking_session_id <> 0  
  
ORDER BY  
    r.wait_time DESC,  
    r.session_id;  
