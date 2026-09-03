/*******************************************************************************
Script Name: open_transactions.sql
Purpose: Identifies open user transactions in the selected database, including sleeping sessions, age, log usage, and last SQL.
Scope: Current database; instance-level session metadata
SQL Server: 2016+
Azure SQL: Azure SQL support varies for instance-level DMVs; see docs/COMPATIBILITY.md
Permissions: VIEW SERVER STATE or VIEW DATABASE STATE, depending on scope; SQL Server 2022+ may require the corresponding PERFORMANCE STATE permission
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/  
  
;WITH OpenTransactions AS  
(  
    SELECT  
        st.session_id,  
        st.transaction_id,  
        at.name AS transaction_name,  
        at.transaction_type,  
        at.transaction_state AS active_transaction_state,  
        dt.database_transaction_state,  
        COALESCE(  
            dt.database_transaction_begin_time,  
            at.transaction_begin_time  
        ) AS transaction_begin_time,  
        DATEDIFF  
        (  
            SECOND,  
            COALESCE(  
                dt.database_transaction_begin_time,  
                at.transaction_begin_time  
            ),  
            SYSDATETIME()  
        ) AS age_seconds,  
        dt.database_transaction_log_bytes_used,  
        dt.database_transaction_log_bytes_reserved,  
        s.status AS session_status,  
        s.login_name,  
        s.host_name,  
        s.program_name,  
        s.open_transaction_count,  
        ar.request_id,  
        ar.request_status,  
        ar.command,  
        ar.wait_type,  
        ar.blocking_session_id,  
        txt.text AS sql_text  
    FROM sys.dm_tran_database_transactions AS dt  
    INNER JOIN sys.dm_tran_session_transactions AS st  
        ON st.transaction_id = dt.transaction_id  
    INNER JOIN sys.dm_tran_active_transactions AS at  
        ON at.transaction_id = st.transaction_id  
    INNER JOIN sys.dm_exec_sessions AS s  
        ON s.session_id = st.session_id  
  
    OUTER APPLY  
    (  
        SELECT TOP (1)  
            r.request_id,  
            r.status AS request_status,  
            r.command,  
            r.wait_type,  
            r.blocking_session_id,  
            r.sql_handle  
        FROM sys.dm_exec_requests AS r  
        WHERE r.session_id = st.session_id  
        ORDER BY r.request_id  
    ) AS ar  
  
    OUTER APPLY  
    (  
        SELECT TOP (1)  
            c.most_recent_sql_handle  
        FROM sys.dm_exec_connections AS c  
        WHERE c.session_id = st.session_id  
        ORDER BY c.connect_time DESC  
    ) AS ec  
  
    OUTER APPLY sys.dm_exec_sql_text  
    (  
        COALESCE(ar.sql_handle, ec.most_recent_sql_handle)  
    ) AS txt  
  
    WHERE dt.database_id = DB_ID()  
      AND st.is_user_transaction = 1  
      AND s.is_user_process = 1  
      AND st.session_id <> @@SPID  
)  
SELECT TOP (100)  
    CASE  
        WHEN age_seconds >= 300  
          OR database_transaction_log_bytes_used >= 104857600  
          OR  
             (  
                 session_status = N'sleeping'  
                 AND age_seconds >= 60  
             )  
            THEN 'High'  
        WHEN age_seconds >= 60  
          OR session_status = N'sleeping'  
            THEN 'Medium'  
        ELSE 'Low'  
    END AS [Priority],  
  
    'Transaction' AS [Category],  
  
    CONCAT(  
        'transaction ',  
        transaction_id,  
        ' session ',  
        session_id  
    ) AS [Object],  
  
    CASE  
        WHEN session_status = N'sleeping'  
            THEN 'Sleeping session has an open transaction'  
        WHEN age_seconds >= 60  
            THEN 'Long-lived open transaction'  
        ELSE 'Open user transaction'  
    END AS [Finding],  
  
    CONCAT(  
        'database=', DB_NAME(),  
        '; session=', session_id,  
        '; transaction id=', transaction_id,  
        '; transaction name=', COALESCE(transaction_name, N'<unnamed>'),  
        '; transaction type=',  
        CASE transaction_type  
            WHEN 1 THEN 'read/write'  
            WHEN 2 THEN 'read-only'  
            WHEN 3 THEN 'system'  
            WHEN 4 THEN 'distributed'  
            ELSE CONCAT('unknown(', transaction_type, ')')  
        END,  
        '; transaction state=',  
        CASE database_transaction_state  
            WHEN 1 THEN 'not initialized'  
            WHEN 3 THEN 'initialized without log records'  
            WHEN 4 THEN 'generated log records'  
            WHEN 5 THEN 'prepared'  
            WHEN 10 THEN 'committed'  
            WHEN 11 THEN 'rolled back'  
            WHEN 12 THEN 'commit in progress'  
            ELSE CONCAT('unknown(', database_transaction_state, ')')  
        END,  
        '; begin=',  
        COALESCE(  
            CONVERT(varchar(19), transaction_begin_time, 120),  
            '<unknown>'  
        ),  
        '; age sec=', COALESCE(age_seconds, 0),  
        '; log used MB=',  
        CONVERT(  
            decimal(18,2),  
            COALESCE(database_transaction_log_bytes_used, 0) / 1048576.0  
        ),  
        '; log reserved MB=',  
        CONVERT(  
            decimal(18,2),  
            COALESCE(database_transaction_log_bytes_reserved, 0) / 1048576.0  
        ),  
        '; session status=', session_status,  
        '; open transaction count=', open_transaction_count,  
        '; request status=', COALESCE(request_status, N'<no active request>'),  
        '; command=', COALESCE(command, N'<none>'),  
        '; wait type=', COALESCE(wait_type, N'<none>'),  
        '; blocker=', COALESCE(blocking_session_id, 0),  
        '; login=', COALESCE(login_name, N'<unknown>'),  
        '; host=', COALESCE(host_name, N'<unknown>'),  
        '; application=', COALESCE(program_name, N'<unknown>'),  
        '; SQL=',  
        LEFT(  
            REPLACE(  
                REPLACE(  
                    COALESCE(sql_text, N'<SQL text unavailable>'),  
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
        WHEN session_status = N'sleeping'  
            THEN 'Identify why the application left the transaction open. Prefer COMMIT or ROLLBACK in the owning session; terminate it only after validating rollback impact.'  
        WHEN age_seconds >= 60  
            THEN 'Review transaction scope, blocking, log reuse, application timeouts, error handling, and whether external calls occur inside the transaction.'  
        ELSE  
            'Monitor the transaction and verify that it completes within the expected application transaction scope.'  
    END AS [Recommendation],  
  
    CONCAT(  
        '-- COMMIT or ROLLBACK must normally run in session ',  
        session_id,  
        '; if termination is approved from another session: KILL ',  
        session_id,  
        ';'  
    ) AS [SuggestedSql],  
  
    'High: ending a session can trigger a long rollback, release locks abruptly, and cause application-level inconsistencies'  
        AS [Risk]  
  
FROM OpenTransactions  
ORDER BY  
    age_seconds DESC,  
    database_transaction_log_bytes_used DESC;  
