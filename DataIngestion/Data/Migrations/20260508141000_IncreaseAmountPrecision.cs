using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataIngestion.Data.Migrations;

public partial class IncreaseAmountPrecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_IngestionEvents_Dedup' AND object_id = OBJECT_ID(N'[IngestionEvents]'))
    BEGIN
        DROP INDEX [UX_IngestionEvents_Dedup] ON [IngestionEvents];
    END;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IngestionEvents_CustomerId_TransactionDate_Amount_Currency_SourceChannel' AND object_id = OBJECT_ID(N'[IngestionEvents]'))
    BEGIN
        DROP INDEX [IX_IngestionEvents_CustomerId_TransactionDate_Amount_Currency_SourceChannel] ON [IngestionEvents];
    END;

    ALTER TABLE [IngestionEvents] ALTER COLUMN [Amount] decimal(18,6) NOT NULL;

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

    ALTER TABLE [IngestionEvents] ALTER COLUMN [Amount] decimal(18,2) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_IngestionEvents_Dedup' AND object_id = OBJECT_ID(N'[IngestionEvents]'))
    BEGIN
        CREATE UNIQUE INDEX [UX_IngestionEvents_Dedup]
        ON [IngestionEvents]([CustomerId],[TransactionDate],[Amount],[Currency],[SourceChannel])
        WHERE [SourceChannel] <> N'legacy';
    END;
END;
");
    }
}

