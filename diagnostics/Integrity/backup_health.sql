/*******************************************************************************
Script Name: backup_health.sql
Purpose: Reviews the latest full, differential and log backups for online user databases on the SQL Server instance.
Scope: SQL Server instance; reads msdb backup history
SQL Server: 2016+
Azure SQL: Azure SQL support varies for msdb and file operations; see docs/COMPATIBILITY.md
Permissions: VIEW DATABASE STATE or read access to msdb backup history, depending on the script
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

DECLARE @FullBackupWarningHours int = 36;
DECLARE @LogBackupWarningMinutes int = 30;

;WITH b AS
(
    SELECT
        bs.database_name,
        MAX(CASE WHEN bs.type = 'D' THEN bs.backup_finish_date END) AS last_full_backup,
        MAX(CASE WHEN bs.type = 'I' THEN bs.backup_finish_date END) AS last_diff_backup,
        MAX(CASE WHEN bs.type = 'L' THEN bs.backup_finish_date END) AS last_log_backup
    FROM msdb.dbo.backupset AS bs
    WHERE bs.is_copy_only = 0
    GROUP BY bs.database_name
)
SELECT
    CASE
        WHEN b.last_full_backup IS NULL THEN 'High'
        WHEN DATEDIFF(hour, b.last_full_backup, GETDATE()) >= @FullBackupWarningHours THEN 'High'
        WHEN d.recovery_model_desc IN (N'FULL', N'BULK_LOGGED')
             AND (b.last_log_backup IS NULL
                  OR DATEDIFF(minute, b.last_log_backup, GETDATE()) >= @LogBackupWarningMinutes)
             THEN 'High'
        ELSE 'Low'
    END AS [Priority],
    'Backup' AS [Category],
    QUOTENAME(d.name) AS [Object],
    CASE
        WHEN b.last_full_backup IS NULL THEN 'No non-copy-only full backup found in msdb history'
        WHEN DATEDIFF(hour, b.last_full_backup, GETDATE()) >= @FullBackupWarningHours THEN 'Full backup is older than the review threshold'
        WHEN d.recovery_model_desc IN (N'FULL', N'BULK_LOGGED') AND b.last_log_backup IS NULL THEN 'No log backup found in msdb history'
        WHEN d.recovery_model_desc IN (N'FULL', N'BULK_LOGGED')
             AND DATEDIFF(minute, b.last_log_backup, GETDATE()) >= @LogBackupWarningMinutes
             THEN 'Log backup is older than the review threshold'
        ELSE 'Recent backup history found'
    END AS [Finding],
    CONCAT(
        'recovery=', d.recovery_model_desc,
        '; last full=', COALESCE(CONVERT(varchar(19), b.last_full_backup, 120), 'never/unknown'),
        '; last diff=', COALESCE(CONVERT(varchar(19), b.last_diff_backup, 120), 'never/unknown'),
        '; last log=', COALESCE(CONVERT(varchar(19), b.last_log_backup, 120), 'never/unknown')
    ) AS [Evidence],
    'Validate the backup chain, backup destination, restore testing, retention policy, RPO/RTO and whether a third-party product records history in msdb. A successful backup is not proof that restore will succeed.' AS [Recommendation],
    '-- Review backup jobs and perform scheduled restore tests. Do not change recovery model merely to suppress a backup warning.' AS [SuggestedSql],
    'Low: read-only. Operational risk is High if backup history is assumed to prove recoverability without restore testing.' AS [Risk]
FROM sys.databases AS d
LEFT JOIN b
    ON b.database_name = d.name
WHERE d.database_id > 4
  AND d.state_desc = N'ONLINE'
ORDER BY
    CASE
        WHEN b.last_full_backup IS NULL THEN 0
        WHEN DATEDIFF(hour, b.last_full_backup, GETDATE()) >= @FullBackupWarningHours THEN 0
        WHEN d.recovery_model_desc IN (N'FULL', N'BULK_LOGGED')
             AND (b.last_log_backup IS NULL
                  OR DATEDIFF(minute, b.last_log_backup, GETDATE()) >= @LogBackupWarningMinutes)
             THEN 0
        ELSE 1
    END,
    d.name;
