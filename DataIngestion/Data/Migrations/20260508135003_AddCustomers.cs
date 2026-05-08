using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataIngestion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[Customers]', N'U') IS NULL
BEGIN
    CREATE TABLE [Customers] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL CONSTRAINT [DF_Customers_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
    );
END;

IF OBJECT_ID(N'[IngestionEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [IngestionEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [CreatedAtUtc] datetime2 NOT NULL CONSTRAINT [DF_IngestionEvents_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [Payload] nvarchar(4000) NULL,
        [CustomerId] bigint NULL,
        CONSTRAINT [PK_IngestionEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IngestionEvents_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers]([Id]) ON DELETE SET NULL
    );

    CREATE INDEX [IX_IngestionEvents_CustomerId] ON [IngestionEvents]([CustomerId]);
END
ELSE
BEGIN
    IF COL_LENGTH(N'IngestionEvents', N'CustomerId') IS NULL
    BEGIN
        ALTER TABLE [IngestionEvents] ADD [CustomerId] bigint NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IngestionEvents_CustomerId' AND object_id = OBJECT_ID(N'[IngestionEvents]'))
    BEGIN
        CREATE INDEX [IX_IngestionEvents_CustomerId] ON [IngestionEvents]([CustomerId]);
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_IngestionEvents_Customers_CustomerId')
    BEGIN
        ALTER TABLE [IngestionEvents]
        ADD CONSTRAINT [FK_IngestionEvents_Customers_CustomerId]
            FOREIGN KEY ([CustomerId]) REFERENCES [Customers]([Id]) ON DELETE SET NULL;
    END;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_IngestionEvents_Customers_CustomerId')
BEGIN
    ALTER TABLE [IngestionEvents] DROP CONSTRAINT [FK_IngestionEvents_Customers_CustomerId];
END;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IngestionEvents_CustomerId' AND object_id = OBJECT_ID(N'[IngestionEvents]'))
BEGIN
    DROP INDEX [IX_IngestionEvents_CustomerId] ON [IngestionEvents];
END;

IF COL_LENGTH(N'IngestionEvents', N'CustomerId') IS NOT NULL
BEGIN
    ALTER TABLE [IngestionEvents] DROP COLUMN [CustomerId];
END;

IF OBJECT_ID(N'[Customers]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [Customers];
END;
");
        }
    }
}
