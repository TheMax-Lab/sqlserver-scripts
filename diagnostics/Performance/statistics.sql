/*******************************************************************************
Script Name: statistics.sql
Purpose: Searches the current database for statistics that have never been initialized or whose modification counter exceeds a practical auto-update threshold.
Scope: Current database
SQL Server: 2012 SP1+
Azure SQL: Azure SQL support varies for msdb and file operations; see docs/COMPATIBILITY.md
Permissions: VIEW DATABASE STATE or read access to msdb backup history, depending on the script
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/  
  
;WITH [ObjectRows] AS  
(  
    SELECT  
        p.[object_id],  
        SUM(CONVERT(bigint, p.[rows])) AS [object_rows]  
    FROM sys.partitions AS p  
    WHERE p.[index_id] IN (0, 1)  
    GROUP BY  
        p.[object_id]  
),  
[StatisticsBase] AS  
(  
    SELECT  
        sc.[name] AS [schema_name],  
        o.[name] AS [object_name],  
        st.[name] AS [statistics_name],  
        st.[stats_id],  
        st.[auto_created],  
        st.[user_created],  
        st.[no_recompute],  
        st.[has_filter],  
        st.[filter_definition],  
        sp.[last_updated],  
        sp.[rows] AS [statistics_rows],  
        sp.[rows_sampled],  
        COALESCE(sp.[modification_counter], 0) AS [modification_counter],  
        COALESCE(sp.[rows], obr.[object_rows], 0) AS [effective_rows],  
        d.[is_auto_update_stats_on]  
    FROM sys.stats AS st  
    INNER JOIN sys.objects AS o  
        ON o.[object_id] = st.[object_id]  
    INNER JOIN sys.schemas AS sc  
        ON sc.[schema_id] = o.[schema_id]  
    LEFT JOIN [ObjectRows] AS obr  
        ON obr.[object_id] = o.[object_id]  
    INNER JOIN sys.databases AS d  
        ON d.[database_id] = DB_ID()  
    OUTER APPLY sys.dm_db_stats_properties  
    (  
        st.[object_id],  
        st.[stats_id]  
    ) AS sp  
    WHERE  
        o.[is_ms_shipped] = 0  
        AND o.[type] IN ('U', 'V')  
),  
[StatisticsAssessment] AS  
(  
    SELECT  
        sb.*,  
        CASE  
            WHEN sb.[effective_rows] <= 500  
                THEN 500.0  
            WHEN sb.[effective_rows] <= 25000  
                THEN 500.0  
                   + (0.20 * CONVERT(float, sb.[effective_rows]))  
            ELSE SQRT(1000.0 * CONVERT(float, sb.[effective_rows]))  
        END AS [modification_threshold]  
    FROM [StatisticsBase] AS sb  
)  
SELECT  
    CASE  
        WHEN sa.[last_updated] IS NULL  
          OR sa.[is_auto_update_stats_on] = 0  
          OR sa.[no_recompute] = 1  
          OR CONVERT(float, sa.[modification_counter])  
                >= sa.[modification_threshold] * 2.0  
            THEN 'High'  
        ELSE 'Medium'  
    END AS [Priority],  
  
    'Statistics' AS [Category],  
  
    CONCAT(  
        QUOTENAME(sa.[schema_name]), '.',  
        QUOTENAME(sa.[object_name]), '.',  
        QUOTENAME(sa.[statistics_name])  
    ) AS [Object],  
  
    CASE  
        WHEN sa.[last_updated] IS NULL  
            THEN 'Statistic has no initialized statistics blob'  
        WHEN sa.[is_auto_update_stats_on] = 0  
            THEN 'Modification threshold exceeded while AUTO_UPDATE_STATISTICS is disabled'  
        WHEN sa.[no_recompute] = 1  
            THEN 'Modification threshold exceeded for a NORECOMPUTE statistic'  
        ELSE 'Statistics modification threshold exceeded'  
    END AS [Finding],  
  
    CONCAT(  
        'last updated=',  
        COALESCE(  
            CONVERT(varchar(19), sa.[last_updated], 120),  
            'never'  
        ),  
        '; rows=',  
        sa.[effective_rows],  
        '; modifications=',  
        sa.[modification_counter],  
        '; threshold=',  
        CONVERT(  
            bigint,  
            CEILING(sa.[modification_threshold])  
        ),  
        '; changed=',  
        CASE  
            WHEN sa.[effective_rows] > 0  
            THEN CONVERT(  
                    varchar(30),  
                    CONVERT(  
                        decimal(9,2),  
                        100.0  
                        * CONVERT(float, sa.[modification_counter])  
                        / NULLIF(  
                            CONVERT(float, sa.[effective_rows]),  
                            0  
                        )  
                    )  
                 )  
            ELSE 'n/a'  
        END,  
        '%; sampled=',  
        COALESCE(  
            CONVERT(varchar(30), sa.[rows_sampled]),  
            'n/a'  
        ),  
        CASE  
            WHEN sa.[statistics_rows] > 0  
             AND sa.[rows_sampled] IS NOT NULL  
            THEN CONCAT(  
                    ' (',  
                    CONVERT(  
                        varchar(30),  
                        CONVERT(  
                            decimal(9,2),  
                            100.0  
                            * CONVERT(float, sa.[rows_sampled])  
                            / NULLIF(  
                                CONVERT(float, sa.[statistics_rows]),  
                                0  
                            )  
                        )  
                    ),  
                    '%)'  
                 )  
            ELSE ''  
        END,  
        '; source=',  
        CASE  
            WHEN sa.[auto_created] = 1 THEN 'auto-created'  
            WHEN sa.[user_created] = 1 THEN 'user-created'  
            ELSE 'index statistic'  
        END,  
        '; auto update=',  
        CASE sa.[is_auto_update_stats_on]  
            WHEN 1 THEN 'enabled'  
            ELSE 'disabled'  
        END,  
        '; no recompute=',  
        CASE sa.[no_recompute]  
            WHEN 1 THEN 'yes'  
            ELSE 'no'  
        END,  
        CASE  
            WHEN sa.[has_filter] = 1  
                THEN CONCAT(  
                    '; filter=',  
                    LEFT(sa.[filter_definition], 500)  
                )  
            ELSE ''  
        END  
    ) AS [Evidence],  
  
    CASE  
        WHEN sa.[is_auto_update_stats_on] = 0  
            THEN 'Update this statistic after validating the maintenance window, then review whether AUTO_UPDATE_STATISTICS should be enabled for the database.'  
        WHEN sa.[no_recompute] = 1  
            THEN 'Update this statistic and verify whether NORECOMPUTE is intentional. Define an explicit statistics maintenance schedule if it must remain enabled.'  
        WHEN sa.[last_updated] IS NULL  
            THEN 'Initialize the statistic before evaluating query estimates and execution plans.'  
        ELSE 'Update the statistic after validating workload impact. Use FULLSCAN only when a representative sample is insufficient and the additional I/O is acceptable.'  
    END AS [Recommendation],  
  
    CONCAT(  
        'UPDATE STATISTICS ',  
        QUOTENAME(sa.[schema_name]), '.',  
        QUOTENAME(sa.[object_name]), ' ',  
        QUOTENAME(sa.[statistics_name]),  
        ';'  
    ) AS [SuggestedSql],  
  
    CASE  
        WHEN sa.[effective_rows] >= 10000000  
            THEN 'High: updating statistics on a large object can consume significant CPU and I/O and can trigger plan recompilation or plan changes.'  
        ELSE 'Medium: updating statistics consumes CPU and I/O and may trigger recompilation or execution-plan changes.'  
    END AS [Risk]  
  
FROM [StatisticsAssessment] AS sa  
WHERE  
    sa.[effective_rows] > 0  
    AND  
    (  
        sa.[last_updated] IS NULL  
        OR CONVERT(float, sa.[modification_counter])  
            >= sa.[modification_threshold]  
    )  
ORDER BY  
    CASE  
        WHEN sa.[last_updated] IS NULL  
          OR sa.[is_auto_update_stats_on] = 0  
          OR sa.[no_recompute] = 1  
          OR CONVERT(float, sa.[modification_counter])  
                >= sa.[modification_threshold] * 2.0  
            THEN 1  
        ELSE 2  
    END,  
    CONVERT(float, sa.[modification_counter])  
        / NULLIF(CONVERT(float, sa.[effective_rows]), 0) DESC,  
    sa.[modification_counter] DESC;  
