namespace TicketFlow.Contracts;

/// <summary>Command sent by the booking saga to the Payments service.</summary>
public sealed record ProcessPayment(Guid BookingId, string CustomerEmail, decimal Amount, string Currency);

public sealed record PaymentSucceeded(Guid BookingId, Guid PaymentId);

public sealed record PaymentFailed(Guid BookingId, string Reason);
