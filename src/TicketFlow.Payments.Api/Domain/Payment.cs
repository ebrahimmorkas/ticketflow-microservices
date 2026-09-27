namespace TicketFlow.Payments.Api.Domain;

public sealed class Payment
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>One payment per booking; enforced by a unique index to make processing idempotent.</summary>
    public Guid BookingId { get; private set; }

    public string CustomerEmail { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public PaymentStatus Status { get; private set; }

    public string? GatewayReference { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime ProcessedAtUtc { get; private set; }

    public static Payment Succeeded(Guid bookingId, string customerEmail, decimal amount, string currency, string reference, DateTime utcNow) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            BookingId = bookingId,
            CustomerEmail = customerEmail,
            Amount = amount,
            Currency = currency,
            Status = PaymentStatus.Succeeded,
            GatewayReference = reference,
            ProcessedAtUtc = utcNow
        };

    public static Payment Failed(Guid bookingId, string customerEmail, decimal amount, string currency, string reason, DateTime utcNow) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            BookingId = bookingId,
            CustomerEmail = customerEmail,
            Amount = amount,
            Currency = currency,
            Status = PaymentStatus.Failed,
            FailureReason = reason,
            ProcessedAtUtc = utcNow
        };
}

public enum PaymentStatus
{
    Succeeded = 0,
    Failed = 1
}
