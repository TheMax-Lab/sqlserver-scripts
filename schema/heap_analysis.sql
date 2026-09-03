/*******************************************************************************
Script Name: heap_analysis.sql
Purpose: Finds heap tables and evaluates their size, forwarded records, page density, extent fragmentation, and nonclustered indexes. Physical statistics are collected using SAMPLED mode.
Scope: Current database
SQL Server: 2016+
Azure SQL: Azure SQL Database and Managed Instance; see docs/COMPATIBILITY.md
Permissions: Metadata visibility; physical-statistics scripts require VIEW DATABASE STATE
Risk: Read-only; potentially high query cost because physical statistics use SAMPLED mode.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/  
  
;WITH HeapSize AS  
(  
    SELECT  
        dps.object_id,  
        SUM(CONVERT(bigint, dps.row_count)) AS RowCount,  
        SUM(CONVERT(bigint, dps.reserved_page_count)) AS ReservedPageCount,  
        SUM(CONVERT(bigint, dps.used_page_count)) AS UsedPageCount  
    FROM sys.dm_db_partition_stats AS dps  
    WHERE dps.index_id = 0  
    GROUP BY  
        dps.object_id  
)  
SELECT  
    CASE  
        WHEN  
        (  
            COALESCE(ph.ForwardedRecords, 0) >= 100000  
            OR  
            (  
                COALESCE(ph.ForwardedRecords, 0) >= 1000  
                AND metrics.ForwardedPercent >= 5.00  
            )  
        )  
        THEN 'High'  
        ELSE 'Medium'  
    END AS [Priority],  
    'Schema' AS [Category],  
    CONCAT  
    (  
        QUOTENAME(s.name),  
        '.',  
        QUOTENAME(t.name)  
    ) AS [Object],  
    CASE  
        WHEN COALESCE(ph.ForwardedRecords, 0) > 0 THEN  
            'Heap contains forwarded records'  
        WHEN hs.ReservedPageCount >= 12800  
          OR hs.RowCount >= 1000000 THEN  
            'Large table is stored as a heap'  
        ELSE  
            'Table is stored as a heap'  
    END AS [Finding],  
    CONCAT  
    (  
        'rows=', hs.RowCount,  
        '; reserved MB=',  
        CONVERT  
        (  
            decimal(18,2),  
            CONVERT(float, hs.ReservedPageCount) * 8.0 / 1024.0  
        ),  
        '; used MB=',  
        CONVERT  
        (  
            decimal(18,2),  
            CONVERT(float, hs.UsedPageCount) * 8.0 / 1024.0  
        ),  
        '; nonclustered indexes=',  
        COALESCE(ni.NonclusteredIndexCount, 0),  
        '; forwarded rows=',  
        COALESCE(ph.ForwardedRecords, 0),  
        '; forwarded percent=',  
        metrics.ForwardedPercent,  
        '; average page density percent=',  
        COALESCE  
        (  
            CONVERT  
            (  
                varchar(30),  
                CONVERT(decimal(9,2), ph.AveragePageDensityPercent)  
            ),  
            'n/a'  
        ),  
        '; extent fragmentation percent=',  
        COALESCE  
        (  
            CONVERT  
            (  
                varchar(30),  
                CONVERT(decimal(9,2), ph.ExtentFragmentationPercent)  
            ),  
            'n/a'  
        ),  
        '; physical stats mode=SAMPLED'  
    ) AS [Evidence],  
    CASE  
        WHEN COALESCE(ph.ForwardedRecords, 0) > 0 THEN  
            'Review variable-length updates and row growth. Test a heap rebuild as a short-term correction. Consider a clustered index only when access patterns and a suitable clustering key justify it.'  
        WHEN hs.ReservedPageCount >= 12800  
          OR hs.RowCount >= 1000000 THEN  
            'Evaluate table access patterns, range queries, lookup frequency, ETL behavior, and candidate clustering keys. A heap can be intentional, but large heaps should have a documented design rationale.'  
        ELSE  
            'Confirm that heap storage is intentional. Small staging, queue, or transient tables can be valid heaps; monitor growth and forwarded records.'  
    END AS [Recommendation],  
    CASE  
        WHEN COALESCE(ph.ForwardedRecords, 0) > 0 THEN  
            CONCAT  
            (  
                '-- Test duration, blocking, log usage, and required free space before execution.',  
                CHAR(13),  
                CHAR(10),  
                'ALTER TABLE ',  
                QUOTENAME(s.name),  
                '.',  
                QUOTENAME(t.name),  
                ' REBUILD;'  
            )  
        ELSE  
            '-- No automatic change recommended. A clustered key cannot be selected safely without workload and data analysis.'  
    END AS [SuggestedSql],  
    'High: rebuilding a heap or creating a clustered index can require substantial log and temporary space, block concurrent activity, and rebuild nonclustered indexes because row locators change.' AS [Risk]  
FROM sys.tables AS t  
INNER JOIN sys.schemas AS s  
    ON s.schema_id = t.schema_id  
INNER JOIN sys.indexes AS heap_index  
    ON heap_index.object_id = t.object_id  
   AND heap_index.index_id = 0  
INNER JOIN HeapSize AS hs  
    ON hs.object_id = t.object_id  
OUTER APPLY  
(  
    SELECT  
        SUM  
        (  
            CONVERT  
            (  
                bigint,  
                COALESCE(ips.forwarded_record_count, 0)  
            )  
        ) AS ForwardedRecords,  
        SUM  
        (  
            CASE  
                WHEN ips.avg_page_space_used_in_percent IS NOT NULL THEN  
                    CONVERT(float, ips.avg_page_space_used_in_percent)  
                    * CONVERT(float, ips.page_count)  
            END  
        )  
        /  
        NULLIF  
        (  
            SUM  
            (  
                CASE  
                    WHEN ips.avg_page_space_used_in_percent IS NOT NULL THEN  
                        CONVERT(float, ips.page_count)  
                END  
            ),  
            0.0  
        ) AS AveragePageDensityPercent,  
        SUM  
        (  
            CASE  
                WHEN ips.avg_fragmentation_in_percent IS NOT NULL THEN  
                    CONVERT(float, ips.avg_fragmentation_in_percent)  
                    * CONVERT(float, ips.page_count)  
            END  
        )  
        /  
        NULLIF  
        (  
            SUM  
            (  
                CASE  
                    WHEN ips.avg_fragmentation_in_percent IS NOT NULL THEN  
                        CONVERT(float, ips.page_count)  
                END  
            ),  
            0.0  
        ) AS ExtentFragmentationPercent  
    FROM sys.dm_db_index_physical_stats  
    (  
        DB_ID(),  
        t.object_id,  
        0,  
        NULL,  
        'SAMPLED'  
    ) AS ips  
    WHERE ips.index_id = 0  
      AND ips.index_level = 0  
      AND ips.alloc_unit_type_desc = 'IN_ROW_DATA'  
) AS ph  
OUTER APPLY  
(  
    SELECT  
        COUNT(*) AS NonclusteredIndexCount  
    FROM sys.indexes AS i  
    WHERE i.object_id = t.object_id  
      AND i.index_id > 0  
      AND i.is_hypothetical = 0  
) AS ni  
CROSS APPLY  
(  
    VALUES  
    (  
        CONVERT  
        (  
            decimal(9,2),  
            COALESCE  
            (  
                100.0  
                * CONVERT(float, COALESCE(ph.ForwardedRecords, 0))  
                / NULLIF(CONVERT(float, hs.RowCount), 0.0),  
                0.0  
            )  
        )  
    )  
) AS metrics(ForwardedPercent)  
WHERE t.is_ms_shipped = 0  
  AND t.is_memory_optimized = 0  
ORDER BY  
    CASE  
        WHEN  
        (  
            COALESCE(ph.ForwardedRecords, 0) >= 100000  
            OR  
            (  
                COALESCE(ph.ForwardedRecords, 0) >= 1000  
                AND metrics.ForwardedPercent >= 5.00  
            )  
        )  
        THEN 0  
        ELSE 1  
    END,  
    COALESCE(ph.ForwardedRecords, 0) DESC,  
    hs.ReservedPageCount DESC,  
    s.name,  
    t.name;  
