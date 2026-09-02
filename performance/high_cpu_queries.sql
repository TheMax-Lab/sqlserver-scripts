/*******************************************************************************  
Script Name:      high_cpu_queries.sql  
Description:      Searches the plan cache for queries with high average or  
                  cumulative CPU consumption.  
  
Author:           TheMAxLab  
Version:          1.0  
License:          MIT  
  
Usage:  
  1. Connect to the target SQL Server instance.  
  2. Select the database context:  
       USE [YourDatabaseName];  
  3. Execute the script in SSMS or Azure Data Studio.  
  4. Required permissions:  
       - SQL Server 2019 and earlier: VIEW SERVER STATE  
       - SQL Server 2022 and later:   VIEW SERVER PERFORMANCE STATE  
  
Notes:  
  - total_worker_time is cumulative CPU time for the cached plan.  
  - Statistics are cumulative since the plan was compiled.  
  - A worker/elapsed ratio greater than 100% may indicate CPU consumed  
    by multiple parallel workers.  
  - Thresholds in the Priority and WHERE clauses can be customized.  
*******************************************************************************/  
  
SELECT TOP (100)  
    CASE  
        WHEN m.avg_cpu_ms >= 1000  
          OR m.total_cpu_ms >= 300000  
        THEN 'High'  
        ELSE 'Medium'  
    END AS [Priority],  
  
    'Query' AS [Category],  
  
    CONCAT(  
        'query ',  
        CONVERT(varchar(130), qs.query_hash, 1),  
        '; plan ',  
        CONVERT(varchar(130), qs.plan_handle, 1)  
    ) AS [Object],  
  
    CASE  
        WHEN m.worker_elapsed_pct >= 150  
         AND m.avg_cpu_ms >= 250  
            THEN 'Worker time materially exceeds elapsed time; inspect parallelism'  
  
        WHEN m.avg_cpu_ms >= 1000  
            THEN 'High average CPU consumption per execution'  
  
        WHEN m.total_cpu_ms >= 300000  
         AND qs.execution_count >= 1000  
            THEN 'High cumulative CPU amplified by execution frequency'  
  
        WHEN m.total_cpu_ms >= 300000  
            THEN 'High cumulative CPU consumption'  
  
        WHEN qs.execution_count >= 1000  
            THEN 'CPU consumption amplified by frequent executions'  
  
        WHEN m.avg_logical_reads >= 100000  
            THEN 'Elevated CPU associated with high logical I/O'  
  
        ELSE 'Elevated CPU consumption'  
    END AS [Finding],  
  
    CONCAT(  
        'executions=', qs.execution_count,  
  
        '; total CPU ms=',  
        CONVERT(decimal(28,2), m.total_cpu_ms),  
  
        '; avg CPU ms=',  
        CONVERT(decimal(28,2), m.avg_cpu_ms),  
  
        '; total elapsed ms=',  
        CONVERT(decimal(28,2), m.total_elapsed_ms),  
  
        '; avg elapsed ms=',  
        CONVERT(decimal(28,2), m.avg_elapsed_ms),  
  
        '; worker/elapsed %=',  
        COALESCE(  
            CONVERT(  
                varchar(40),  
                CONVERT(decimal(28,2), m.worker_elapsed_pct)  
            ),  
            'n/a'  
        ),  
  
        '; total logical reads=',  
        qs.total_logical_reads,  
  
        '; avg logical reads=',  
        CONVERT(decimal(28,2), m.avg_logical_reads),  
  
        '; cached since=',  
        CONVERT(varchar(19), qs.creation_time, 120),  
  
        '; last=',  
        CONVERT(varchar(19), qs.last_execution_time, 120),  
  
        '; plan XML=',  
        CASE  
            WHEN qp.query_plan IS NULL THEN 'unavailable'  
            ELSE 'available'  
        END,  
  
        '; SQL=',  
        COALESCE(  
            LEFT(  
                REPLACE(  
                    REPLACE(txt.statement_text, CHAR(13), N' '),  
                    CHAR(10),  
                    N' '  
                ),  
                1500  
            ),  
            N'<text unavailable or encrypted>'  
        )  
    ) AS [Evidence],  
  
    'Inspect the cached XML execution plan for expensive scans, inefficient '  
    + 'joins, sorts, hash operations, scalar expressions, functions, cardinality '  
    + 'errors and parallelism. Verify parameter sensitivity and statistics; '  
    + 'capture an actual execution plan when it can be done safely.' AS [Recommendation],  
  
    CONCAT(  
        'SELECT query_plan ',  
        'FROM sys.dm_exec_query_plan(',  
        CONVERT(varchar(130), qs.plan_handle, 1),  
        ');'  
    ) AS [SuggestedSql],  
  
    'Low for read-only plan inspection; High for query, index, parallelism, '  
    + 'or configuration changes, which may cause workload regressions.' AS [Risk]  
  
FROM sys.dm_exec_query_stats AS qs  
  
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st  
  
CROSS APPLY  
(  
    SELECT  
        SUBSTRING(  
            st.text,  
            (qs.statement_start_offset / 2) + 1,  
            (  
                (  
                    CASE qs.statement_end_offset  
                        WHEN -1 THEN DATALENGTH(st.text)  
                        ELSE qs.statement_end_offset  
                    END  
                    - qs.statement_start_offset  
                ) / 2  
            ) + 1  
        ) AS statement_text  
) AS txt  
  
OUTER APPLY sys.dm_exec_query_plan(qs.plan_handle) AS qp  
  
CROSS APPLY  
(  
    SELECT  
        CONVERT(decimal(28,4), qs.total_worker_time)  
            / 1000.0 AS total_cpu_ms,  
  
        CONVERT(decimal(28,4), qs.total_worker_time)  
            / NULLIF(CONVERT(decimal(28,4), qs.execution_count), 0)  
            / 1000.0 AS avg_cpu_ms,  
  
        CONVERT(decimal(28,4), qs.total_elapsed_time)  
            / 1000.0 AS total_elapsed_ms,  
  
        CONVERT(decimal(28,4), qs.total_elapsed_time)  
            / NULLIF(CONVERT(decimal(28,4), qs.execution_count), 0)  
            / 1000.0 AS avg_elapsed_ms,  
  
        CONVERT(decimal(28,4), qs.total_logical_reads)  
            / NULLIF(CONVERT(decimal(28,4), qs.execution_count), 0)  
            AS avg_logical_reads,  
  
        CASE  
            WHEN qs.total_elapsed_time > 0  
            THEN  
                CONVERT(decimal(28,4), qs.total_worker_time)  
                * 100.0  
                / CONVERT(decimal(28,4), qs.total_elapsed_time)  
            ELSE NULL  
        END AS worker_elapsed_pct  
) AS m  
  
WHERE st.dbid = DB_ID()  
  AND  
  (  
       m.total_cpu_ms >= 10000  
    OR m.avg_cpu_ms >= 250  
  )  
  
ORDER BY  
    CASE  
        WHEN m.avg_cpu_ms >= 1000  
          OR m.total_cpu_ms >= 300000  
        THEN 0  
        ELSE 1  
    END,  
    m.total_cpu_ms DESC,  
    m.avg_cpu_ms DESC  
  
OPTION (RECOMPILE);  
