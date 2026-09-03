/*******************************************************************************
Script Name: orphaned_records.sql
Purpose: Scans foreign key relationships and finds child records that do not have a corresponding parent record. Composite foreign keys and nullable columns are handled correctly.
Scope: Current database; all user foreign keys
SQL Server: 2016+
Azure SQL: Azure SQL Database and Managed Instance; see docs/COMPATIBILITY.md
Permissions: Metadata visibility; orphan scans also require SELECT on participating tables
Risk: Read-only for user data; potentially high I/O and blocking cost on large tables.
Output: Priority, Category, Object, Finding, Evidence, Recommendation, SuggestedSql, Risk
Author: TheMax-Lab
Version: 1.0
License: MIT
*******************************************************************************/  
  
SET NOCOUNT ON;  
  
IF OBJECT_ID('tempdb..#OrphanFindings') IS NOT NULL  
    DROP TABLE #OrphanFindings;  
  
CREATE TABLE #OrphanFindings  
(  
    [Priority]       varchar(10)   NOT NULL,  
    [Category]       varchar(30)   NOT NULL,  
    [Object]         nvarchar(776) NOT NULL,  
    [Finding]        nvarchar(4000) NOT NULL,  
    [Evidence]       nvarchar(max) NOT NULL,  
    [Recommendation] nvarchar(max) NOT NULL,  
    [SuggestedSql]   nvarchar(max) NOT NULL,  
    [Risk]           nvarchar(max) NOT NULL,  
    [SortCount]      bigint NULL  
);  
  
DECLARE  
    @FkObjectId            int,  
    @FkName                sysname,  
    @ChildSchema           sysname,  
    @ChildTable            sysname,  
    @ParentSchema          sysname,  
    @ParentTable           sysname,  
    @IsDisabled            bit,  
    @IsNotTrusted          bit,  
    @IsNotForReplication   bit,  
    @ChildObject           nvarchar(517),  
    @ParentObject          nvarchar(517),  
    @FindingObject         nvarchar(776),  
    @JoinPredicate         nvarchar(max),  
    @NotNullPredicate      nvarchar(max),  
    @ColumnMap             nvarchar(max),  
    @CountSql              nvarchar(max),  
    @SampleSql             nvarchar(max),  
    @OrphanCount           bigint;  
  
DECLARE ForeignKeyCursor CURSOR LOCAL FAST_FORWARD FOR  
    SELECT  
        fk.object_id,  
        fk.name,  
        child_schema.name,  
        child_table.name,  
        parent_schema.name,  
        parent_table.name,  
        fk.is_disabled,  
        fk.is_not_trusted,  
        fk.is_not_for_replication  
    FROM sys.foreign_keys AS fk  
    INNER JOIN sys.tables AS child_table  
        ON child_table.object_id = fk.parent_object_id  
    INNER JOIN sys.schemas AS child_schema  
        ON child_schema.schema_id = child_table.schema_id  
    INNER JOIN sys.tables AS parent_table  
        ON parent_table.object_id = fk.referenced_object_id  
    INNER JOIN sys.schemas AS parent_schema  
        ON parent_schema.schema_id = parent_table.schema_id  
    WHERE fk.is_ms_shipped = 0;  
  
    /*  
      For a faster scan limited to relationships at higher risk, add:  
  
      AND  
      (  
          fk.is_disabled = 1  
          OR fk.is_not_trusted = 1  
          OR fk.is_not_for_replication = 1  
      )  
    */  
  
OPEN ForeignKeyCursor;  
  
FETCH NEXT FROM ForeignKeyCursor  
INTO  
    @FkObjectId,  
    @FkName,  
    @ChildSchema,  
    @ChildTable,  
    @ParentSchema,  
    @ParentTable,  
    @IsDisabled,  
    @IsNotTrusted,  
    @IsNotForReplication;  
  
WHILE @@FETCH_STATUS = 0  
BEGIN  
    SET @ChildObject =  
        CONCAT(QUOTENAME(@ChildSchema), N'.', QUOTENAME(@ChildTable));  
  
    SET @ParentObject =  
        CONCAT(QUOTENAME(@ParentSchema), N'.', QUOTENAME(@ParentTable));  
  
    SET @FindingObject =  
        CONCAT(@ChildObject, N'.', QUOTENAME(@FkName));  
  
    SET @JoinPredicate = NULL;  
    SET @NotNullPredicate = NULL;  
    SET @ColumnMap = NULL;  
    SET @OrphanCount = 0;  
  
    SELECT  
        @JoinPredicate = STUFF  
        (  
            (  
                SELECT  
                    N' AND p.' + QUOTENAME(parent_column.name)  
                    + N' = c.' + QUOTENAME(child_column.name)  
                FROM sys.foreign_key_columns AS fkc  
                INNER JOIN sys.columns AS child_column  
                    ON child_column.object_id = fkc.parent_object_id  
                   AND child_column.column_id = fkc.parent_column_id  
                INNER JOIN sys.columns AS parent_column  
                    ON parent_column.object_id = fkc.referenced_object_id  
                   AND parent_column.column_id = fkc.referenced_column_id  
                WHERE fkc.constraint_object_id = @FkObjectId  
                ORDER BY fkc.constraint_column_id  
                FOR XML PATH(''), TYPE  
            ).value('.', 'nvarchar(max)'),  
            1,  
            5,  
            N''  
        );  
  
    SELECT  
        @NotNullPredicate = STUFF  
        (  
            (  
                SELECT  
                    N' AND c.' + QUOTENAME(child_column.name) + N' IS NOT NULL'  
                FROM sys.foreign_key_columns AS fkc  
                INNER JOIN sys.columns AS child_column  
                    ON child_column.object_id = fkc.parent_object_id  
                   AND child_column.column_id = fkc.parent_column_id  
                WHERE fkc.constraint_object_id = @FkObjectId  
                ORDER BY fkc.constraint_column_id  
                FOR XML PATH(''), TYPE  
            ).value('.', 'nvarchar(max)'),  
            1,  
            5,  
            N''  
        );  
  
    SELECT  
        @ColumnMap = STUFF  
        (  
            (  
                SELECT  
                    N', ' + QUOTENAME(child_column.name)  
                    + N' -> ' + QUOTENAME(parent_column.name)  
                FROM sys.foreign_key_columns AS fkc  
                INNER JOIN sys.columns AS child_column  
                    ON child_column.object_id = fkc.parent_object_id  
                   AND child_column.column_id = fkc.parent_column_id  
                INNER JOIN sys.columns AS parent_column  
                    ON parent_column.object_id = fkc.referenced_object_id  
                   AND parent_column.column_id = fkc.referenced_column_id  
                WHERE fkc.constraint_object_id = @FkObjectId  
                ORDER BY fkc.constraint_column_id  
                FOR XML PATH(''), TYPE  
            ).value('.', 'nvarchar(max)'),  
            1,  
            2,  
            N''  
        );  
  
    SET @CountSql = CONCAT  
    (  
        N'SELECT @OrphanCount = COUNT_BIG(*)',  
        CHAR(13), CHAR(10),  
        N'FROM ', @ChildObject, N' AS c',  
        CHAR(13), CHAR(10),  
        N'WHERE ', @NotNullPredicate,  
        CHAR(13), CHAR(10),  
        N'  AND NOT EXISTS',  
        CHAR(13), CHAR(10),  
        N'      (',  
        CHAR(13), CHAR(10),  
        N'          SELECT 1',  
        CHAR(13), CHAR(10),  
        N'          FROM ', @ParentObject, N' AS p',  
        CHAR(13), CHAR(10),  
        N'          WHERE ', @JoinPredicate,  
        CHAR(13), CHAR(10),  
        N'      );'  
    );  
  
    SET @SampleSql = CONCAT  
    (  
        N'-- Sample orphaned child rows for ', @FindingObject,  
        CHAR(13), CHAR(10),  
        N'SELECT TOP (100) c.*',  
        CHAR(13), CHAR(10),  
        N'FROM ', @ChildObject, N' AS c',  
        CHAR(13), CHAR(10),  
        N'WHERE ', @NotNullPredicate,  
        CHAR(13), CHAR(10),  
        N'  AND NOT EXISTS',  
        CHAR(13), CHAR(10),  
        N'      (',  
        CHAR(13), CHAR(10),  
        N'          SELECT 1',  
        CHAR(13), CHAR(10),  
        N'          FROM ', @ParentObject, N' AS p',  
        CHAR(13), CHAR(10),  
        N'          WHERE ', @JoinPredicate,  
        CHAR(13), CHAR(10),  
        N'      );'  
    );  
  
    BEGIN TRY  
        EXEC sys.sp_executesql  
            @stmt = @CountSql,  
            @params = N'@OrphanCount bigint OUTPUT',  
            @OrphanCount = @OrphanCount OUTPUT;  
  
        IF @OrphanCount > 0  
        BEGIN  
            INSERT INTO #OrphanFindings  
            (  
                [Priority],  
                [Category],  
                [Object],  
                [Finding],  
                [Evidence],  
                [Recommendation],  
                [SuggestedSql],  
                [Risk],  
                [SortCount]  
            )  
            VALUES  
            (  
                'High',  
                'Integrity',  
                @FindingObject,  
                'Orphaned child records detected',  
                CONCAT  
                (  
                    'orphaned rows=', @OrphanCount,  
                    '; child=', @ChildObject,  
                    '; parent=', @ParentObject,  
                    '; columns=', @ColumnMap,  
                    '; disabled=',  
                    CASE WHEN @IsDisabled = 1 THEN 'Yes' ELSE 'No' END,  
                    '; trusted=',  
                    CASE WHEN @IsNotTrusted = 1 THEN 'No' ELSE 'Yes' END,  
                    '; not for replication=',  
                    CASE WHEN @IsNotForReplication = 1 THEN 'Yes' ELSE 'No' END  
                ),  
                'Review the orphaned rows and business rules. Restore the missing parent rows or correct/delete the child rows, then enable and validate the foreign key in a controlled maintenance window.',  
                @SampleSql,  
                'High: automatically deleting or updating orphaned rows can cause data loss. Validation and remediation may scan large tables, block activity, and generate significant transaction log usage.',  
                @OrphanCount  
            );  
        END;  
    END TRY  
    BEGIN CATCH  
        INSERT INTO #OrphanFindings  
        (  
            [Priority],  
            [Category],  
            [Object],  
            [Finding],  
            [Evidence],  
            [Recommendation],  
            [SuggestedSql],  
            [Risk],  
            [SortCount]  
        )  
        VALUES  
        (  
            'Medium',  
            'Integrity',  
            @FindingObject,  
            'Orphan scan could not be completed',  
            CONCAT  
            (  
                'error=', ERROR_NUMBER(),  
                '; message=', ERROR_MESSAGE(),  
                '; child=', @ChildObject,  
                '; parent=', @ParentObject  
            ),  
            'Verify SELECT permissions, object availability, blocking, and concurrent schema changes, then execute the scan again.',  
            '-- Scan failed. No remediation SQL can be generated safely.',  
            'Medium: the relationship was not validated, so the absence of an orphan finding cannot be assumed.',  
            NULL  
        );  
    END CATCH;  
  
    FETCH NEXT FROM ForeignKeyCursor  
    INTO  
        @FkObjectId,  
        @FkName,  
        @ChildSchema,  
        @ChildTable,  
        @ParentSchema,  
        @ParentTable,  
        @IsDisabled,  
        @IsNotTrusted,  
        @IsNotForReplication;  
END;  
  
CLOSE ForeignKeyCursor;  
DEALLOCATE ForeignKeyCursor;  
  
SELECT  
    [Priority],  
    [Category],  
    [Object],  
    [Finding],  
    [Evidence],  
    [Recommendation],  
    [SuggestedSql],  
    [Risk]  
FROM #OrphanFindings  
ORDER BY  
    CASE [Priority]  
        WHEN 'High' THEN 0  
        WHEN 'Medium' THEN 1  
        ELSE 2  
    END,  
    [SortCount] DESC,  
    [Object];  
  
DROP TABLE #OrphanFindings;  
