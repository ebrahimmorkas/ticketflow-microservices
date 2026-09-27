using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TicketFlow.Events.Api.Data;

/// <summary>
/// Used only by <c>dotnet ef</c> to create migrations; at runtime the connection comes from Aspire.
/// </summary>
internal sealed class EventsDbContextFactory : IDesignTimeDbContextFactory<EventsDbContext>
{
    public EventsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<EventsDbContext>()
            .UseNpgsql("Host=localhost;Database=eventsdb;Username=postgres;Password=postgres")
            .Options);
}
