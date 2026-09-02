/*******************************************************************************  
Script Name:      expensive_queries.sql  
Description:      Searches the plan cache for queries with high average or  
                  cumulative elapsed time, CPU usage, logical/physical reads,  
                  or logical writes.  
  
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
  - Results are based on the current plan cache.  
  - Statistics are cumulative since the plan was compiled.  
  - Results are reset after restart, failover, recompile, or plan eviction.  
  - Thresholds in the Priority and WHERE clauses can be customized.  
*******************************************************************************/  
  
SELECT TOP (100)  
    CASE  
        WHEN m.avg_elapsed_ms >= 5000  
          OR m.avg_cpu_ms >= 2000  
          OR m.avg_logical_reads >= 100000  
          OR m.avg_physical_reads >= 10000  
          OR m.avg_logical_writes >= 10000  
          OR m.total_elapsed_ms >= 300000  
          OR m.total_cpu_ms >= 300000  
          OR qs.total_logical_reads >= 5000000  
          OR qs.total_physical_reads >= 1000000  
          OR qs.total_logical_writes >= 1000000  
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
        WHEN m.avg_elapsed_ms >= 5000  
            THEN 'High average duration per execution'  
  
        WHEN m.avg_cpu_ms >= 2000  
            THEN 'High average CPU consumption per execution'  
  
        WHEN m.avg_logical_reads >= 100000  
            THEN 'High average logical reads per execution'  
  
        WHEN m.avg_physical_reads >= 10000  
            THEN 'High average physical reads per execution'  
  
        WHEN m.avg_logical_writes >= 10000  
            THEN 'High average logical writes per execution'  
  
        WHEN m.total_cpu_ms >= 60000  
            THEN 'High cumulative CPU consumption'  
  
        WHEN m.total_elapsed_ms >= 60000  
            THEN 'High cumulative elapsed time'  
  
        WHEN qs.total_logical_reads >= 1000000  
            THEN 'High cumulative logical reads'  
  
        WHEN qs.total_physical_reads >= 100000  
            THEN 'High cumulative physical reads'  
  
        WHEN qs.total_logical_writes >= 100000  
            THEN 'High cumulative logical writes'  
  
        ELSE 'Elevated resource consumption'  
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
  
        '; total logical reads=',  
        qs.total_logical_reads,  
  
        '; avg logical reads=',  
        CONVERT(decimal(28,2), m.avg_logical_reads),  
  
        '; total physical reads=',  
        qs.total_physical_reads,  
  
        '; avg physical reads=',  
        CONVERT(decimal(28,2), m.avg_physical_reads),  
  
        '; total logical writes=',  
        qs.total_logical_writes,  
  
        '; avg logical writes=',  
        CONVERT(decimal(28,2), m.avg_logical_writes),  
  
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
  
    'Inspect the cached XML execution plan; validate cardinality estimates, '  
    + 'parameter sensitivity, statistics, scans, joins, sorts, hash operators, '  
    + 'spills, predicates, data types and indexes. Measure performance and DML '  
    + 'overhead before and after any change.' AS [Recommendation],  
  
    CONCAT(  
        'SELECT query_plan ',  
        'FROM sys.dm_exec_query_plan(',  
        CONVERT(varchar(130), qs.plan_handle, 1),  
        ');'  
    ) AS [SuggestedSql],  
  
    'Low for read-only plan inspection; High for query or index changes, '  
    + 'which may cause regressions or increase DML and storage costs.' AS [Risk]  
  
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
  
        CONVERT(decimal(28,4), qs.total_physical_reads)  
            / NULLIF(CONVERT(decimal(28,4), qs.execution_count), 0)  
            AS avg_physical_reads,  
  
        CONVERT(decimal(28,4), qs.total_logical_writes)  
            / NULLIF(CONVERT(decimal(28,4), qs.execution_count), 0)  
            AS avg_logical_writes  
) AS m  
  
WHERE st.dbid = DB_ID()  
  AND  
  (  
       m.avg_elapsed_ms >= 500  
    OR m.avg_cpu_ms >= 250  
    OR m.avg_logical_reads >= 10000  
    OR m.avg_physical_reads >= 1000  
    OR m.avg_logical_writes >= 1000  
    OR m.total_elapsed_ms >= 60000  
    OR m.total_cpu_ms >= 60000  
    OR qs.total_logical_reads >= 1000000  
    OR qs.total_physical_reads >= 100000  
    OR qs.total_logical_writes >= 100000  
  )  
  
ORDER BY  
    CASE  
        WHEN m.avg_elapsed_ms >= 5000  
          OR m.avg_cpu_ms >= 2000  
          OR m.avg_logical_reads >= 100000  
          OR m.avg_physical_reads >= 10000  
          OR m.avg_logical_writes >= 10000  
          OR m.total_elapsed_ms >= 300000  
          OR m.total_cpu_ms >= 300000  
          OR qs.total_logical_reads >= 5000000  
          OR qs.total_physical_reads >= 1000000  
          OR qs.total_logical_writes >= 1000000  
        THEN 0  
        ELSE 1  
    END,  
  
    (  
          m.avg_elapsed_ms / 500.0  
        + m.avg_cpu_ms / 250.0  
        + m.avg_logical_reads / 10000.0  
        + m.avg_physical_reads / 1000.0  
        + m.avg_logical_writes / 1000.0  
        + m.total_elapsed_ms / 60000.0  
        + m.total_cpu_ms / 60000.0  
        + CONVERT(decimal(28,4), qs.total_logical_reads) / 1000000.0  
        + CONVERT(decimal(28,4), qs.total_physical_reads) / 100000.0  
        + CONVERT(decimal(28,4), qs.total_logical_writes) / 100000.0  
    ) DESC  
  
OPTION (RECOMPILE);  
