/*******************************************************************************
Script Name: memory_pressure.sql
Purpose: Provides a compact SQL Server/OS memory pressure snapshot.
Scope: SQL Server instance
SQL Server: 2016+
Azure SQL: Azure SQL support varies for instance-level DMVs; see docs/COMPATIBILITY.md
Permissions: VIEW SERVER STATE or VIEW DATABASE STATE, depending on scope; SQL Server 2022+ may require the corresponding PERFORMANCE STATE permission
Risk: Read-only; review and test any generated SQL before execution.
Output: Memory summary and top memory clerks
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/

SELECT
    CASE
        WHEN pm.process_physical_memory_low = 1 OR pm.process_virtual_memory_low = 1 THEN 'High'
        WHEN sm.available_physical_memory_kb < 1048576 THEN 'Medium'
        ELSE 'Low'
    END AS [Priority],
    'Memory' AS [Category],
    'SQL Server process / operating system' AS [Object],
    CASE
        WHEN pm.process_physical_memory_low = 1 THEN 'SQL Server reports low physical memory'
        WHEN pm.process_virtual_memory_low = 1 THEN 'SQL Server reports low virtual memory'
        WHEN sm.available_physical_memory_kb < 1048576 THEN 'Low available physical memory'
        ELSE 'No immediate low-memory flag detected'
    END AS [Finding],
    CONCAT(
        'SQL physical MB=', CONVERT(decimal(18,1), pm.physical_memory_in_use_kb / 1024.0),
        '; SQL locked pages MB=', CONVERT(decimal(18,1), pm.locked_page_allocations_kb / 1024.0),
        '; SQL virtual committed MB=', CONVERT(decimal(18,1), pm.virtual_address_space_committed_kb / 1024.0),
        '; OS total MB=', CONVERT(decimal(18,1), sm.total_physical_memory_kb / 1024.0),
        '; OS available MB=', CONVERT(decimal(18,1), sm.available_physical_memory_kb / 1024.0),
        '; OS memory state=', sm.system_memory_state_desc
    ) AS [Evidence],
    'Correlate this snapshot with max server memory, memory grants, cache composition, OS workload, paging, workload concurrency and sustained trends. A single snapshot is not enough to diagnose memory pressure.' AS [Recommendation],
    '-- Review active grants with performance/memory_grants.sql and validate max server memory against total host responsibilities.' AS [SuggestedSql],
    'Low: read-only. Configuration changes to SQL Server memory can cause severe regressions if made without workload evidence.' AS [Risk]
FROM sys.dm_os_process_memory AS pm
CROSS JOIN sys.dm_os_sys_memory AS sm;

SELECT TOP (20)
    type AS [MemoryClerk],
    SUM(pages_kb) / 1024.0 AS [PagesMB],
    SUM(virtual_memory_committed_kb) / 1024.0 AS [VirtualCommittedMB],
    SUM(awe_allocated_kb) / 1024.0 AS [AweAllocatedMB]
FROM sys.dm_os_memory_clerks
GROUP BY type
ORDER BY SUM(pages_kb) DESC;
