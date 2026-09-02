/*******************************************************************************
Script Name:      query_plan_candidates.sql
Description:      Searches the plan cache for expensive queries with implicit 
                  conversions, tempdb spills, or high-cost scans.
                  
Author:           TheMAxLab
Version:          1.0
License:          MIT

Usage:
  1. Connect to the target SQL Server instance.
  2. Select the database context: USE [YourDatabaseName];
  3. Execute the script in SSMS or Azure Data Studio.
*******************************************************************************/

SELECT TOP (100)
    CASE 
        WHEN qs.total_worker_time / NULLIF(qs.execution_count, 0) >= 1000000 
          OR qs.total_logical_reads / NULLIF(qs.execution_count, 0) >= 100000 THEN 'High' 
        ELSE 'Medium' 
    END AS [Priority],
    'Query' AS [Category],
    CONCAT('plan ', CONVERT(varchar(34), qs.plan_handle, 1)) AS [Object],
    CASE 
        WHEN CONVERT(nvarchar(max), qp.query_plan) LIKE '%CONVERT_IMPLICIT%' THEN 'Implicit conversion detected in plan'
        WHEN CONVERT(nvarchar(max), qp.query_plan) LIKE '%SpillToTempDb%' 
          OR CONVERT(nvarchar(max), qp.query_plan) LIKE '%SpillOccurred%' THEN 'Tempdb spill detected'
        WHEN CONVERT(nvarchar(max), qp.query_plan) LIKE '%Table Scan%' 
          OR CONVERT(nvarchar(max), qp.query_plan) LIKE '%Index Scan%' THEN 'Scan in high-resource query' 
        ELSE 'High average resource consuming query' 
    END AS [Finding],
    CONCAT(
        'executions=', qs.execution_count, 
        '; avg CPU ms=', CONVERT(decimal(18,2), qs.total_worker_time / NULLIF(qs.execution_count, 0) / 1000.0), 
        '; avg reads=', qs.total_logical_reads / NULLIF(qs.execution_count, 0), 
        '; last=', CONVERT(varchar(19), qs.last_execution_time, 120), 
        '; SQL=', LEFT(REPLACE(REPLACE(st.text, CHAR(13), ' '), CHAR(10), ' '), 1500)
    ) AS [Evidence],
    'Examine the XML execution plan, parameters, cardinality, statistics, predicate data types, and indexes; measure performance before/after.' AS [Recommendation],
    '-- Query tuning cannot be safely automated' AS [SuggestedSql],
    'High: refactoring queries or adding indexes may cause regressions or increase DML write cost' AS [Risk]
FROM sys.dm_exec_query_stats qs 
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st 
OUTER APPLY sys.dm_exec_query_plan(qs.plan_handle) qp
WHERE st.dbid = DB_ID() 
  AND (
      qs.total_worker_time / NULLIF(qs.execution_count, 0) >= 500000 
      OR qs.total_logical_reads / NULLIF(qs.execution_count, 0) >= 10000
      OR CONVERT(nvarchar(max), qp.query_plan) LIKE '%CONVERT_IMPLICIT%'
      OR CONVERT(nvarchar(max), qp.query_plan) LIKE '%SpillToTempDb%'
      OR CONVERT(nvarchar(max), qp.query_plan) LIKE '%SpillOccurred%'
  )
ORDER BY (qs.total_worker_time / NULLIF(qs.execution_count, 0)) DESC;
