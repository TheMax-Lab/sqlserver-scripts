/*******************************************************************************
Script Name:      missing_indexes.sql
Description:      Identifies high-impact missing nonclustered indexes for the 
                  current database using SQL Server Dynamic Management Views 
                  (DMVs). Generates DDL scripts with priority levels.
                  
Author:           TheMaxLab
Version:          1.0
License:          MIT

Usage:
  1. Connect to the target SQL Server instance.
  2. Select the database context: USE [YourDatabaseName];
  3. Execute the script in SQL Server Management Studio (SSMS) or 
     Azure Data Studio.
     
Notes:
  - DMV statistics reset when the SQL Server service restarts.
  - Review and test all generated DDL statements before applying them to
    production environments.
*******************************************************************************/

SELECT TOP (100)
    CASE 
        WHEN gs.avg_total_user_cost * gs.avg_user_impact * (gs.user_seeks + gs.user_scans) >= 100000 THEN 'High' 
        WHEN gs.avg_user_impact >= 70 THEN 'Medium' 
        ELSE 'Low' 
    END AS [Priority],
    'Missing Index' AS [Category],
    QUOTENAME(OBJECT_SCHEMA_NAME(d.object_id)) + '.' + QUOTENAME(OBJECT_NAME(d.object_id)) AS [Object],
    'The DMV reports a potential missing nonclustered index' AS [Finding],
    CONCAT(
        'Impact=', CONVERT(decimal(6,2), gs.avg_user_impact), 
        '%; seeks=', gs.user_seeks, 
        '; scans=', gs.user_scans, 
        '; avg cost=', CONVERT(decimal(18,2), gs.avg_total_user_cost), 
        '; last=', CONVERT(varchar(19), gs.last_user_seek, 120)
    ) AS [Evidence],
    'Compare with existing indexes, consolidate similar index recommendations, and validate against a full workload cycle.' AS [Recommendation],
    'CREATE NONCLUSTERED INDEX ' + QUOTENAME(LEFT('IX_SQLMax_' + OBJECT_NAME(d.object_id) + '_' + CONVERT(varchar(12), d.index_handle), 128)) + 
    ' ON ' + QUOTENAME(OBJECT_SCHEMA_NAME(d.object_id)) + '.' + QUOTENAME(OBJECT_NAME(d.object_id)) + 
    ' (' + ISNULL(d.equality_columns, '') + 
    CASE WHEN d.equality_columns IS NOT NULL AND d.inequality_columns IS NOT NULL THEN ',' ELSE '' END + 
    ISNULL(d.inequality_columns, '') + ')' + 
    CASE WHEN d.included_columns IS NULL THEN '' ELSE ' INCLUDE (' + d.included_columns + ')' END + ';' AS [SuggestedSql],
    'Medium/High: potential index redundancy, increased storage usage, and additional write overhead (INSERT/UPDATE/DELETE)' AS [Risk]
FROM sys.dm_db_missing_index_details d 
JOIN sys.dm_db_missing_index_groups g 
    ON g.index_handle = d.index_handle
JOIN sys.dm_db_missing_index_group_stats gs 
    ON gs.group_handle = g.index_group_handle
WHERE d.database_id = DB_ID() 
  AND d.object_id > 0
ORDER BY gs.avg_total_user_cost * gs.avg_user_impact * (gs.user_seeks + gs.user_scans) DESC;
