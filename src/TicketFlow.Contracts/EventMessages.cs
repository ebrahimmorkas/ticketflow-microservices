namespace TicketFlow.Contracts;

/// <summary>Raised by the Events service when an event goes on sale.</summary>
public sealed record EventPublished(
    Guid EventId,
    string Name,
    string Venue,
    DateTime StartsAtUtc,
    IReadOnlyList<TicketTypeInfo> TicketTypes);

public sealed record TicketTypeInfo(Guid TicketTypeId, string Name, decimal Price, string Currency, int Capacity);

/// <summary>Raised by the Events service when an event is called off; bookings must be refunded.</summary>
public sealed record EventCancelled(Guid EventId, string Reason);
