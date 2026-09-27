using TicketFlow.BuildingBlocks.Results;

namespace TicketFlow.Events.Api.Domain;

public sealed class Event
{
    private readonly List<TicketType> _ticketTypes = [];

    private Event()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Venue { get; private set; } = string.Empty;

    public DateTime StartsAtUtc { get; private set; }

    public EventStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public IReadOnlyCollection<TicketType> TicketTypes => _ticketTypes.AsReadOnly();

    public static Result<Event> Create(string name, string description, string venue, DateTime startsAtUtc, DateTime utcNow)
    {
        startsAtUtc = startsAtUtc.Kind == DateTimeKind.Local
            ? startsAtUtc.ToUniversalTime()
            : DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc);

        if (startsAtUtc <= utcNow)
        {
            return EventErrors.StartsInPast;
        }

        return new Event
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = description.Trim(),
            Venue = venue.Trim(),
            StartsAtUtc = startsAtUtc,
            Status = EventStatus.Draft,
            CreatedAtUtc = utcNow
        };
    }

    public Result<TicketType> AddTicketType(string name, decimal price, string currency, int capacity)
    {
        if (Status != EventStatus.Draft)
        {
            return EventErrors.NotDraft;
        }

        if (_ticketTypes.Any(t => string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return EventErrors.DuplicateTicketType(name);
        }

        var ticketType = new TicketType(Guid.CreateVersion7(), Id, name.Trim(), price, currency.ToUpperInvariant(), capacity);
        _ticketTypes.Add(ticketType);
        return ticketType;
    }

    public Result Publish(DateTime utcNow)
    {
        if (Status != EventStatus.Draft)
        {
            return EventErrors.NotDraft;
        }

        if (_ticketTypes.Count == 0)
        {
            return EventErrors.NoTicketTypes;
        }

        if (StartsAtUtc <= utcNow)
        {
            return EventErrors.StartsInPast;
        }

        Status = EventStatus.Published;
        PublishedAtUtc = utcNow;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == EventStatus.Cancelled)
        {
            return EventErrors.AlreadyCancelled;
        }

        Status = EventStatus.Cancelled;
        return Result.Success();
    }
}

public sealed class TicketType
{
    internal TicketType(Guid id, Guid eventId, string name, decimal price, string currency, int capacity)
    {
        Id = id;
        EventId = eventId;
        Name = name;
        Price = price;
        Currency = currency;
        Capacity = capacity;
    }

    private TicketType()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Capacity { get; private set; }
}

public enum EventStatus
{
    Draft = 0,
    Published = 1,
    Cancelled = 2
}

public static class EventErrors
{
    public static readonly Error StartsInPast = Error.Validation("Event.StartsInPast", "The event must start in the future.");

    public static readonly Error NotDraft = Error.Conflict("Event.NotDraft", "Only draft events can be changed or published.");

    public static readonly Error NoTicketTypes = Error.Validation("Event.NoTicketTypes", "An event needs at least one ticket type before it can be published.");

    public static readonly Error AlreadyCancelled = Error.Conflict("Event.AlreadyCancelled", "The event is already cancelled.");

    public static Error DuplicateTicketType(string name) =>
        Error.Conflict("Event.DuplicateTicketType", $"A ticket type named '{name}' already exists for this event.");

    public static Error NotFound(Guid id) => Error.NotFound("Event.NotFound", $"Event '{id}' was not found.");
}
