/*******************************************************************************
Script Name: file_space.sql
Purpose: Reviews current-database data/log file size, used/free space and growth settings.
Scope: Current database
SQL Server: 2016+
Azure SQL: Azure SQL support varies for msdb and file operations; see docs/COMPATIBILITY.md
Permissions: VIEW DATABASE STATE or read access to msdb backup history, depending on the script
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

DECLARE @FreeSpaceWarningPct decimal(9,2) = 10.0;
DECLARE @SmallFixedGrowthMB decimal(18,2) = 64.0;

;WITH f AS
(
    SELECT
        df.file_id,
        df.type_desc,
        df.name,
        df.physical_name,
        df.size,
        df.max_size,
        df.growth,
        df.is_percent_growth,
        used_pages =
            CASE WHEN df.type_desc = N'ROWS'
                 THEN FILEPROPERTY(df.name, 'SpaceUsed')
                 ELSE NULL END
    FROM sys.database_files AS df
)
SELECT
    CASE
        WHEN type_desc = N'ROWS'
             AND size > 0
             AND (size - COALESCE(used_pages, 0)) * 100.0 / size < @FreeSpaceWarningPct
             THEN 'High'
        WHEN is_percent_growth = 1 THEN 'Medium'
        WHEN is_percent_growth = 0 AND growth > 0 AND growth / 128.0 < @SmallFixedGrowthMB
             THEN 'Medium'
        ELSE 'Low'
    END AS [Priority],
    'File Capacity' AS [Category],
    CONCAT(QUOTENAME(DB_NAME()), '.', QUOTENAME(name)) AS [Object],
    CASE
        WHEN type_desc = N'ROWS'
             AND size > 0
             AND (size - COALESCE(used_pages, 0)) * 100.0 / size < @FreeSpaceWarningPct
             THEN 'Data file has low free space inside the file'
        WHEN is_percent_growth = 1 THEN 'File uses percentage autogrowth'
        WHEN is_percent_growth = 0 AND growth > 0 AND growth / 128.0 < @SmallFixedGrowthMB
             THEN 'File uses a small fixed autogrowth increment'
        ELSE 'File capacity review'
    END AS [Finding],
    CONCAT(
        'type=', type_desc,
        '; size MB=', CONVERT(decimal(18,2), size / 128.0),
        '; used MB=', COALESCE(CONVERT(varchar(30), CONVERT(decimal(18,2), used_pages / 128.0)), 'n/a'),
        '; free MB=', COALESCE(CONVERT(varchar(30), CONVERT(decimal(18,2), (size - used_pages) / 128.0)), 'n/a'),
        '; growth=',
            CASE WHEN is_percent_growth = 1
                 THEN CONCAT(growth, '%')
                 ELSE CONCAT(CONVERT(decimal(18,2), growth / 128.0), ' MB') END,
        '; max size=',
            CASE WHEN max_size = -1 THEN 'unlimited'
                 ELSE CONCAT(CONVERT(decimal(18,2), max_size / 128.0), ' MB') END,
        '; path=', physical_name
    ) AS [Evidence],
    'Plan file size and autogrowth according to workload and storage capacity. Pre-size files when growth is predictable. Avoid routine shrink/regrow cycles.' AS [Recommendation],
    '-- Review ALTER DATABASE ... MODIFY FILE only after validating storage capacity, growth rate and operational requirements.' AS [SuggestedSql],
    'Medium: file-size and growth changes can affect storage consumption and I/O behavior.' AS [Risk]
FROM f
ORDER BY
    CASE
        WHEN type_desc = N'ROWS'
             AND size > 0
             AND (size - COALESCE(used_pages, 0)) * 100.0 / size < @FreeSpaceWarningPct
             THEN 0
        ELSE 1
    END,
    type_desc,
    file_id
OPTION (RECOMPILE);
