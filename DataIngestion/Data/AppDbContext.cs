using DataIngestion.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataIngestion.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<IngestionEvent> IngestionEvents => Set<IngestionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<IngestionEvent>(entity =>
        {
            entity.ToTable("IngestionEvents");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.CustomerId)
                .IsRequired();

            entity.Property(x => x.TransactionDate)
                .IsRequired();

            entity.Property(x => x.Amount)
                .HasColumnType("decimal(18,6)")
                .IsRequired();

            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(x => x.SourceChannel)
                .HasMaxLength(32)
                .IsRequired();

            entity.HasIndex(x => new
                {
                    x.CustomerId,
                    x.TransactionDate,
                    x.Amount,
                    x.Currency,
                    x.SourceChannel
                })
                .IsUnique();
        });
    }
}

