/*******************************************************************************
Script Name: wait_stats.sql
Purpose: Identifies the most significant SQL Server instance wait categories.
Scope: SQL Server instance
SQL Server: 2016+
Azure SQL: Azure SQL support varies for instance-level DMVs; see docs/COMPATIBILITY.md
Permissions: VIEW SERVER STATE or VIEW DATABASE STATE, depending on scope; SQL Server 2022+ may require the corresponding PERFORMANCE STATE permission
Risk: Read-only; review and test any generated SQL before execution.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

DECLARE @MinimumWaitMs bigint = 1000;
DECLARE @Top int = 25;

;WITH waits AS
(
    SELECT
        wait_type,
        wait_time_ms,
        signal_wait_time_ms,
        waiting_tasks_count,
        resource_wait_ms = wait_time_ms - signal_wait_time_ms
    FROM sys.dm_os_wait_stats
    WHERE wait_time_ms >= @MinimumWaitMs
      AND wait_type NOT IN
      (
          N'BROKER_EVENTHANDLER', N'BROKER_RECEIVE_WAITFOR', N'BROKER_TASK_STOP',
          N'BROKER_TO_FLUSH', N'BROKER_TRANSMITTER', N'CHECKPOINT_QUEUE',
          N'CHKPT', N'CLR_AUTO_EVENT', N'CLR_MANUAL_EVENT', N'CLR_SEMAPHORE',
          N'DBMIRROR_DBM_EVENT', N'DBMIRROR_EVENTS_QUEUE', N'DBMIRROR_WORKER_QUEUE',
          N'DBMIRRORING_CMD', N'DIRTY_PAGE_POLL', N'DISPATCHER_QUEUE_SEMAPHORE',
          N'EXECSYNC', N'FSAGENT', N'FT_IFTS_SCHEDULER_IDLE_WAIT', N'FT_IFTSHC_MUTEX',
          N'HADR_CLUSAPI_CALL', N'HADR_FILESTREAM_IOMGR_IOCOMPLETION',
          N'HADR_LOGCAPTURE_WAIT', N'HADR_NOTIFICATION_DEQUEUE',
          N'HADR_TIMER_TASK', N'HADR_WORK_QUEUE', N'KSOURCE_WAKEUP',
          N'LAZYWRITER_SLEEP', N'LOGMGR_QUEUE', N'MEMORY_ALLOCATION_EXT',
          N'ONDEMAND_TASK_QUEUE', N'PARALLEL_REDO_DRAIN_WORKER',
          N'PARALLEL_REDO_LOG_CACHE', N'PARALLEL_REDO_TRAN_LIST',
          N'PARALLEL_REDO_WORKER_SYNC', N'PARALLEL_REDO_WORKER_WAIT_WORK',
          N'PREEMPTIVE_OS_FLUSHFILEBUFFERS', N'PREEMPTIVE_XE_GETTARGETSTATE',
          N'PWAIT_ALL_COMPONENTS_INITIALIZED', N'PWAIT_DIRECTLOGCONSUMER_GETNEXT',
          N'QDS_ASYNC_QUEUE', N'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',
          N'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP', N'REQUEST_FOR_DEADLOCK_SEARCH',
          N'RESOURCE_QUEUE', N'SERVER_IDLE_CHECK', N'SLEEP_BPOOL_FLUSH',
          N'SLEEP_DBSTARTUP', N'SLEEP_DCOMSTARTUP', N'SLEEP_MASTERDBREADY',
          N'SLEEP_MASTERMDREADY', N'SLEEP_MASTERUPGRADED', N'SLEEP_MSDBSTARTUP',
          N'SLEEP_SYSTEMTASK', N'SLEEP_TASK', N'SLEEP_TEMPDBSTARTUP',
          N'SNI_HTTP_ACCEPT', N'SOS_WORK_DISPATCHER', N'SP_SERVER_DIAGNOSTICS_SLEEP',
          N'SQLTRACE_BUFFER_FLUSH', N'SQLTRACE_INCREMENTAL_FLUSH_SLEEP',
          N'SQLTRACE_WAIT_ENTRIES', N'WAIT_FOR_RESULTS', N'WAITFOR',
          N'WAITFOR_TASKSHUTDOWN', N'WAIT_XTP_RECOVERY', N'WAIT_XTP_HOST_WAIT',
          N'WAIT_XTP_OFFLINE_CKPT_NEW_LOG', N'XE_DISPATCHER_JOIN',
          N'XE_DISPATCHER_WAIT', N'XE_LIVE_TARGET_TVF', N'XE_TIMER_EVENT'
      )
),
totals AS
(
    SELECT total_wait_ms = SUM(wait_time_ms * 1.0)
    FROM waits
)
SELECT TOP (@Top)
    CASE
        WHEN w.wait_time_ms / NULLIF(t.total_wait_ms, 0) >= 0.20 THEN 'High'
        WHEN w.wait_time_ms / NULLIF(t.total_wait_ms, 0) >= 0.05 THEN 'Medium'
        ELSE 'Low'
    END AS [Priority],
    'Waits' AS [Category],
    w.wait_type AS [Object],
    'Significant cumulative wait type' AS [Finding],
    CONCAT(
        'wait ms=', w.wait_time_ms,
        '; resource wait ms=', w.resource_wait_ms,
        '; signal wait ms=', w.signal_wait_time_ms,
        '; tasks=', w.waiting_tasks_count,
        '; share %=', CONVERT(decimal(9,2),
            100.0 * w.wait_time_ms / NULLIF(t.total_wait_ms, 0)),
        '; avg wait ms/task=', CONVERT(decimal(18,2),
            w.wait_time_ms * 1.0 / NULLIF(w.waiting_tasks_count, 0))
    ) AS [Evidence],
    'Correlate the wait type with workload, current requests, storage, CPU, memory, locking and recent changes. Compare deltas over a representative interval rather than treating cumulative waits as a point-in-time diagnosis.' AS [Recommendation],
    '-- Capture a baseline, wait for a representative interval, and compare DMV deltas. Do not clear wait stats in production only to make a report easier.' AS [SuggestedSql],
    'Low: read-only. Interpretation risk is high if cumulative waits are treated as proof of a current bottleneck.' AS [Risk]
FROM waits AS w
CROSS JOIN totals AS t
ORDER BY
    w.wait_time_ms DESC,
    w.waiting_tasks_count DESC
OPTION (RECOMPILE);
