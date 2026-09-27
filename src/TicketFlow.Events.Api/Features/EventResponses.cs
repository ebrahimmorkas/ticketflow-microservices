using System.Linq.Expressions;
using TicketFlow.Events.Api.Domain;

namespace TicketFlow.Events.Api.Features;

public sealed record EventResponse(
    Guid Id,
    string Name,
    string Description,
    string Venue,
    DateTime StartsAtUtc,
    string Status,
    IReadOnlyList<TicketTypeResponse> TicketTypes)
{
    public static readonly Expression<Func<Event, EventResponse>> Projection = e => new EventResponse(
        e.Id,
        e.Name,
        e.Description,
        e.Venue,
        e.StartsAtUtc,
        e.Status.ToString(),
        e.TicketTypes.Select(t => new TicketTypeResponse(t.Id, t.Name, t.Price, t.Currency, t.Capacity)).ToList());
}

public sealed record TicketTypeResponse(Guid Id, string Name, decimal Price, string Currency, int Capacity);
