namespace TicketFlow.Contracts;

/// <summary>Raised when seats have been reserved and the booking awaits payment. Starts the booking saga.</summary>
public sealed record BookingCreated(
    Guid BookingId,
    Guid EventId,
    string EventName,
    string CustomerEmail,
    int Quantity,
    decimal Amount,
    string Currency);

public sealed record BookingConfirmed(Guid BookingId, string EventName, string CustomerEmail, int Quantity, decimal Amount, string Currency);

public sealed record BookingCancelled(Guid BookingId, string EventName, string CustomerEmail, string Reason);
