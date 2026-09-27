using Microsoft.EntityFrameworkCore;
using TicketFlow.BuildingBlocks.Results;
using TicketFlow.Events.Api.Data;
using TicketFlow.Events.Api.Domain;

namespace TicketFlow.Events.Api.Features;

public static class GetEvents
{
    public sealed record PagedResponse(IReadOnlyList<EventResponse> Items, int Page, int PageSize, int TotalCount);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/events", HandleList)
            .WithName("ListEvents")
            .WithSummary("Lists upcoming published events")
            .Produces<PagedResponse>();

        app.MapGet("/events/{id:guid}", HandleGet)
            .WithName("GetEvent")
            .WithSummary("Gets an event with its ticket types")
            .Produces<EventResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    internal static async Task<IResult> HandleList(
        EventsDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        string? search = null,
        int page = 1,
        int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var query = dbContext.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Published && e.StartsAtUtc > utcNow);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.Name, pattern) || EF.Functions.ILike(e.Venue, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.StartsAtUtc)
            .ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(EventResponse.Projection)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new PagedResponse(items, page, pageSize, total));
    }

    internal static async Task<IResult> HandleGet(Guid id, EventsDbContext dbContext, CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(EventResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return @event is null ? EventErrors.NotFound(id).ToProblem() : TypedResults.Ok(@event);
    }
}
