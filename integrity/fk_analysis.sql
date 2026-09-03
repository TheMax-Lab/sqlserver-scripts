/*******************************************************************************
Script Name: fk_analysis.sql
Purpose: Identifies unindexed, untrusted, or disabled Foreign Keys. Helps prevent costly child table scans, blocking, and poor query optimizer estimates.
Scope: Current database
SQL Server: 2016+
Azure SQL: Azure SQL Database and Managed Instance; see docs/COMPATIBILITY.md
Permissions: Metadata visibility; orphan scans also require SELECT on participating tables
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

;WITH fk AS (
    SELECT 
        f.object_id,
        f.name,
        f.parent_object_id,
        f.is_disabled,
        f.is_not_trusted,
        f.delete_referential_action_desc,
        COUNT(*) AS n
    FROM sys.foreign_keys f 
    JOIN sys.foreign_key_columns fc 
        ON fc.constraint_object_id = f.object_id 
    GROUP BY 
        f.object_id,
        f.name,
        f.parent_object_id,
        f.is_disabled,
        f.is_not_trusted,
        f.delete_referential_action_desc
),
covered AS (
    SELECT 
        fk.object_id,
        i.name 
    FROM fk 
    JOIN sys.indexes i 
        ON i.object_id = fk.parent_object_id 
       AND i.index_id > 0 
       AND i.is_disabled = 0 
       AND i.has_filter = 0
    WHERE NOT EXISTS (
        SELECT 1 
        FROM sys.foreign_key_columns fc 
        WHERE fc.constraint_object_id = fk.object_id 
          AND NOT EXISTS (
              SELECT 1 
              FROM sys.index_columns ic 
              WHERE ic.object_id = i.object_id 
                AND ic.index_id = i.index_id 
                AND ic.key_ordinal = fc.constraint_column_id 
                AND ic.column_id = fc.parent_column_id
          )
    )
)
SELECT 
    CASE 
        WHEN fk.is_disabled = 1 OR fk.is_not_trusted = 1 THEN 'High' 
        ELSE 'Medium' 
    END AS [Priority],
    'Foreign Key' AS [Category],
    QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + '.' + 
    QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + '.' + 
    QUOTENAME(fk.name) AS [Object],
    CASE 
        WHEN fk.is_disabled = 1 THEN 'Disabled Foreign Key' 
        WHEN fk.is_not_trusted = 1 THEN 'Untrusted Foreign Key' 
        ELSE 'Foreign Key lacking an index with matching leading columns' 
    END AS [Finding],
    CONCAT('delete action=', fk.delete_referential_action_desc) AS [Evidence],
    CASE 
        WHEN fk.is_disabled = 1 OR fk.is_not_trusted = 1 THEN 'Validate existing data integrity first, then re-enable with full check.' 
        ELSE 'Evaluate adding an index on FK columns to optimize parent DELETE/UPDATE operations and frequent JOINs.' 
    END AS [Recommendation],
    CASE 
        WHEN fk.is_disabled = 1 OR fk.is_not_trusted = 1 THEN 
            'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + '.' + 
            QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + 
            ' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(fk.name) + ';'
        ELSE '-- Create index only after analyzing existing indexes and workload' 
    END AS [SuggestedSql],
    'Medium/High: data validation overhead, storage growth, write performance, and locking' AS [Risk]
FROM fk 
LEFT JOIN covered c ON c.object_id = fk.object_id 
WHERE c.object_id IS NULL OR fk.is_disabled = 1 OR fk.is_not_trusted = 1
ORDER BY 
    CASE WHEN fk.is_disabled = 1 OR fk.is_not_trusted = 1 THEN 0 ELSE 1 END;
