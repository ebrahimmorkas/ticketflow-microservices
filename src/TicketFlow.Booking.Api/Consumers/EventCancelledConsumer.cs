using MassTransit;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Booking.Api.Data;
using TicketFlow.Booking.Api.Domain;
using TicketFlow.Contracts;

namespace TicketFlow.Booking.Api.Consumers;

/// <summary>
/// Stops sales for a cancelled event and cancels its active bookings so customers are notified.
/// </summary>
public sealed class EventCancelledConsumer(BookingDbContext dbContext, TimeProvider timeProvider) : IConsumer<EventCancelled>
{
    public async Task Consume(ConsumeContext<EventCancelled> context)
    {
        var eventId = context.Message.EventId;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var reason = $"Event cancelled: {context.Message.Reason}";

        var inventory = await dbContext.Inventory.Where(i => i.EventId == eventId).ToListAsync(context.CancellationToken);
        inventory.ForEach(i => i.StopSales());

        var bookings = await dbContext.Bookings
            .Where(b => b.EventId == eventId && b.Status != BookingStatus.Cancelled)
            .ToListAsync(context.CancellationToken);

        foreach (var booking in bookings)
        {
            booking.Cancel(reason, utcNow);
            await context.Publish(
                new BookingCancelled(booking.Id, booking.EventName, booking.CustomerEmail, reason),
                context.CancellationToken);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
