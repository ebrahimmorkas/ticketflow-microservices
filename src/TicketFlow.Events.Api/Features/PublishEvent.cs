using MassTransit;
using Microsoft.EntityFrameworkCore;
using TicketFlow.BuildingBlocks.Results;
using TicketFlow.Contracts;
using TicketFlow.Events.Api.Data;
using TicketFlow.Events.Api.Domain;
using Event = TicketFlow.Events.Api.Domain.Event;

namespace TicketFlow.Events.Api.Features;

public static class PublishEvent
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/events/{id:guid}/publish", Handle)
            .WithName("PublishEvent")
            .WithSummary("Puts a draft event on sale and notifies other services")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    internal static async Task<IResult> Handle(
        Guid id,
        EventsDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events
            .Include(e => e.TicketTypes)
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (@event is null)
        {
            return EventErrors.NotFound(id).ToProblem();
        }

        var result = @event.Publish(timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        // With the bus outbox, Publish only writes to the outbox table. The message is delivered to
        // RabbitMQ after SaveChanges commits, so the status change and the event can never diverge.
        await publishEndpoint.Publish(
            new EventPublished(
                @event.Id,
                @event.Name,
                @event.Venue,
                @event.StartsAtUtc,
                @event.TicketTypes.Select(t => new TicketTypeInfo(t.Id, t.Name, t.Price, t.Currency, t.Capacity)).ToList()),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
