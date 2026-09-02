/*******************************************************************************  
Script Name:      database_health.sql  
Description:      Checks the current database for configuration, backup, and  
                  transaction log health conditions.  
Author:           TheMaxLab  
Version:          1.0  
License:          MIT  
  
Usage:  
  1. Connect to the target SQL Server instance.  
  2. Select the database context:  
       USE [YourDatabaseName];  
  3. Execute the script in SSMS or Azure Data Studio.  
  
Notes:  
  - Backup findings are based on backup history available in msdb.  
  - No returned rows means that no condition exceeded the configured thresholds.  
*******************************************************************************/  
  
;WITH DbInfo AS  
(  
    SELECT  
        d.database_id,  
        d.name,  
        d.create_date,  
        d.state_desc,  
        d.user_access_desc,  
        d.recovery_model_desc,  
        d.page_verify_option_desc,  
        d.log_reuse_wait_desc,  
        d.is_auto_close_on,  
        d.is_auto_shrink_on  
    FROM sys.databases AS d  
    WHERE d.database_id = DB_ID()  
),  
LogInfo AS  
(  
    SELECT  
        CONVERT(bigint, total_log_size_in_bytes) AS total_log_size_in_bytes,  
        CONVERT(bigint, used_log_space_in_bytes) AS used_log_space_in_bytes,  
        CONVERT(decimal(9,2), used_log_space_in_percent)  
            AS used_log_space_in_percent  
    FROM sys.dm_db_log_space_usage  
),  
BackupInfo AS  
(  
    SELECT  
        MAX(CASE  
                WHEN bs.[type] = 'D'  
                THEN bs.backup_finish_date  
            END) AS last_full_backup,  
        MAX(CASE  
                WHEN bs.[type] = 'L'  
                THEN bs.backup_finish_date  
            END) AS last_log_backup  
    FROM DbInfo AS d  
    LEFT JOIN msdb.dbo.backupset AS bs  
        ON bs.database_name = d.name  
       AND bs.backup_finish_date IS NOT NULL  
       AND bs.backup_finish_date >= d.create_date  
),  
Diagnostics AS  
(  
    SELECT  
        'High' AS [Priority],  
        'Database' AS [Category],  
        d.name AS [Object],  
        CONCAT('Database state is ', d.state_desc) AS [Finding],  
        CONCAT(  
            'database=', d.name,  
            '; state=', d.state_desc,  
            '; user access=', d.user_access_desc,  
            '; recovery model=', d.recovery_model_desc  
        ) AS [Evidence],  
        'Investigate SQL Server error log, storage, recovery, availability group, and database status before changing the state.'  
            AS [Recommendation],  
        '-- Do not force a database state change before identifying the root cause'  
            AS [SuggestedSql],  
        'High: forcing ONLINE, EMERGENCY, or other states can cause data loss or interrupt recovery'  
            AS [Risk]  
    FROM DbInfo AS d  
    WHERE d.state_desc <> N'ONLINE'  
  
    UNION ALL  
  
    SELECT  
        'Medium',  
        'Database',  
        d.name,  
        CONCAT('Database user access is ', d.user_access_desc),  
        CONCAT(  
            'database=', d.name,  
            '; user access=', d.user_access_desc,  
            '; state=', d.state_desc  
        ),  
        'Verify whether SINGLE_USER or RESTRICTED_USER is intentional and identify the sessions that depend on this setting.',  
        CONCAT(  
            '-- If approved, review before executing: ALTER DATABASE ',  
            QUOTENAME(d.name),  
            ' SET MULTI_USER;'  
        ),  
        'High: changing user access can disconnect users and interfere with maintenance or recovery'  
    FROM DbInfo AS d  
    WHERE d.user_access_desc <> N'MULTI_USER'  
  
    UNION ALL  
  
    SELECT  
        'High',  
        'Configuration',  
        d.name,  
        CONCAT('PAGE_VERIFY is configured as ', d.page_verify_option_desc),  
        CONCAT(  
            'database=', d.name,  
            '; PAGE_VERIFY=', d.page_verify_option_desc  
        ),  
        'Use PAGE_VERIFY CHECKSUM and schedule DBCC CHECKDB. The setting protects newly written pages but does not validate existing pages.',  
        CONCAT(  
            'ALTER DATABASE ',  
            QUOTENAME(d.name),  
            ' SET PAGE_VERIFY CHECKSUM;'  
        ),  
        'Low: small metadata change; CHECKSUM adds minor write CPU overhead and does not repair existing corruption'  
    FROM DbInfo AS d  
    WHERE d.page_verify_option_desc <> N'CHECKSUM'  
  
    UNION ALL  
  
    SELECT  
        'High',  
        'Configuration',  
        d.name,  
        'AUTO_CLOSE is enabled',  
        CONCAT(  
            'database=', d.name,  
            '; AUTO_CLOSE=ON'  
        ),  
        'Disable AUTO_CLOSE to avoid repeated database startup, cache eviction, and avoidable connection latency.',  
        CONCAT(  
            'ALTER DATABASE ',  
            QUOTENAME(d.name),  
            ' SET AUTO_CLOSE OFF;'  
        ),  
        'Low: disabling AUTO_CLOSE keeps the database open and preserves cached plans and data pages'  
    FROM DbInfo AS d  
    WHERE d.is_auto_close_on = 1  
  
    UNION ALL  
  
    SELECT  
        'High',  
        'Configuration',  
        d.name,  
        'AUTO_SHRINK is enabled',  
        CONCAT(  
            'database=', d.name,  
            '; AUTO_SHRINK=ON'  
        ),  
        'Disable AUTO_SHRINK. Size data and log files deliberately and investigate unexpected file growth.',  
        CONCAT(  
            'ALTER DATABASE ',  
            QUOTENAME(d.name),  
            ' SET AUTO_SHRINK OFF;'  
        ),  
        'Low: disabling AUTO_SHRINK prevents automatic shrink operations; verify storage capacity separately'  
    FROM DbInfo AS d  
    WHERE d.is_auto_shrink_on = 1  
  
    UNION ALL  
  
    SELECT  
        CASE  
            WHEN b.last_full_backup IS NULL  
              OR b.last_full_backup < DATEADD(DAY, -14, GETDATE())  
                THEN 'High'  
            ELSE 'Medium'  
        END,  
        'Backup',  
        d.name,  
        'No recent full backup was found in msdb',  
        CONCAT(  
            'database=', d.name,  
            '; recovery model=', d.recovery_model_desc,  
            '; last full backup=',  
            COALESCE(  
                CONVERT(varchar(19), b.last_full_backup, 120),  
                '<not found>'  
            ),  
            '; threshold=7 days'  
        ),  
        'Verify the backup platform and msdb retention, then create and restore-test a CHECKSUM backup according to the required RPO and RTO.',  
        CONCAT(  
            '-- Example only: BACKUP DATABASE ',  
            QUOTENAME(d.name),  
            ' TO DISK = ''<validated_path>'' WITH CHECKSUM;'  
        ),  
        'High: an unverified backup may be unusable; validate paths, encryption, retention, and restore procedures'  
    FROM DbInfo AS d  
    CROSS JOIN BackupInfo AS b  
    WHERE d.name <> N'tempdb'  
      AND  
      (  
          b.last_full_backup IS NULL  
          OR b.last_full_backup < DATEADD(DAY, -7, GETDATE())  
      )  
  
    UNION ALL  
  
    SELECT  
        CASE  
            WHEN b.last_log_backup IS NULL  
              OR b.last_log_backup < DATEADD(HOUR, -4, GETDATE())  
                THEN 'High'  
            ELSE 'Medium'  
        END,  
        'Backup',  
        d.name,  
        'Transaction log backup is missing or older than one hour',  
        CONCAT(  
            'database=', d.name,  
            '; recovery model=', d.recovery_model_desc,  
            '; last log backup=',  
            COALESCE(  
                CONVERT(varchar(19), b.last_log_backup, 120),  
                '<not found>'  
            ),  
            '; threshold=1 hour'  
        ),  
        'Verify that a full backup has initialized the log chain and that scheduled log backups meet the required RPO.',  
        CONCAT(  
            '-- Example only: BACKUP LOG ',  
            QUOTENAME(d.name),  
            ' TO DISK = ''<validated_path>'' WITH CHECKSUM;'  
        ),  
        'High: starting or changing a backup strategy without validating the log chain can compromise point-in-time recovery'  
    FROM DbInfo AS d  
    CROSS JOIN BackupInfo AS b  
    WHERE d.name <> N'tempdb'  
      AND d.recovery_model_desc IN (N'FULL', N'BULK_LOGGED')  
      AND  
      (  
          b.last_log_backup IS NULL  
          OR b.last_log_backup < DATEADD(HOUR, -1, GETDATE())  
      )  
  
    UNION ALL  
  
    SELECT  
        CASE  
            WHEN l.used_log_space_in_percent >= 85  
                THEN 'High'  
            ELSE 'Medium'  
        END,  
        'Transaction Log',  
        CONCAT(d.name, ' transaction log'),  
        'Transaction log space utilization is elevated',  
        CONCAT(  
            'database=', d.name,  
            '; log size MB=',  
            CONVERT(decimal(18,2), l.total_log_size_in_bytes / 1048576.0),  
            '; used MB=',  
            CONVERT(decimal(18,2), l.used_log_space_in_bytes / 1048576.0),  
            '; used percent=', l.used_log_space_in_percent,  
            '; reuse wait=', d.log_reuse_wait_desc  
        ),  
        'Identify the log reuse wait, verify log backups, active transactions, replication, availability replicas, and available disk space.',  
        '-- Investigate the log reuse wait before growing or shrinking the transaction log',  
        'High: an exhausted transaction log can stop write activity; routine shrink operations can cause repeated growth and fragmentation'  
    FROM DbInfo AS d  
    CROSS JOIN LogInfo AS l  
    WHERE l.used_log_space_in_percent >= 70  
  
    UNION ALL  
  
    SELECT  
        CASE  
            WHEN d.log_reuse_wait_desc IN  
                 (  
                     N'ACTIVE_TRANSACTION',  
                     N'LOG_BACKUP',  
                     N'REPLICATION',  
                     N'AVAILABILITY_REPLICA'  
                 )  
                THEN 'High'  
            ELSE 'Medium'  
        END,  
        'Transaction Log',  
        CONCAT(d.name, ' transaction log'),  
        CONCAT('Log reuse is waiting on ', d.log_reuse_wait_desc),  
        CONCAT(  
            'database=', d.name,  
            '; reuse wait=', d.log_reuse_wait_desc,  
            '; used percent=', l.used_log_space_in_percent,  
            '; recovery model=', d.recovery_model_desc  
        ),  
        CASE d.log_reuse_wait_desc  
            WHEN N'ACTIVE_TRANSACTION'  
                THEN 'Run open_transactions.sql and identify the oldest transaction retaining the log.'  
            WHEN N'LOG_BACKUP'  
                THEN 'Verify the transaction log backup job, backup destination, and log chain.'  
            WHEN N'REPLICATION'  
                THEN 'Check replication log reader latency and replication health.'  
            WHEN N'AVAILABILITY_REPLICA'  
                THEN 'Check availability replica connectivity, send queues, redo queues, and synchronization state.'  
            ELSE  
                'Investigate the documented cause of the current log reuse wait before changing file size.'  
        END,  
        '-- Resolve the log reuse wait; do not use SHRINKFILE as the primary corrective action',  
        'High: incorrect intervention can break recovery objectives, interrupt HA/DR, or cause long rollbacks'  
    FROM DbInfo AS d  
    CROSS JOIN LogInfo AS l  
    WHERE d.log_reuse_wait_desc <> N'NOTHING'  
      AND l.used_log_space_in_percent >= 50  
)  
SELECT  
    [Priority],  
    [Category],  
    [Object],  
    [Finding],  
    [Evidence],  
    [Recommendation],  
    [SuggestedSql],  
    [Risk]  
FROM Diagnostics  
ORDER BY  
    CASE [Priority]  
        WHEN 'High' THEN 1  
        WHEN 'Medium' THEN 2  
        ELSE 3  
    END,  
    [Category],  
    [Object];  
