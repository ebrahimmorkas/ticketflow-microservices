using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TicketFlow.Payments.Api.Domain;

namespace TicketFlow.Payments.Api.Data;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments");

        modelBuilder.Entity<Payment>(p =>
        {
            p.ToTable("payments");
            p.HasKey(x => x.Id);
            p.HasIndex(x => x.BookingId).IsUnique();
            p.Property(x => x.CustomerEmail).HasMaxLength(256).IsRequired();
            p.Property(x => x.Amount).HasPrecision(18, 2);
            p.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            p.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            p.Property(x => x.GatewayReference).HasMaxLength(100);
            p.Property(x => x.FailureReason).HasMaxLength(500);
        });

        modelBuilder.AddTransactionalOutboxEntities();
    }
}

/// <summary>
/// Used only by <c>dotnet ef</c> to create migrations; at runtime the connection comes from Aspire.
/// </summary>
internal sealed class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql("Host=localhost;Database=paymentsdb;Username=postgres;Password=postgres")
            .Options);
}
