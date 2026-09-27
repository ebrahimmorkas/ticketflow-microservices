using MassTransit;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Contracts;
using TicketFlow.Payments.Api.Data;
using TicketFlow.Payments.Api.Domain;
using TicketFlow.Payments.Api.Gateway;

namespace TicketFlow.Payments.Api.Consumers;

/// <summary>
/// Charges the customer for a booking. Business-level idempotency: if the booking was already
/// processed, the stored outcome is re-published instead of charging the card a second time.
/// </summary>
public sealed partial class ProcessPaymentConsumer(
    PaymentsDbContext dbContext,
    IPaymentGateway gateway,
    TimeProvider timeProvider,
    ILogger<ProcessPaymentConsumer> logger)
    : IConsumer<ProcessPayment>
{
    public async Task Consume(ConsumeContext<ProcessPayment> context)
    {
        var command = context.Message;

        var existing = await dbContext.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.BookingId == command.BookingId, context.CancellationToken);

        if (existing is not null)
        {
            LogDuplicate(logger, command.BookingId);
            await PublishOutcome(context, existing);
            return;
        }

        var result = await gateway.ChargeAsync(
            new ChargeRequest(command.BookingId, command.CustomerEmail, command.Amount, command.Currency),
            context.CancellationToken);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var payment = result.Approved
            ? Payment.Succeeded(command.BookingId, command.CustomerEmail, command.Amount, command.Currency, result.Reference!, utcNow)
            : Payment.Failed(command.BookingId, command.CustomerEmail, command.Amount, command.Currency, result.DeclineReason!, utcNow);

        dbContext.Payments.Add(payment);
        await PublishOutcome(context, payment);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        LogProcessed(logger, command.BookingId, payment.Status, command.Amount, command.Currency);
    }

    private static Task PublishOutcome(ConsumeContext context, Payment payment) =>
        payment.Status == PaymentStatus.Succeeded
            ? context.Publish(new PaymentSucceeded(payment.BookingId, payment.Id), context.CancellationToken)
            : context.Publish(new PaymentFailed(payment.BookingId, payment.FailureReason!), context.CancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Payment for booking {BookingId} {Status}: {Amount} {Currency}")]
    private static partial void LogProcessed(ILogger logger, Guid bookingId, PaymentStatus status, decimal amount, string currency);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment for booking {BookingId} was already processed; re-publishing outcome")]
    private static partial void LogDuplicate(ILogger logger, Guid bookingId);
}
