using TicketFlow.BuildingBlocks.Results;

namespace TicketFlow.Booking.Api.Domain;

/// <summary>
/// Local read/write model of ticket availability, built from <c>EventPublished</c> messages.
/// The Booking service owns reservations, so it doesn't need to call the Events service to sell tickets.
/// </summary>
public sealed class TicketInventory
{
    private TicketInventory()
    {
    }

    public Guid TicketTypeId { get; private set; }

    public Guid EventId { get; private set; }

    public string EventName { get; private set; } = string.Empty;

    public string TicketTypeName { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Capacity { get; private set; }

    public int Reserved { get; private set; }

    public bool IsOnSale { get; private set; }

    public int Available => Capacity - Reserved;

    public static TicketInventory Create(
        Guid ticketTypeId,
        Guid eventId,
        string eventName,
        string ticketTypeName,
        decimal price,
        string currency,
        int capacity) => new()
    {
        TicketTypeId = ticketTypeId,
        EventId = eventId,
        EventName = eventName,
        TicketTypeName = ticketTypeName,
        Price = price,
        Currency = currency,
        Capacity = capacity,
        IsOnSale = true
    };

    public Result Reserve(int quantity)
    {
        if (!IsOnSale)
        {
            return BookingErrors.NotOnSale(TicketTypeId);
        }

        if (quantity > Available)
        {
            return BookingErrors.SoldOut(TicketTypeId, Available, quantity);
        }

        Reserved += quantity;
        return Result.Success();
    }

    public void Release(int quantity) => Reserved = Math.Max(0, Reserved - quantity);

    public void StopSales() => IsOnSale = false;
}
