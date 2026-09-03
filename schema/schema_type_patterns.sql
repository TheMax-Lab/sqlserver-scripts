/*******************************************************************************
Script Name: schema_type_patterns.sql
Purpose: Detects structural anti-patterns such as missing Primary Keys, large Heaps, and deprecated or unbounded LOB data types.
Scope: Current database
SQL Server: 2016+
Azure SQL: Azure SQL Database and Managed Instance; see docs/COMPATIBILITY.md
Permissions: Metadata visibility; physical-statistics scripts require VIEW DATABASE STATE
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

;WITH table_rows AS (
    SELECT 
        object_id,
        SUM(CASE WHEN index_id IN (0,1) THEN rows ELSE 0 END) AS rows 
    FROM sys.partitions 
    GROUP BY object_id
), 
findings AS (
    SELECT 
        t.object_id,
        'Table without Primary Key' AS finding,
        'Define a stable Primary Key if the entity supports one.' AS recommendation,
        'High: requires data model and application changes' AS risk
    FROM sys.tables t 
    WHERE t.is_ms_shipped = 0 
      AND NOT EXISTS (
          SELECT 1 
          FROM sys.indexes i 
          WHERE i.object_id = t.object_id AND i.is_primary_key = 1
      )
    
    UNION ALL 
    
    SELECT 
        t.object_id,
        'Heap with >10,000 rows',
        'Evaluate adding a narrow, stable, and monotonically increasing Clustered Index.',
        'High: impacts storage layout, locking, and potential workload dependencies'
    FROM sys.tables t 
    JOIN table_rows r ON r.object_id = t.object_id 
    WHERE t.is_ms_shipped = 0 
      AND r.rows >= 10000 
      AND EXISTS (
          SELECT 1 
          FROM sys.indexes i 
          WHERE i.object_id = t.object_id AND i.index_id = 0
      )
    
    UNION ALL 
    
    SELECT 
        c.object_id,
        'Deprecated or unbounded LOB data type: ' + QUOTENAME(c.name),
        'Evaluate modern data types and lengths consistent with actual data.',
        'High: data conversion and application compatibility risk'
    FROM sys.columns c 
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id 
    JOIN sys.tables t ON t.object_id = c.object_id 
    WHERE t.is_ms_shipped = 0 
      AND (
          ty.name IN ('text', 'ntext', 'image') 
          OR (c.max_length = -1 AND ty.name IN ('varchar', 'nvarchar', 'varbinary'))
      )
)
SELECT 
    CASE WHEN ISNULL(r.rows,0) >= 1000000 THEN 'High' ELSE 'Medium' END AS [Priority],
    'Schema' AS [Category],
    QUOTENAME(OBJECT_SCHEMA_NAME(f.object_id)) + '.' + QUOTENAME(OBJECT_NAME(f.object_id)) AS [Object],
    f.finding AS [Finding],
    CONCAT('rows=', ISNULL(r.rows, 0)) AS [Evidence],
    f.recommendation AS [Recommendation],
    '-- Requires refactoring and testing; no automatic script provided' AS [SuggestedSql],
    f.risk AS [Risk]
FROM findings f 
LEFT JOIN table_rows r ON r.object_id = f.object_id 
ORDER BY r.rows DESC;
