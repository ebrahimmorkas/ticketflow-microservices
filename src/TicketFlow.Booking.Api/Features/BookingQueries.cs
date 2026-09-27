using Microsoft.EntityFrameworkCore;
using TicketFlow.Booking.Api.Data;
using TicketFlow.Booking.Api.Domain;
using TicketFlow.BuildingBlocks.Results;
using BookingEntity = TicketFlow.Booking.Api.Domain.Booking;

namespace TicketFlow.Booking.Api.Features;

public sealed record BookingResponse(
    Guid Id,
    Guid EventId,
    string EventName,
    Guid TicketTypeId,
    string CustomerEmail,
    int Quantity,
    decimal Total,
    string Currency,
    string Status,
    string? CancellationReason,
    DateTime CreatedAtUtc)
{
    public static BookingResponse From(BookingEntity b) => new(
        b.Id, b.EventId, b.EventName, b.TicketTypeId, b.CustomerEmail, b.Quantity, b.Total, b.Currency,
        b.Status.ToString(), b.CancellationReason, b.CreatedAtUtc);
}

public sealed record AvailabilityResponse(Guid TicketTypeId, string TicketTypeName, decimal Price, string Currency, int Available, bool OnSale);

public static class BookingQueries
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/bookings/{id:guid}", GetBooking)
            .WithName("GetBooking")
            .WithSummary("Gets a booking and its current status")
            .Produces<BookingResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/events/{eventId:guid}/availability", GetAvailability)
            .WithName("GetAvailability")
            .WithSummary("Live ticket availability for an event")
            .Produces<IReadOnlyList<AvailabilityResponse>>();
    }

    internal static async Task<IResult> GetBooking(Guid id, BookingDbContext dbContext, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        return booking is null
            ? BookingErrors.NotFound(id).ToProblem()
            : TypedResults.Ok(BookingResponse.From(booking));
    }

    internal static async Task<IResult> GetAvailability(Guid eventId, BookingDbContext dbContext, CancellationToken cancellationToken)
    {
        var availability = await dbContext.Inventory
            .AsNoTracking()
            .Where(i => i.EventId == eventId)
            .OrderBy(i => i.Price)
            .Select(i => new AvailabilityResponse(i.TicketTypeId, i.TicketTypeName, i.Price, i.Currency, i.Capacity - i.Reserved, i.IsOnSale))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(availability);
    }
}
