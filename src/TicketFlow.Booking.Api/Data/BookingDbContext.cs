using MassTransit;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Booking.Api.Domain;
using BookingEntity = TicketFlow.Booking.Api.Domain.Booking;

namespace TicketFlow.Booking.Api.Data;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();

    public DbSet<TicketInventory> Inventory => Set<TicketInventory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("booking");

        modelBuilder.Entity<TicketInventory>(i =>
        {
            i.ToTable("ticket_inventory");
            i.HasKey(x => x.TicketTypeId);
            i.Property(x => x.EventName).HasMaxLength(200).IsRequired();
            i.Property(x => x.TicketTypeName).HasMaxLength(100).IsRequired();
            i.Property(x => x.Price).HasPrecision(18, 2);
            i.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            i.Ignore(x => x.Available);
            i.HasIndex(x => x.EventId);

            // xmin row version: concurrent reservations of the same ticket type can't overbook.
            i.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<BookingEntity>(b =>
        {
            b.ToTable("bookings");
            b.HasKey(x => x.Id);
            b.Property(x => x.EventName).HasMaxLength(200).IsRequired();
            b.Property(x => x.CustomerEmail).HasMaxLength(256).IsRequired();
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.CancellationReason).HasMaxLength(500);
            b.Ignore(x => x.Total);
            b.HasIndex(x => x.EventId);
            b.HasIndex(x => x.CustomerEmail);
        });

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
