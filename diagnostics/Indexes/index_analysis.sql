/*******************************************************************************
Script Name: index_analysis.sql
Purpose: Analyzes SQL Server indexes for usage patterns, duplicate key definitions, and physical fragmentation to identify cleanup or maintenance opportunities.
Scope: Current database
SQL Server: 2016+
Azure SQL: Azure SQL Database and Managed Instance; see docs/COMPATIBILITY.md
Permissions: VIEW SERVER STATE or VIEW DATABASE STATE, depending on the DMV; SQL Server 2022+ may require the corresponding PERFORMANCE STATE permission
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

;WITH sz AS (
    SELECT 
        object_id,
        index_id,
        SUM(used_page_count) AS pages 
    FROM sys.dm_db_partition_stats 
    GROUP BY object_id, index_id
),
frag AS (
    SELECT 
        object_id,
        index_id,
        MAX(avg_fragmentation_in_percent) AS fragmentation,
        SUM(page_count) AS frag_pages 
    FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') 
    GROUP BY object_id, index_id
),
cols AS (
    SELECT 
        i.object_id,
        i.index_id,
        STUFF((
            SELECT ',' + CONVERT(varchar(12), ic.column_id) + CASE WHEN ic.is_descending_key = 1 THEN 'D' ELSE 'A' END 
            FROM sys.index_columns ic 
            WHERE ic.object_id = i.object_id 
              AND ic.index_id = i.index_id 
              AND ic.key_ordinal > 0 
            ORDER BY ic.key_ordinal 
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 1, '') AS keysig
    FROM sys.indexes i 
    WHERE i.index_id > 0
),
dup AS (
    SELECT 
        a.object_id,
        a.index_id,
        bidx.name AS duplicate_of 
    FROM cols a 
    JOIN cols b 
        ON b.object_id = a.object_id 
       AND b.index_id < a.index_id 
       AND b.keysig = a.keysig 
    JOIN sys.indexes bidx 
        ON bidx.object_id = b.object_id 
       AND bidx.index_id = b.index_id
)
SELECT TOP (200)
    CASE 
        WHEN d.duplicate_of IS NOT NULL OR (ISNULL(u.user_updates, 0) >= 10000 AND ISNULL(u.user_seeks, 0) + ISNULL(u.user_scans, 0) + ISNULL(u.user_lookups, 0) = 0) THEN 'High' 
        ELSE 'Medium' 
    END AS [Priority],
    'Index' AS [Category],
    QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + '.' + QUOTENAME(OBJECT_NAME(i.object_id)) + '.' + QUOTENAME(i.name) AS [Object],
    CASE 
        WHEN d.duplicate_of IS NOT NULL THEN 'Duplicate keys of ' + QUOTENAME(d.duplicate_of)
        WHEN ISNULL(u.user_seeks, 0) + ISNULL(u.user_scans, 0) + ISNULL(u.user_lookups, 0) = 0 AND ISNULL(u.user_updates, 0) > 0 THEN 'Unused index (updates occurring without reads)'
        WHEN f.fragmentation >= 30 AND f.frag_pages >= 1000 THEN 'High fragmentation' 
        ELSE 'Moderate fragmentation' 
    END AS [Finding],
    CONCAT(
        'pages=', ISNULL(s.pages, 0), 
        '; frag=', CONVERT(decimal(6,2), ISNULL(f.fragmentation, 0)), 
        '%; seeks=', ISNULL(u.user_seeks, 0), 
        '; scans=', ISNULL(u.user_scans, 0), 
        '; lookups=', ISNULL(u.user_lookups, 0), 
        '; updates=', ISNULL(u.user_updates, 0)
    ) AS [Evidence],
    CASE 
        WHEN d.duplicate_of IS NOT NULL THEN 'Compare INCLUDE columns, filters, uniqueness, usage, and dependencies before consolidating.'
        WHEN ISNULL(u.user_seeks, 0) + ISNULL(u.user_scans, 0) + ISNULL(u.user_lookups, 0) = 0 THEN 'Observe over a representative operational window and verify foreign keys, query hints, batch processes, and constraints before dropping.'
        ELSE 'Prefer REORGANIZE for 10-30% fragmentation; evaluate REBUILD for >30% based on SQL Server edition, transaction logs, and maintenance windows.' 
    END AS [Recommendation],
    CASE 
        WHEN d.duplicate_of IS NOT NULL OR ISNULL(u.user_seeks, 0) + ISNULL(u.user_scans, 0) + ISNULL(u.user_lookups, 0) = 0 THEN '-- No automatic DROP generated: manual review required'
        WHEN f.fragmentation >= 30 THEN 'ALTER INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + '.' + QUOTENAME(OBJECT_NAME(i.object_id)) + ' REBUILD;'
        ELSE 'ALTER INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + '.' + QUOTENAME(OBJECT_NAME(i.object_id)) + ' REORGANIZE;' 
    END AS [SuggestedSql],
    'Medium/High: potential locks, log growth, storage impact, edition limits, and loss of query performance' AS [Risk]
FROM sys.indexes i 
LEFT JOIN sz s 
    ON s.object_id = i.object_id 
   AND s.index_id = i.index_id 
LEFT JOIN frag f 
    ON f.object_id = i.object_id 
   AND f.index_id = i.index_id
LEFT JOIN sys.dm_db_index_usage_stats u 
    ON u.database_id = DB_ID() 
   AND u.object_id = i.object_id 
   AND u.index_id = i.index_id 
LEFT JOIN dup d 
    ON d.object_id = i.object_id 
   AND d.index_id = i.index_id
WHERE i.index_id > 0 
  AND i.is_hypothetical = 0 
  AND i.is_disabled = 0 
  AND i.is_primary_key = 0 
  AND i.is_unique_constraint = 0
  AND (
      d.duplicate_of IS NOT NULL 
      OR (ISNULL(u.user_seeks, 0) + ISNULL(u.user_scans, 0) + ISNULL(u.user_lookups, 0) = 0 AND ISNULL(u.user_updates, 0) > 0) 
      OR (f.fragmentation >= 10 AND f.frag_pages >= 1000)
  )
ORDER BY 
    CASE 
        WHEN d.duplicate_of IS NOT NULL THEN 0 
        WHEN ISNULL(u.user_updates, 0) >= 10000 THEN 1 
        ELSE 2 
    END, 
    s.pages DESC;
