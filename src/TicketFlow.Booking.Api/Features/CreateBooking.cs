using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Booking.Api.Data;
using TicketFlow.Booking.Api.Domain;
using TicketFlow.BuildingBlocks.Results;
using TicketFlow.BuildingBlocks.Validation;
using TicketFlow.Contracts;
using BookingEntity = TicketFlow.Booking.Api.Domain.Booking;

namespace TicketFlow.Booking.Api.Features;

public static class CreateBooking
{
    private const int MaxAttempts = 3;

    public sealed record Request(Guid TicketTypeId, int Quantity, string CustomerEmail);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.TicketTypeId).NotEmpty();
            RuleFor(r => r.Quantity).InclusiveBetween(1, 10);
            RuleFor(r => r.CustomerEmail).NotEmpty().EmailAddress().MaximumLength(256);
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/bookings", Handle)
            .WithName("CreateBooking")
            .WithSummary("Reserves tickets and starts the payment process")
            .WithValidation<Request>()
            .Produces<BookingResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    internal static async Task<IResult> Handle(
        Request request,
        BookingDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var result = await ReserveAsync(request, dbContext, publishEndpoint, timeProvider, cancellationToken);

        // 202: the booking exists but its outcome depends on asynchronous payment processing.
        return result.Match(booking => TypedResults.AcceptedAtRoute(
            BookingResponse.From(booking),
            "GetBooking",
            new { id = booking.Id }));
    }

    internal static async Task<Result<BookingEntity>> ReserveAsync(
        Request request,
        BookingDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // Optimistic concurrency with retry: under contention another request may reserve the same
        // ticket type first; we reload fresh stock and try again instead of failing immediately.
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var inventory = await dbContext.Inventory
                .SingleOrDefaultAsync(i => i.TicketTypeId == request.TicketTypeId, cancellationToken);

            if (inventory is null)
            {
                return BookingErrors.TicketTypeNotFound(request.TicketTypeId);
            }

            var reservation = inventory.Reserve(request.Quantity);
            if (reservation.IsFailure)
            {
                return reservation.Error!;
            }

            var booking = BookingEntity.Create(inventory, request.CustomerEmail, request.Quantity, timeProvider.GetUtcNow().UtcDateTime);
            dbContext.Bookings.Add(booking);

            await publishEndpoint.Publish(
                new BookingCreated(
                    booking.Id,
                    booking.EventId,
                    booking.EventName,
                    booking.CustomerEmail,
                    booking.Quantity,
                    booking.Total,
                    booking.Currency),
                cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return booking;
            }
            catch (DbUpdateConcurrencyException)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        return BookingErrors.ConcurrencyConflict;
    }
}
