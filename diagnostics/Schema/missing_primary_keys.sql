/*******************************************************************************
Script Name: missing_primary_keys.sql
Purpose: Finds user tables without a primary key and reports row count, storage type, unique indexes, and incoming foreign keys. Temporal history tables are excluded because they normally cannot have a primary key.
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
  
;WITH TableRows AS  
(  
    SELECT  
        p.object_id,  
        SUM(CONVERT(bigint, p.rows)) AS [RowCount]  
    FROM sys.partitions AS p  
    WHERE p.index_id IN (0, 1)  
    GROUP BY  
        p.object_id  
),  
IndexSummary AS  
(  
    SELECT  
        i.object_id,  
        SUM  
        (  
            CASE  
                WHEN i.is_unique = 1  
                 AND i.index_id > 0  
                 AND i.is_disabled = 0  
                THEN 1  
                ELSE 0  
            END  
        ) AS UniqueIndexCount,  
        SUM  
        (  
            CASE  
                WHEN i.is_unique = 1  
                 AND i.has_filter = 0  
                 AND i.index_id > 0  
                 AND i.is_disabled = 0  
                THEN 1  
                ELSE 0  
            END  
        ) AS UnfilteredUniqueIndexCount,  
        MAX  
        (  
            CASE  
                WHEN i.index_id = 0 THEN 1  
                ELSE 0  
            END  
        ) AS IsHeap,  
        MAX  
        (  
            CASE  
                WHEN i.type = 1 THEN 1  
                ELSE 0  
            END  
        ) AS HasClusteredIndex  
    FROM sys.indexes AS i  
    WHERE i.is_hypothetical = 0  
    GROUP BY  
        i.object_id  
),  
IncomingForeignKeys AS  
(  
    SELECT  
        fk.referenced_object_id AS object_id,  
        COUNT(*) AS IncomingForeignKeyCount  
    FROM sys.foreign_keys AS fk  
    WHERE fk.is_ms_shipped = 0  
    GROUP BY  
        fk.referenced_object_id  
)  
SELECT  
    CASE  
        WHEN COALESCE(tr.[RowCount], 0) > 0  
         AND COALESCE(ix.UnfilteredUniqueIndexCount, 0) = 0  
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
        WHEN COALESCE(ix.UnfilteredUniqueIndexCount, 0) > 0 THEN  
            'Table has no primary key; an unfiltered unique index may be a candidate'  
        ELSE  
            'Table has no primary key or obvious unfiltered unique candidate'  
    END AS [Finding],  
    CONCAT  
    (  
        'rows=', COALESCE(tr.[RowCount], 0),  
        '; storage=',  
        CASE  
            WHEN COALESCE(ix.IsHeap, 0) = 1 THEN 'HEAP'  
            WHEN COALESCE(ix.HasClusteredIndex, 0) = 1 THEN 'CLUSTERED INDEX'  
            WHEN t.is_memory_optimized = 1 THEN 'MEMORY OPTIMIZED'  
            ELSE 'NONCLUSTERED OR SPECIALIZED'  
        END,  
        '; unique indexes=', COALESCE(ix.UniqueIndexCount, 0),  
        '; unfiltered unique indexes=',  
        COALESCE(ix.UnfilteredUniqueIndexCount, 0),  
        '; incoming foreign keys=',  
        COALESCE(ifk.IncomingForeignKeyCount, 0)  
    ) AS [Evidence],  
    'Identify a narrow, stable, unique, and NOT NULL business or surrogate key. Check for duplicates and NULL values, evaluate clustered versus nonclustered placement, and document intentional exceptions such as staging or transient tables.' AS [Recommendation],  
    CONCAT  
    (  
        '-- Replace <key_column> after profiling uniqueness, NULL values, and application dependencies.',  
        CHAR(13),  
        CHAR(10),  
        'ALTER TABLE ',  
        QUOTENAME(s.name),  
        '.',  
        QUOTENAME(t.name),  
        ' ADD CONSTRAINT ',  
        QUOTENAME(LEFT(N'PK_' + t.name, 128)),  
        ' PRIMARY KEY (<key_column>);'  
    ) AS [SuggestedSql],  
    'High: choosing an incorrect key can break application behavior. Creating a primary key can fail on duplicates or NULLs, consume log and storage, block activity, and alter physical access paths if created as clustered.' AS [Risk]  
FROM sys.tables AS t  
INNER JOIN sys.schemas AS s  
    ON s.schema_id = t.schema_id  
LEFT JOIN TableRows AS tr  
    ON tr.object_id = t.object_id  
LEFT JOIN IndexSummary AS ix  
    ON ix.object_id = t.object_id  
LEFT JOIN IncomingForeignKeys AS ifk  
    ON ifk.object_id = t.object_id  
WHERE t.is_ms_shipped = 0  
  AND t.temporal_type <> 1  
  AND NOT EXISTS  
  (  
      SELECT 1  
      FROM sys.key_constraints AS kc  
      WHERE kc.parent_object_id = t.object_id  
        AND kc.type = 'PK'  
  )  
ORDER BY  
    CASE  
        WHEN COALESCE(tr.[RowCount], 0) > 0  
         AND COALESCE(ix.UnfilteredUniqueIndexCount, 0) = 0  
        THEN 0  
        ELSE 1  
    END,  
    COALESCE(tr.[RowCount], 0) DESC,  
    s.name,  
    t.name;  
