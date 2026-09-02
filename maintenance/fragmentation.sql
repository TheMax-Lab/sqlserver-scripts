/*******************************************************************************  
Script Name:      fragmentation.sql  
Description:      Searches the current database for fragmented rowstore  
                  indexes and recommends REORGANIZE or REBUILD operations.  
Author:           TheMaxLab  
Version:          1.0  
License:          MIT  
  
Usage:  
  1. Connect to the target SQL Server instance.  
  2. Select the database context:  
       USE [YourDatabaseName];  
  3. Execute the script in SSMS or Azure Data Studio.  
  
Notes:  
  - Only rowstore clustered and nonclustered indexes are analyzed.  
  - Indexes smaller than 1,000 pages are excluded.  
  - Fragmentation below 10 percent is excluded.  
  - LIMITED mode is used to reduce the diagnostic overhead.  
  - REBUILD is generated without ONLINE = ON for broader compatibility.  
*******************************************************************************/  
  
;WITH [PartitionCounts] AS  
(  
    SELECT  
        p.[object_id],  
        p.[index_id],  
        COUNT_BIG(*) AS [partition_count]  
    FROM sys.partitions AS p  
    WHERE p.[index_id] > 0  
    GROUP BY  
        p.[object_id],  
        p.[index_id]  
)  
SELECT  
    CASE  
        WHEN ips.[avg_fragmentation_in_percent] >= 30.0 THEN 'High'  
        ELSE 'Medium'  
    END AS [Priority],  
  
    'Index' AS [Category],  
  
    CONCAT(  
        QUOTENAME(s.[name]), '.',  
        QUOTENAME(t.[name]), '.',  
        QUOTENAME(i.[name])  
    ) AS [Object],  
  
    CASE  
        WHEN ips.[avg_fragmentation_in_percent] >= 30.0  
            THEN 'High rowstore index fragmentation'  
        WHEN i.[allow_page_locks] = 0  
            THEN 'Moderate fragmentation; REORGANIZE is unavailable because page locks are disabled'  
        ELSE 'Moderate rowstore index fragmentation'  
    END AS [Finding],  
  
    CONCAT(  
        'fragmentation=',  
        CONVERT(  
            varchar(30),  
            CONVERT(decimal(9,2), ips.[avg_fragmentation_in_percent])  
        ),  
        '%; pages=',  
        ips.[page_count],  
        '; partition=',  
        ips.[partition_number],  
        CASE  
            WHEN pc.[partition_count] > 1 THEN ' of partitioned index'  
            ELSE ' of non-partitioned index'  
        END,  
        '; index type=',  
        i.[type_desc],  
        '; page locks=',  
        CASE i.[allow_page_locks]  
            WHEN 1 THEN 'enabled'  
            ELSE 'disabled'  
        END  
    ) AS [Evidence],  
  
    CASE  
        WHEN ips.[avg_fragmentation_in_percent] >= 30.0  
          OR i.[allow_page_locks] = 0  
            THEN 'Rebuild the affected index or partition during an approved maintenance window. Verify transaction log, tempdb, storage, blocking, and edition-specific online-operation support.'  
        ELSE 'Reorganize the affected index or partition. REORGANIZE does not refresh statistics, so evaluate the statistics report separately.'  
    END AS [Recommendation],  
  
    CASE  
        WHEN ips.[avg_fragmentation_in_percent] >= 30.0  
          OR i.[allow_page_locks] = 0  
        THEN  
            CONCAT(  
                'ALTER INDEX ',  
                QUOTENAME(i.[name]),  
                ' ON ',  
                QUOTENAME(s.[name]), '.',  
                QUOTENAME(t.[name]),  
                CASE  
                    WHEN pc.[partition_count] > 1  
                        THEN CONCAT(  
                            ' REBUILD PARTITION = ',  
                            ips.[partition_number]  
                        )  
                    ELSE ' REBUILD'  
                END,  
                ';'  
            )  
        ELSE  
            CONCAT(  
                'ALTER INDEX ',  
                QUOTENAME(i.[name]),  
                ' ON ',  
                QUOTENAME(s.[name]), '.',  
                QUOTENAME(t.[name]),  
                CASE  
                    WHEN pc.[partition_count] > 1  
                        THEN CONCAT(  
                            ' REORGANIZE PARTITION = ',  
                            ips.[partition_number]  
                        )  
                    ELSE ' REORGANIZE'  
                END,  
                ';'  
            )  
    END AS [SuggestedSql],  
  
    CASE  
        WHEN ips.[avg_fragmentation_in_percent] >= 30.0  
          OR i.[allow_page_locks] = 0  
            THEN 'High: REBUILD can cause blocking, transaction log growth, tempdb usage, additional I/O, and plan changes.'  
        ELSE 'Medium: REORGANIZE is normally online but generates transaction log activity and additional I/O.'  
    END AS [Risk]  
  
FROM sys.dm_db_index_physical_stats  
(  
    DB_ID(),  
    NULL,  
    NULL,  
    NULL,  
    'LIMITED'  
) AS ips  
INNER JOIN sys.indexes AS i  
    ON i.[object_id] = ips.[object_id]  
   AND i.[index_id]  = ips.[index_id]  
INNER JOIN sys.tables AS t  
    ON t.[object_id] = ips.[object_id]  
INNER JOIN sys.schemas AS s  
    ON s.[schema_id] = t.[schema_id]  
INNER JOIN [PartitionCounts] AS pc  
    ON pc.[object_id] = ips.[object_id]  
   AND pc.[index_id]  = ips.[index_id]  
WHERE  
    ips.[index_id] > 0  
    AND ips.[index_level] = 0  
    AND ips.[alloc_unit_type_desc] = 'IN_ROW_DATA'  
    AND ips.[page_count] >= 1000  
    AND ips.[avg_fragmentation_in_percent] >= 10.0  
    AND i.[type] IN (1, 2)  
    AND i.[is_disabled] = 0  
    AND i.[is_hypothetical] = 0  
    AND t.[is_ms_shipped] = 0  
ORDER BY  
    CASE  
        WHEN ips.[avg_fragmentation_in_percent] >= 30.0 THEN 1  
        ELSE 2  
    END,  
    ips.[avg_fragmentation_in_percent] DESC,  
    ips.[page_count] DESC;  
