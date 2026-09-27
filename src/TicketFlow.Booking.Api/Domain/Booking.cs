using TicketFlow.BuildingBlocks.Results;

namespace TicketFlow.Booking.Api.Domain;

public sealed class Booking
{
    private Booking()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public Guid TicketTypeId { get; private set; }

    public string EventName { get; private set; } = string.Empty;

    public string CustomerEmail { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal Total => UnitPrice * Quantity;

    public BookingStatus Status { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public static Booking Create(TicketInventory inventory, string customerEmail, int quantity, DateTime utcNow) => new()
    {
        Id = Guid.CreateVersion7(),
        EventId = inventory.EventId,
        TicketTypeId = inventory.TicketTypeId,
        EventName = inventory.EventName,
        CustomerEmail = customerEmail.Trim().ToLowerInvariant(),
        Quantity = quantity,
        UnitPrice = inventory.Price,
        Currency = inventory.Currency,
        Status = BookingStatus.PendingPayment,
        CreatedAtUtc = utcNow
    };

    public Result Confirm(DateTime utcNow)
    {
        if (Status != BookingStatus.PendingPayment)
        {
            return BookingErrors.InvalidTransition(Status, BookingStatus.Confirmed);
        }

        Status = BookingStatus.Confirmed;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public Result Cancel(string reason, DateTime utcNow)
    {
        if (Status == BookingStatus.Cancelled)
        {
            return BookingErrors.InvalidTransition(Status, BookingStatus.Cancelled);
        }

        Status = BookingStatus.Cancelled;
        CancellationReason = reason;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }
}

public enum BookingStatus
{
    PendingPayment = 0,
    Confirmed = 1,
    Cancelled = 2
}
