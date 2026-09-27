using MassTransit;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Events.Api.Domain;
using Event = TicketFlow.Events.Api.Domain.Event;

namespace TicketFlow.Events.Api.Data;

public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    public DbSet<TicketType> TicketTypes => Set<TicketType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("events");

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Venue).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasMany(x => x.TicketTypes).WithOne().HasForeignKey(t => t.EventId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.TicketTypes).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasIndex(x => new { x.Status, x.StartsAtUtc });
        });

        modelBuilder.Entity<TicketType>(t =>
        {
            t.ToTable("ticket_types");
            t.HasKey(x => x.Id);
            t.Property(x => x.Name).HasMaxLength(100).IsRequired();
            t.Property(x => x.Price).HasPrecision(18, 2);
            t.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        });

        // Tables for MassTransit's transactional outbox and inbox.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
