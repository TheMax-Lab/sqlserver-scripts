/*******************************************************************************
Script Name: untrusted_constraints.sql
Purpose: Finds disabled or untrusted FOREIGN KEY and CHECK constraints. Untrusted constraints cannot be safely used by the optimizer and may indicate that existing data has not been validated.
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
  
;WITH ConstraintFindings AS  
(  
    SELECT  
        N'FOREIGN KEY' AS ConstraintType,  
        s.name AS SchemaName,  
        t.name AS TableName,  
        fk.name AS ConstraintName,  
        rs.name AS ReferencedSchemaName,  
        rt.name AS ReferencedTableName,  
        CAST(NULL AS nvarchar(max)) AS ConstraintDefinition,  
        fk.is_disabled AS IsDisabled,  
        fk.is_not_trusted AS IsNotTrusted,  
        fk.is_not_for_replication AS IsNotForReplication  
    FROM sys.foreign_keys AS fk  
    INNER JOIN sys.tables AS t  
        ON t.object_id = fk.parent_object_id  
    INNER JOIN sys.schemas AS s  
        ON s.schema_id = t.schema_id  
    INNER JOIN sys.tables AS rt  
        ON rt.object_id = fk.referenced_object_id  
    INNER JOIN sys.schemas AS rs  
        ON rs.schema_id = rt.schema_id  
    WHERE fk.is_ms_shipped = 0  
      AND  
      (  
          fk.is_disabled = 1  
          OR fk.is_not_trusted = 1  
      )  
  
    UNION ALL  
  
    SELECT  
        N'CHECK' AS ConstraintType,  
        s.name AS SchemaName,  
        t.name AS TableName,  
        cc.name AS ConstraintName,  
        CAST(NULL AS sysname) AS ReferencedSchemaName,  
        CAST(NULL AS sysname) AS ReferencedTableName,  
        cc.definition AS ConstraintDefinition,  
        cc.is_disabled AS IsDisabled,  
        cc.is_not_trusted AS IsNotTrusted,  
        cc.is_not_for_replication AS IsNotForReplication  
    FROM sys.check_constraints AS cc  
    INNER JOIN sys.tables AS t  
        ON t.object_id = cc.parent_object_id  
    INNER JOIN sys.schemas AS s  
        ON s.schema_id = t.schema_id  
    WHERE cc.is_ms_shipped = 0  
      AND  
      (  
          cc.is_disabled = 1  
          OR cc.is_not_trusted = 1  
      )  
)  
SELECT  
    CASE  
        WHEN cf.IsDisabled = 1 THEN 'High'  
        ELSE 'Medium'  
    END AS [Priority],  
    'Integrity' AS [Category],  
    CONCAT  
    (  
        QUOTENAME(cf.SchemaName),  
        '.',  
        QUOTENAME(cf.TableName),  
        '.',  
        QUOTENAME(cf.ConstraintName)  
    ) AS [Object],  
    CASE  
        WHEN cf.IsDisabled = 1 THEN  
            CONCAT(cf.ConstraintType, ' constraint is disabled')  
        WHEN cf.IsNotForReplication = 1 THEN  
            CONCAT  
            (  
                cf.ConstraintType,  
                ' constraint is untrusted and marked NOT FOR REPLICATION'  
            )  
        ELSE  
            CONCAT(cf.ConstraintType, ' constraint is enabled but untrusted')  
    END AS [Finding],  
    CONCAT  
    (  
        'type=', cf.ConstraintType,  
        '; disabled=',  
        CASE WHEN cf.IsDisabled = 1 THEN 'Yes' ELSE 'No' END,  
        '; trusted=',  
        CASE WHEN cf.IsNotTrusted = 1 THEN 'No' ELSE 'Yes' END,  
        '; not for replication=',  
        CASE WHEN cf.IsNotForReplication = 1 THEN 'Yes' ELSE 'No' END,  
        CASE  
            WHEN cf.ConstraintType = N'FOREIGN KEY' THEN  
                CONCAT  
                (  
                    '; references=',  
                    QUOTENAME(cf.ReferencedSchemaName),  
                    '.',  
                    QUOTENAME(cf.ReferencedTableName)  
                )  
            ELSE  
                CONCAT  
                (  
                    '; expression=',  
                    LEFT  
                    (  
                        REPLACE  
                        (  
                            REPLACE  
                            (  
                                COALESCE(cf.ConstraintDefinition, N''),  
                                CHAR(13),  
                                N' '  
                            ),  
                            CHAR(10),  
                            N' '  
                        ),  
                        1500  
                    )  
                )  
        END  
    ) AS [Evidence],  
    CASE  
        WHEN cf.IsNotForReplication = 1 THEN  
            'Review the replication design and verify whether NOT FOR REPLICATION is intentional. Validate existing data and only re-enable or change the constraint after confirming replication requirements.'  
        ELSE  
            'Identify and correct violating rows, then validate and enable the constraint using WITH CHECK CHECK CONSTRAINT during a controlled maintenance window.'  
    END AS [Recommendation],  
    CASE  
        WHEN cf.IsNotForReplication = 1 THEN  
            CONCAT  
            (  
                '-- NOT FOR REPLICATION is configured. Review replication semantics before applying changes.',  
                CHAR(13),  
                CHAR(10),  
                '-- Candidate validation command: ALTER TABLE ',  
                QUOTENAME(cf.SchemaName),  
                '.',  
                QUOTENAME(cf.TableName),  
                ' WITH CHECK CHECK CONSTRAINT ',  
                QUOTENAME(cf.ConstraintName),  
                ';'  
            )  
        ELSE  
            CONCAT  
            (  
                'ALTER TABLE ',  
                QUOTENAME(cf.SchemaName),  
                '.',  
                QUOTENAME(cf.TableName),  
                ' WITH CHECK CHECK CONSTRAINT ',  
                QUOTENAME(cf.ConstraintName),  
                ';'  
            )  
    END AS [SuggestedSql],  
    'High: validating a constraint scans existing rows, may block concurrent activity, and fails if violations exist. Changing NOT FOR REPLICATION settings can affect replication.' AS [Risk]  
FROM ConstraintFindings AS cf  
ORDER BY  
    CASE WHEN cf.IsDisabled = 1 THEN 0 ELSE 1 END,  
    cf.SchemaName,  
    cf.TableName,  
    cf.ConstraintName;  
