using Microsoft.EntityFrameworkCore;
using TicketFlow.BuildingBlocks.Results;
using TicketFlow.Payments.Api.Data;

namespace TicketFlow.Payments.Api.Features;

public sealed record PaymentResponse(
    Guid Id,
    Guid BookingId,
    decimal Amount,
    string Currency,
    string Status,
    string? GatewayReference,
    string? FailureReason,
    DateTime ProcessedAtUtc);

public static class PaymentQueries
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/payments/{bookingId:guid}", GetByBooking)
            .WithName("GetPaymentByBooking")
            .WithSummary("Gets the payment outcome for a booking")
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    internal static async Task<IResult> GetByBooking(Guid bookingId, PaymentsDbContext dbContext, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .AsNoTracking()
            .Where(p => p.BookingId == bookingId)
            .Select(p => new PaymentResponse(
                p.Id, p.BookingId, p.Amount, p.Currency, p.Status.ToString(), p.GatewayReference, p.FailureReason, p.ProcessedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return payment is null
            ? Error.NotFound("Payment.NotFound", $"No payment found for booking '{bookingId}'.").ToProblem()
            : TypedResults.Ok(payment);
    }
}
