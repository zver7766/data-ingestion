using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataIngestion.Data.Migrations;

public partial class AddCustomerTransactionsIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_IngestionEvents_CustomerId_TransactionDate'
          AND object_id = OBJECT_ID(N'[IngestionEvents]')
    )
    BEGIN
        CREATE INDEX [IX_IngestionEvents_CustomerId_TransactionDate]
        ON [IngestionEvents]([CustomerId], [TransactionDate] DESC, [Id] DESC);
    END
END;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_IngestionEvents_CustomerId_TransactionDate'
          AND object_id = OBJECT_ID(N'[IngestionEvents]')
    )
    BEGIN
        DROP INDEX [IX_IngestionEvents_CustomerId_TransactionDate] ON [IngestionEvents];
    END
END;
");
    }
}

