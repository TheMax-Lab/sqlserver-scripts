/*******************************************************************************  
Script Name:      database_sizes.sql  
Description:      Reports the current database files, allocated sizes, free  
                  space, transaction log utilization, MAXSIZE, and autogrowth  
                  configuration.  
Author:           TheMaxLab  
Version:          1.0  
License:          MIT  
  
Usage:  
  1. Connect to the target SQL Server instance.  
  2. Select the database context:  
       USE [YourDatabaseName];  
  3. Execute the script in SSMS or Azure Data Studio.  
  
Notes:  
  - Returns one row for each file in the current database.  
  - Data-file free space is calculated with FILEPROPERTY.  
  - Transaction-log utilization is database-wide and is therefore repeated  
    for each log file.  
  - Generated FILEGROWTH values are starting recommendations only and must be  
    adjusted to the workload, storage performance, and recovery objectives.  
*******************************************************************************/  
  
;WITH [RawFiles] AS  
(  
    SELECT  
        df.[file_id],  
        df.[type] AS [file_type],  
        df.[type_desc] AS [file_type_desc],  
        df.[name] AS [logical_name],  
        df.[physical_name],  
        df.[state_desc],  
  
        CONVERT(  
            decimal(19,2),  
            CONVERT(float, df.[size]) * 8.0 / 1024.0  
        ) AS [size_mb],  
  
        CASE  
            WHEN df.[type] = 0  
            THEN CONVERT(  
                    decimal(19,2),  
                    CONVERT(  
                        float,  
                        FILEPROPERTY(df.[name], 'SpaceUsed')  
                    ) * 8.0 / 1024.0  
                 )  
            ELSE NULL  
        END AS [used_mb],  
  
        df.[max_size],  
  
        CASE  
            WHEN df.[max_size] = -1 THEN NULL  
            ELSE CONVERT(  
                    decimal(19,2),  
                    CONVERT(float, df.[max_size]) * 8.0 / 1024.0  
                 )  
        END AS [max_size_mb],  
  
        df.[growth],  
        df.[is_percent_growth],  
  
        CASE  
            WHEN df.[is_percent_growth] = 0  
            THEN CONVERT(  
                    decimal(19,2),  
                    CONVERT(float, df.[growth]) * 8.0 / 1024.0  
                 )  
            ELSE NULL  
        END AS [fixed_growth_mb]  
    FROM sys.database_files AS df  
),  
[FileMetrics] AS  
(  
    SELECT  
        rf.*,  
  
        CASE  
            WHEN rf.[file_type] = 0  
             AND rf.[used_mb] IS NOT NULL  
                THEN CONVERT(  
                        decimal(19,2),  
                        rf.[size_mb] - rf.[used_mb]  
                     )  
            ELSE NULL  
        END AS [free_mb],  
  
        CASE  
            WHEN rf.[file_type] = 0  
             AND rf.[used_mb] IS NOT NULL  
             AND rf.[size_mb] > 0  
                THEN CONVERT(  
                        decimal(9,2),  
                        100.0  
                        * CONVERT(  
                            float,  
                            rf.[size_mb] - rf.[used_mb]  
                        )  
                        / NULLIF(CONVERT(float, rf.[size_mb]), 0)  
                     )  
            ELSE NULL  
        END AS [free_percent]  
    FROM [RawFiles] AS rf  
),  
[DatabaseTotal] AS  
(  
    SELECT  
        CONVERT(  
            decimal(19,2),  
            SUM(CONVERT(float, df.[size])) * 8.0 / 1024.0  
        ) AS [database_allocated_mb]  
    FROM sys.database_files AS df  
),  
[LogSpace] AS  
(  
    SELECT  
        CONVERT(  
            decimal(19,2),  
            CONVERT(float, ls.[total_log_size_in_bytes]) / 1048576.0  
        ) AS [total_log_mb],  
  
        CONVERT(  
            decimal(19,2),  
            CONVERT(float, ls.[used_log_space_in_bytes]) / 1048576.0  
        ) AS [used_log_mb],  
  
        CONVERT(  
            decimal(9,2),  
            ls.[used_log_space_in_percent]  
        ) AS [used_log_percent]  
    FROM sys.dm_db_log_space_usage AS ls  
),  
[AssessedFiles] AS  
(  
    SELECT  
        fm.*,  
        dt.[database_allocated_mb],  
        ls.[total_log_mb],  
        ls.[used_log_mb],  
        ls.[used_log_percent],  
  
        CASE  
            WHEN fm.[file_type] IN (0, 1)  
             AND fm.[growth] = 0  
                THEN 1  
            WHEN fm.[max_size] > 0  
             AND fm.[size_mb] >= fm.[max_size_mb] * 0.90  
                THEN 1  
            WHEN fm.[file_type] = 0  
             AND fm.[free_percent] < 5.0  
                THEN 1  
            WHEN fm.[file_type] = 1  
             AND ls.[used_log_percent] >= 90.0  
                THEN 1  
            WHEN fm.[file_type] = 0  
             AND fm.[free_percent] < 15.0  
                THEN 2  
            WHEN fm.[file_type] = 1  
             AND ls.[used_log_percent] >= 80.0  
                THEN 2  
            WHEN fm.[file_type] IN (0, 1)  
             AND fm.[is_percent_growth] = 1  
                THEN 2  
            WHEN fm.[file_type] IN (0, 1)  
             AND fm.[is_percent_growth] = 0  
             AND fm.[growth] > 0  
             AND fm.[fixed_growth_mb] < 64.0  
                THEN 2  
            ELSE 3  
        END AS [priority_rank],  
  
        CASE  
            WHEN fm.[file_type] IN (0, 1)  
             AND fm.[growth] = 0  
                THEN 'Autogrowth is disabled'  
            WHEN fm.[max_size] > 0  
             AND fm.[size_mb] >= fm.[max_size_mb] * 0.90  
                THEN 'File is close to its configured MAXSIZE'  
            WHEN fm.[file_type] = 0  
             AND fm.[free_percent] < 5.0  
                THEN 'Critically low free space in data file'  
            WHEN fm.[file_type] = 1  
             AND ls.[used_log_percent] >= 90.0  
                THEN 'Critical transaction log utilization'  
            WHEN fm.[file_type] = 0  
             AND fm.[free_percent] < 15.0  
                THEN 'Low free space in data file'  
            WHEN fm.[file_type] = 1  
             AND ls.[used_log_percent] >= 80.0  
                THEN 'High transaction log utilization'  
            WHEN fm.[file_type] IN (0, 1)  
             AND fm.[is_percent_growth] = 1  
                THEN 'Percentage-based autogrowth is configured'  
            WHEN fm.[file_type] IN (0, 1)  
             AND fm.[is_percent_growth] = 0  
             AND fm.[growth] > 0  
             AND fm.[fixed_growth_mb] < 64.0  
                THEN 'Small fixed autogrowth increment'  
            ELSE 'Database file inventory; no configured threshold exceeded'  
        END AS [finding]  
    FROM [FileMetrics] AS fm  
    CROSS JOIN [DatabaseTotal] AS dt  
    CROSS JOIN [LogSpace] AS ls  
)  
SELECT  
    CASE af.[priority_rank]  
        WHEN 1 THEN 'High'  
        WHEN 2 THEN 'Medium'  
        ELSE 'Low'  
    END AS [Priority],  
  
    'Storage' AS [Category],  
  
    CONCAT(  
        QUOTENAME(DB_NAME()), '.',  
        QUOTENAME(af.[logical_name])  
    ) AS [Object],  
  
    af.[finding] AS [Finding],  
  
    CONCAT(  
        'database allocated MB=',  
        af.[database_allocated_mb],  
        '; file type=',  
        af.[file_type_desc],  
        '; state=',  
        af.[state_desc],  
        '; file size MB=',  
        af.[size_mb],  
  
        CASE  
            WHEN af.[file_type] = 0  
             AND af.[used_mb] IS NOT NULL  
            THEN CONCAT(  
                    '; data used MB=',  
                    af.[used_mb],  
                    '; data free MB=',  
                    af.[free_mb],  
                    '; data free=',  
                    af.[free_percent],  
                    '%'  
                 )  
            ELSE ''  
        END,  
  
        CASE  
            WHEN af.[file_type] = 1  
            THEN CONCAT(  
                    '; database log total MB=',  
                    af.[total_log_mb],  
                    '; database log used MB=',  
                    af.[used_log_mb],  
                    '; database log used=',  
                    af.[used_log_percent],  
                    '%'  
                 )  
            ELSE ''  
        END,  
  
        '; max size=',  
        CASE  
            WHEN af.[max_size] = -1  
                THEN 'unlimited'  
            WHEN af.[max_size] = 0  
                THEN 'growth disabled'  
            ELSE CONCAT(af.[max_size_mb], ' MB')  
        END,  
  
        '; growth=',  
        CASE  
            WHEN af.[growth] = 0  
                THEN 'disabled'  
            WHEN af.[is_percent_growth] = 1  
                THEN CONCAT(af.[growth], '%')  
            ELSE CONCAT(af.[fixed_growth_mb], ' MB')  
        END,  
  
        '; physical=',  
        af.[physical_name]  
    ) AS [Evidence],  
  
    CASE  
        WHEN af.[file_type] IN (0, 1)  
         AND af.[growth] = 0  
            THEN 'Verify available storage and configure a fixed autogrowth increment if the file is expected to grow. Pre-size the file whenever possible.'  
        WHEN af.[max_size] > 0  
         AND af.[size_mb] >= af.[max_size_mb] * 0.90  
            THEN 'Review the configured MAXSIZE, available storage, and expected growth. Increase the limit or provision additional storage before the limit is reached.'  
        WHEN af.[file_type] = 0  
         AND af.[free_percent] < 15.0  
            THEN 'Review expected data growth and available storage. Pre-size the data file during a controlled maintenance window instead of relying on repeated autogrowth.'  
        WHEN af.[file_type] = 1  
         AND af.[used_log_percent] >= 80.0  
            THEN 'Check long-running transactions, log reuse wait, recovery model, log backup frequency, availability features, and storage capacity. Do not shrink the log as routine maintenance.'  
        WHEN af.[file_type] IN (0, 1)  
         AND af.[is_percent_growth] = 1  
            THEN 'Replace percentage-based autogrowth with a workload-appropriate fixed increment to obtain more predictable growth duration and file sizes.'  
        WHEN af.[file_type] IN (0, 1)  
         AND af.[is_percent_growth] = 0  
         AND af.[growth] > 0  
         AND af.[fixed_growth_mb] < 64.0  
            THEN 'Increase the fixed autogrowth increment after validating storage throughput and expected workload growth.'  
        ELSE 'Monitor file size, free space, transaction log utilization, autogrowth events, and underlying storage capacity.'  
    END AS [Recommendation],  
  
    CASE  
        WHEN af.[file_type] IN (0, 1)  
         AND  
         (  
             af.[growth] = 0  
             OR af.[is_percent_growth] = 1  
             OR  
             (  
                 af.[is_percent_growth] = 0  
                 AND af.[growth] > 0  
                 AND af.[fixed_growth_mb] < 64.0  
             )  
         )  
        THEN CONCAT(  
                'ALTER DATABASE ',  
                QUOTENAME(DB_NAME()),  
                ' MODIFY FILE (NAME = N''',  
                REPLACE(af.[logical_name], '''', ''''''),  
                ''', FILEGROWTH = ',  
                CASE  
                    WHEN af.[file_type] = 1 THEN '256MB'  
                    ELSE '512MB'  
                END,  
                ');'  
             )  
        WHEN af.[max_size] > 0  
         AND af.[size_mb] >= af.[max_size_mb] * 0.90  
            THEN '-- Review storage capacity before changing MAXSIZE'  
        WHEN af.[file_type] = 0  
         AND af.[free_percent] < 15.0  
            THEN '-- Pre-size the data file after capacity planning'  
        WHEN af.[file_type] = 1  
         AND af.[used_log_percent] >= 80.0  
            THEN '-- Investigate log reuse and active transactions before resizing'  
        ELSE '-- No immediate size change suggested'  
    END AS [SuggestedSql],  
  
    CASE af.[priority_rank]  
        WHEN 1  
            THEN 'High: insufficient file or log space can stop write activity; file growth can pause workloads and consume substantial storage.'  
        WHEN 2  
            THEN 'Medium: changing file-growth settings affects storage consumption and growth latency; validate increment sizes against workload and storage performance.'  
        ELSE 'Low: informational inventory; continue monitoring size, utilization, growth events, and storage capacity.'  
    END AS [Risk]  
  
FROM [AssessedFiles] AS af  
ORDER BY  
    af.[priority_rank],  
    af.[file_type],  
    af.[file_id];  
