using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataIngestion.Data.Migrations;

public partial class ReshapeIngestionEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Split into multiple batches to avoid SQL Server compile-time errors
        // when referencing columns added earlier in the same batch.
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'IngestionEvents', N'TransactionDate') IS NULL
    BEGIN
        ALTER TABLE [IngestionEvents] ADD [TransactionDate] datetime2 NOT NULL CONSTRAINT [DF_IngestionEvents_TransactionDate] DEFAULT (SYSUTCDATETIME());
    END;

    IF COL_LENGTH(N'IngestionEvents', N'Amount') IS NULL
    BEGIN
        ALTER TABLE [IngestionEvents] ADD [Amount] decimal(18,2) NOT NULL CONSTRAINT [DF_IngestionEvents_Amount] DEFAULT (0);
    END;

    IF COL_LENGTH(N'IngestionEvents', N'Currency') IS NULL
    BEGIN
        ALTER TABLE [IngestionEvents] ADD [Currency] nvarchar(3) NOT NULL CONSTRAINT [DF_IngestionEvents_Currency] DEFAULT (N'XXX');
    END;

    IF COL_LENGTH(N'IngestionEvents', N'SourceChannel') IS NULL
    BEGIN
        ALTER TABLE [IngestionEvents] ADD [SourceChannel] nvarchar(32) NOT NULL CONSTRAINT [DF_IngestionEvents_SourceChannel] DEFAULT (N'legacy');
    END;

    IF COL_LENGTH(N'IngestionEvents', N'CustomerId') IS NULL
    BEGIN
        ALTER TABLE [IngestionEvents] ADD [CustomerId] bigint NOT NULL CONSTRAINT [DF_IngestionEvents_CustomerId] DEFAULT (0);
    END;
END;
");

        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NOT NULL
BEGIN
    -- Migrate legacy columns if present
    IF COL_LENGTH(N'IngestionEvents', N'CreatedAtUtc') IS NOT NULL
    BEGIN
        UPDATE [IngestionEvents]
        SET [TransactionDate] = [CreatedAtUtc]
        WHERE [TransactionDate] = CONVERT(datetime2, '0001-01-01T00:00:00');
    END;
END;
");

        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NOT NULL
BEGIN
    -- Drop legacy FK/index if present
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_IngestionEvents_Customers_CustomerId')
    BEGIN
        ALTER TABLE [IngestionEvents] DROP CONSTRAINT [FK_IngestionEvents_Customers_CustomerId];
    END;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IngestionEvents_CustomerId' AND object_id = OBJECT_ID(N'[IngestionEvents]'))
    BEGIN
        DROP INDEX [IX_IngestionEvents_CustomerId] ON [IngestionEvents];
    END;
END;
");

        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NOT NULL
BEGIN
    -- Drop old columns
    IF COL_LENGTH(N'IngestionEvents', N'Payload') IS NOT NULL
    BEGIN
        DECLARE @df_payload sysname;
        SELECT @df_payload = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
        INNER JOIN sys.tables t ON t.object_id = c.object_id
        WHERE t.name = N'IngestionEvents' AND c.name = N'Payload';

        IF @df_payload IS NOT NULL
        BEGIN
            EXEC(N'ALTER TABLE [IngestionEvents] DROP CONSTRAINT [' + @df_payload + N']');
        END;

        ALTER TABLE [IngestionEvents] DROP COLUMN [Payload];
    END;

    IF COL_LENGTH(N'IngestionEvents', N'CreatedAtUtc') IS NOT NULL
    BEGIN
        DECLARE @df_created sysname;
        SELECT @df_created = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
        INNER JOIN sys.tables t ON t.object_id = c.object_id
        WHERE t.name = N'IngestionEvents' AND c.name = N'CreatedAtUtc';

        IF @df_created IS NOT NULL
        BEGIN
            EXEC(N'ALTER TABLE [IngestionEvents] DROP CONSTRAINT [' + @df_created + N']');
        END;

        ALTER TABLE [IngestionEvents] DROP COLUMN [CreatedAtUtc];
    END;
END;
");

        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NOT NULL
BEGIN
    -- Unique index for de-duplication (ignore legacy rows)
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_IngestionEvents_Dedup' AND object_id = OBJECT_ID(N'[IngestionEvents]'))
    BEGIN
        CREATE UNIQUE INDEX [UX_IngestionEvents_Dedup]
        ON [IngestionEvents]([CustomerId],[TransactionDate],[Amount],[Currency],[SourceChannel])
        WHERE [SourceChannel] <> N'legacy';
    END;
END;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_IngestionEvents_Dedup' AND object_id = OBJECT_ID(N'[IngestionEvents]'))
    BEGIN
        DROP INDEX [UX_IngestionEvents_Dedup] ON [IngestionEvents];
    END;
END;
");
    }
}

