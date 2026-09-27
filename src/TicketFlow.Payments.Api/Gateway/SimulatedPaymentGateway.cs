using Microsoft.Extensions.Options;

namespace TicketFlow.Payments.Api.Gateway;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    public int SimulatedLatencyMilliseconds { get; init; } = 300;

    public decimal MaxAmount { get; init; } = 5_000m;
}

/// <summary>
/// Deterministic fake gateway so the whole booking flow can be demoed and tested:
/// <list type="bullet">
/// <item>amounts above <see cref="PaymentGatewayOptions.MaxAmount"/> are declined</item>
/// <item>emails containing <c>+decline</c> are declined (e.g. <c>jane+decline@example.com</c>)</item>
/// </list>
/// </summary>
public sealed class SimulatedPaymentGateway(IOptions<PaymentGatewayOptions> options) : IPaymentGateway
{
    private readonly PaymentGatewayOptions _options = options.Value;

    public async Task<GatewayResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken)
    {
        if (_options.SimulatedLatencyMilliseconds > 0)
        {
            await Task.Delay(_options.SimulatedLatencyMilliseconds, cancellationToken);
        }

        if (request.Amount > _options.MaxAmount)
        {
            return GatewayResult.Decline($"Amount exceeds the {_options.MaxAmount:0.00} {request.Currency} limit.");
        }

        if (request.CustomerEmail.Contains("+decline", StringComparison.OrdinalIgnoreCase))
        {
            return GatewayResult.Decline("Card declined by issuer.");
        }

        return GatewayResult.Approve($"sim_{request.IdempotencyKey:N}");
    }
}
