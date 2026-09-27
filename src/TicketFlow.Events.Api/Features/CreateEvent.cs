using FluentValidation;
using TicketFlow.BuildingBlocks.Results;
using TicketFlow.BuildingBlocks.Validation;
using TicketFlow.Events.Api.Data;
using TicketFlow.Events.Api.Domain;

namespace TicketFlow.Events.Api.Features;

public static class CreateEvent
{
    public sealed record Request(
        string Name,
        string Description,
        string Venue,
        DateTime StartsAtUtc,
        IReadOnlyList<TicketTypeRequest> TicketTypes);

    public sealed record TicketTypeRequest(string Name, decimal Price, string Currency, int Capacity);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
            RuleFor(r => r.Description).MaximumLength(4000);
            RuleFor(r => r.Venue).NotEmpty().MaximumLength(200);
            RuleFor(r => r.TicketTypes).NotEmpty();
            RuleForEach(r => r.TicketTypes).ChildRules(t =>
            {
                t.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
                t.RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
                t.RuleFor(x => x.Currency).NotEmpty().Length(3);
                t.RuleFor(x => x.Capacity).InclusiveBetween(1, 100_000);
            });
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/events", Handle)
            .WithName("CreateEvent")
            .WithSummary("Creates a draft event with its ticket types")
            .WithValidation<Request>()
            .ProducesProblem(StatusCodes.Status409Conflict);

    internal static async Task<IResult> Handle(
        Request request,
        EventsDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var result = await CreateAsync(request, dbContext, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return result.Match(id => TypedResults.CreatedAtRoute(new { id }, "GetEvent", new { id }));
    }

    internal static async Task<Result<Guid>> CreateAsync(
        Request request,
        EventsDbContext dbContext,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var eventResult = Event.Create(request.Name, request.Description, request.Venue, request.StartsAtUtc, utcNow);
        if (eventResult.IsFailure)
        {
            return eventResult.Error!;
        }

        var @event = eventResult.Value;
        foreach (var ticketType in request.TicketTypes)
        {
            var added = @event.AddTicketType(ticketType.Name, ticketType.Price, ticketType.Currency, ticketType.Capacity);
            if (added.IsFailure)
            {
                return added.Error!;
            }
        }

        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync(cancellationToken);

        return @event.Id;
    }
}
