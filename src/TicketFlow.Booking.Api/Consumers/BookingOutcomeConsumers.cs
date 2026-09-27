using MassTransit;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Booking.Api.Data;
using TicketFlow.Booking.Api.Domain;
using TicketFlow.Contracts;

namespace TicketFlow.Booking.Api.Consumers;

/// <summary>Marks the booking as confirmed once the saga reports a successful payment.</summary>
public sealed partial class BookingConfirmedConsumer(
    BookingDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<BookingConfirmedConsumer> logger)
    : IConsumer<BookingConfirmed>
{
    public async Task Consume(ConsumeContext<BookingConfirmed> context)
    {
        var booking = await dbContext.Bookings.SingleOrDefaultAsync(b => b.Id == context.Message.BookingId, context.CancellationToken);
        if (booking is null || booking.Status == BookingStatus.Confirmed)
        {
            return;
        }

        var result = booking.Confirm(timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure)
        {
            // e.g. the event was cancelled while payment was in flight; a refund flow would start here.
            LogCannotConfirm(logger, booking.Id, booking.Status);
            return;
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Booking {BookingId} was paid but is {Status}; refund required")]
    private static partial void LogCannotConfirm(ILogger logger, Guid bookingId, BookingStatus status);
}

/// <summary>
/// Compensating action for a failed checkout: cancels the booking and returns its seats to inventory.
/// </summary>
public sealed class BookingCancelledConsumer(BookingDbContext dbContext, TimeProvider timeProvider) : IConsumer<BookingCancelled>
{
    public async Task Consume(ConsumeContext<BookingCancelled> context)
    {
        var booking = await dbContext.Bookings.SingleOrDefaultAsync(b => b.Id == context.Message.BookingId, context.CancellationToken);
        if (booking is null || booking.Status == BookingStatus.Cancelled)
        {
            // Already compensated (or cancelled together with its event) – nothing to do.
            return;
        }

        booking.Cancel(context.Message.Reason, timeProvider.GetUtcNow().UtcDateTime);

        var inventory = await dbContext.Inventory.SingleOrDefaultAsync(i => i.TicketTypeId == booking.TicketTypeId, context.CancellationToken);
        inventory?.Release(booking.Quantity);

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
