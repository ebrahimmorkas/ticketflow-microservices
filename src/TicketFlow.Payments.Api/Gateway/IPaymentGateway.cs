namespace TicketFlow.Payments.Api.Gateway;

/// <summary>
/// Abstraction over an external payment provider (Stripe, Adyen, ...). Swapping the simulated
/// implementation for a real one requires no change to the consumer.
/// </summary>
public interface IPaymentGateway
{
    Task<GatewayResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken);
}

public sealed record ChargeRequest(Guid IdempotencyKey, string CustomerEmail, decimal Amount, string Currency);

public sealed record GatewayResult(bool Approved, string? Reference, string? DeclineReason)
{
    public static GatewayResult Approve(string reference) => new(true, reference, null);

    public static GatewayResult Decline(string reason) => new(false, null, reason);
}
