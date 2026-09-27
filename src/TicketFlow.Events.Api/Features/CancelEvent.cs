using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using TicketFlow.BuildingBlocks.Results;
using TicketFlow.BuildingBlocks.Validation;
using TicketFlow.Contracts;
using TicketFlow.Events.Api.Data;
using TicketFlow.Events.Api.Domain;
using Event = TicketFlow.Events.Api.Domain.Event;

namespace TicketFlow.Events.Api.Features;

public static class CancelEvent
{
    public sealed record Request(string Reason);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(500);
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/events/{id:guid}/cancel", Handle)
            .WithName("CancelEvent")
            .WithSummary("Cancels an event; existing bookings are cancelled by the Booking service")
            .WithValidation<Request>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    internal static async Task<IResult> Handle(
        Guid id,
        Request request,
        EventsDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (@event is null)
        {
            return EventErrors.NotFound(id).ToProblem();
        }

        var wasPublished = @event.Status == EventStatus.Published;
        var result = @event.Cancel();
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        if (wasPublished)
        {
            await publishEndpoint.Publish(new EventCancelled(@event.Id, request.Reason), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
