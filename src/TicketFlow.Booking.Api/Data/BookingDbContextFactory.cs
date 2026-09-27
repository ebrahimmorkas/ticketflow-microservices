using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TicketFlow.Booking.Api.Data;

/// <summary>
/// Used only by <c>dotnet ef</c> to create migrations; at runtime the connection comes from Aspire.
/// </summary>
internal sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql("Host=localhost;Database=bookingdb;Username=postgres;Password=postgres")
            .Options);
}
