/*******************************************************************************
Script Name: tempdb_usage.sql
Purpose: Reports overall tempdb data-file utilization and the top user sessions consuming tempdb space.
Scope: SQL Server instance; tempdb
SQL Server: 2016+
Azure SQL: Azure SQL support varies for instance-level DMVs; see docs/COMPATIBILITY.md
Permissions: VIEW SERVER STATE or VIEW DATABASE STATE, depending on scope; SQL Server 2022+ may require the corresponding PERFORMANCE STATE permission
Risk: Read-only; review and test any generated SQL before execution.
Output: Overall tempdb, file-level, and top-session findings
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/  
  
;WITH FileUsage AS  
(  
    SELECT  
        df.file_id,  
        CONVERT(bigint, df.size) AS total_pages,  
        CONVERT(bigint, fs.unallocated_extent_page_count)  
            AS free_pages,  
        CONVERT(bigint, fs.version_store_reserved_page_count)  
            AS version_store_pages,  
        CONVERT(bigint, fs.user_object_reserved_page_count)  
            AS user_object_pages,  
        CONVERT(bigint, fs.internal_object_reserved_page_count)  
            AS internal_object_pages,  
        CONVERT(bigint, fs.mixed_extent_page_count)  
            AS mixed_extent_pages  
    FROM tempdb.sys.database_files AS df  
    INNER JOIN tempdb.sys.dm_db_file_space_usage AS fs  
        ON fs.file_id = df.file_id  
    WHERE df.[type] = 0  
),  
TempdbTotals AS  
(  
    SELECT  
        SUM(total_pages) AS total_pages,  
        SUM(free_pages) AS free_pages,  
        SUM(version_store_pages) AS version_store_pages,  
        SUM(user_object_pages) AS user_object_pages,  
        SUM(internal_object_pages) AS internal_object_pages,  
        SUM(mixed_extent_pages) AS mixed_extent_pages  
    FROM FileUsage  
),  
TempdbMetrics AS  
(  
    SELECT  
        total_pages,  
        total_pages - free_pages AS used_pages,  
        free_pages,  
        version_store_pages,  
        user_object_pages,  
        internal_object_pages,  
        mixed_extent_pages,  
        CONVERT  
        (  
            decimal(9,2),  
            (total_pages - free_pages) * 100.0  
                / NULLIF(total_pages, 0)  
        ) AS used_percent  
    FROM TempdbTotals  
),  
TaskUsage AS  
(  
    SELECT  
        tsu.session_id,  
        SUM  
        (  
            CONVERT(bigint, tsu.user_objects_alloc_page_count)  
            - CONVERT(bigint, tsu.user_objects_dealloc_page_count)  
        ) AS task_user_pages,  
        SUM  
        (  
            CONVERT(bigint, tsu.internal_objects_alloc_page_count)  
            - CONVERT(bigint, tsu.internal_objects_dealloc_page_count)  
        ) AS task_internal_pages  
    FROM tempdb.sys.dm_db_task_space_usage AS tsu  
    GROUP BY  
        tsu.session_id  
),  
SessionUsage AS  
(  
    SELECT  
        su.session_id,  
        s.status AS session_status,  
        s.login_name,  
        s.host_name,  
        s.program_name,  
        s.open_transaction_count,  
  
        (  
            CONVERT(bigint, su.user_objects_alloc_page_count)  
            - CONVERT(bigint, su.user_objects_dealloc_page_count)  
            + COALESCE(tu.task_user_pages, 0)  
        ) AS user_pages,  
  
        (  
            CONVERT(bigint, su.internal_objects_alloc_page_count)  
            - CONVERT(bigint, su.internal_objects_dealloc_page_count)  
            + COALESCE(tu.task_internal_pages, 0)  
        ) AS internal_pages  
  
    FROM tempdb.sys.dm_db_session_space_usage AS su  
    INNER JOIN sys.dm_exec_sessions AS s  
        ON s.session_id = su.session_id  
    LEFT JOIN TaskUsage AS tu  
        ON tu.session_id = su.session_id  
    WHERE su.session_id <> @@SPID  
      AND s.is_user_process = 1  
),  
SessionTotals AS  
(  
    SELECT  
        session_id,  
        session_status,  
        login_name,  
        host_name,  
        program_name,  
        open_transaction_count,  
        user_pages,  
        internal_pages,  
        user_pages + internal_pages AS total_pages  
    FROM SessionUsage  
),  
TopConsumers AS  
(  
    SELECT TOP (50)  
        session_id,  
        session_status,  
        login_name,  
        host_name,  
        program_name,  
        open_transaction_count,  
        user_pages,  
        internal_pages,  
        total_pages  
    FROM SessionTotals  
    WHERE total_pages >= 1280  -- 10 MB  
    ORDER BY  
        total_pages DESC  
),  
ConsumerDetails AS  
(  
    SELECT  
        c.session_id,  
        c.session_status,  
        c.login_name,  
        c.host_name,  
        c.program_name,  
        c.open_transaction_count,  
        c.user_pages,  
        c.internal_pages,  
        c.total_pages,  
        ar.request_id,  
        ar.request_status,  
        ar.command,  
        ar.database_id AS request_database_id,  
        ar.wait_type,  
        ar.blocking_session_id,  
        txt.text AS sql_text  
    FROM TopConsumers AS c  
  
    OUTER APPLY  
    (  
        SELECT TOP (1)  
            r.request_id,  
            r.status AS request_status,  
            r.command,  
            r.database_id,  
            r.wait_type,  
            r.blocking_session_id,  
            r.sql_handle  
        FROM sys.dm_exec_requests AS r  
        WHERE r.session_id = c.session_id  
        ORDER BY r.request_id  
    ) AS ar  
  
    OUTER APPLY  
    (  
        SELECT TOP (1)  
            ec.most_recent_sql_handle  
        FROM sys.dm_exec_connections AS ec  
        WHERE ec.session_id = c.session_id  
        ORDER BY ec.connect_time DESC  
    ) AS ec  
  
    OUTER APPLY sys.dm_exec_sql_text  
    (  
        COALESCE(ar.sql_handle, ec.most_recent_sql_handle)  
    ) AS txt  
),  
Diagnostics AS  
(  
    SELECT  
        0 AS SortGroup,  
        t.used_pages AS SortValue,  
  
        CASE  
            WHEN t.used_percent >= 85 THEN 'High'  
            WHEN t.used_percent >= 70 THEN 'Medium'  
            ELSE 'Low'  
        END AS [Priority],  
  
        'Tempdb' AS [Category],  
  
        'tempdb data files' AS [Object],  
  
        CASE  
            WHEN t.used_percent >= 85  
                THEN 'Critical tempdb data-file utilization'  
            WHEN t.used_percent >= 70  
                THEN 'Elevated tempdb data-file utilization'  
            WHEN t.version_store_pages >= t.user_object_pages  
             AND t.version_store_pages >= t.internal_object_pages  
                THEN 'Current tempdb utilization is dominated by the version store'  
            WHEN t.internal_object_pages >= t.user_object_pages  
                THEN 'Current tempdb utilization is dominated by internal objects'  
            ELSE 'Current tempdb space utilization'  
        END AS [Finding],  
  
        CONCAT(  
            'total MB=',  
            CONVERT(decimal(18,2), t.total_pages * 8.0 / 1024),  
            '; used MB=',  
            CONVERT(decimal(18,2), t.used_pages * 8.0 / 1024),  
            '; free MB=',  
            CONVERT(decimal(18,2), t.free_pages * 8.0 / 1024),  
            '; used percent=', t.used_percent,  
            '; version store MB=',  
            CONVERT(decimal(18,2), t.version_store_pages * 8.0 / 1024),  
            '; user objects MB=',  
            CONVERT(decimal(18,2), t.user_object_pages * 8.0 / 1024),  
            '; internal objects MB=',  
            CONVERT(decimal(18,2), t.internal_object_pages * 8.0 / 1024),  
            '; mixed extents MB=',  
            CONVERT(decimal(18,2), t.mixed_extent_pages * 8.0 / 1024)  
        ) AS [Evidence],  
  
        CASE  
            WHEN t.used_percent >= 70  
                THEN 'Identify top sessions, version-store retention, large sorts, hashes, temporary objects, spills, file-growth settings, and available disk capacity.'  
            WHEN t.version_store_pages >= t.user_object_pages  
             AND t.version_store_pages >= t.internal_object_pages  
                THEN 'Monitor long-running snapshot transactions, read-committed snapshot workloads, online operations, and version-store cleanup.'  
            ELSE  
                'Trend tempdb utilization and verify equal data-file sizing, growth configuration, and sufficient storage headroom.'  
        END AS [Recommendation],  
  
        '-- Investigate consumers and storage capacity before growing, shrinking, or restarting SQL Server'  
            AS [SuggestedSql],  
  
        'High: tempdb exhaustion can stop queries and maintenance; shrinking or restarting can interrupt workloads and hide the root cause'  
            AS [Risk]  
  
    FROM TempdbMetrics AS t  
  
    UNION ALL  
  
    SELECT  
        1,  
        c.total_pages,  
  
        CASE  
            WHEN c.total_pages >= 131072 THEN 'High'    -- 1 GB  
            WHEN c.total_pages >= 12800 THEN 'Medium'   -- 100 MB  
            ELSE 'Low'  
        END,  
  
        'Tempdb Session',  
  
        CONCAT('session ', c.session_id),  
  
        CASE  
            WHEN c.internal_pages >= c.user_pages  
                THEN 'Session consuming tempdb for internal objects'  
            ELSE 'Session consuming tempdb for user objects'  
        END,  
  
        CONCAT(  
            'session=', c.session_id,  
            '; database=',  
            COALESCE(DB_NAME(c.request_database_id), N'<no active request>'),  
            '; total MB=',  
            CONVERT(decimal(18,2), c.total_pages * 8.0 / 1024),  
            '; user objects MB=',  
            CONVERT(decimal(18,2), c.user_pages * 8.0 / 1024),  
            '; internal objects MB=',  
            CONVERT(decimal(18,2), c.internal_pages * 8.0 / 1024),  
            '; session status=', c.session_status,  
            '; request status=',  
            COALESCE(c.request_status, N'<no active request>'),  
            '; command=', COALESCE(c.command, N'<none>'),  
            '; wait type=', COALESCE(c.wait_type, N'<none>'),  
            '; blocker=', COALESCE(c.blocking_session_id, 0),  
            '; open transactions=', c.open_transaction_count,  
            '; login=', COALESCE(c.login_name, N'<unknown>'),  
            '; host=', COALESCE(c.host_name, N'<unknown>'),  
            '; application=', COALESCE(c.program_name, N'<unknown>'),  
            '; SQL=',  
            LEFT(  
                REPLACE(  
                    REPLACE(  
                        COALESCE(c.sql_text, N'<SQL text unavailable>'),  
                        CHAR(13),  
                        N' '  
                    ),  
                    CHAR(10),  
                    N' '  
                ),  
                1500  
            )  
        ),  
  
        'Inspect the execution plan for spills, large sorts, hashes, spools, temporary tables, row versioning, inaccurate estimates, and excessive memory grant pressure.',  
  
        CONCAT(  
            '-- Review the workload first; if cancellation is approved: KILL ',  
            c.session_id,  
            ';'  
        ),  
  
        'High: cancelling a tempdb consumer can trigger rollback, block other sessions, and interrupt application or maintenance operations'  
  
    FROM ConsumerDetails AS c  
)  
SELECT  
    [Priority],  
    [Category],  
    [Object],  
    [Finding],  
    [Evidence],  
    [Recommendation],  
    [SuggestedSql],  
    [Risk]  
FROM Diagnostics  
ORDER BY  
    SortGroup,  
    SortValue DESC;  
