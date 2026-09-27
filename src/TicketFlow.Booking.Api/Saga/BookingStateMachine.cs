using MassTransit;
using TicketFlow.Contracts;

namespace TicketFlow.Booking.Api.Saga;

/// <summary>
/// Orchestrates the checkout of a booking across services:
/// <code>
///   BookingCreated ──► [AwaitingPayment] ──PaymentSucceeded──► publish BookingConfirmed ──► (final)
///                                     └───PaymentFailed────► publish BookingCancelled ──► (final)
/// </code>
/// Compensation (releasing the reserved seats) happens in <c>BookingCancelledConsumer</c>.
/// </summary>
public sealed class BookingStateMachine : MassTransitStateMachine<BookingState>
{
    public BookingStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => BookingCreated, e => e.CorrelateById(context => context.Message.BookingId));
        // Late or duplicate payment results for a completed saga are discarded instead of faulting.
        Event(() => PaymentSucceeded, e =>
        {
            e.CorrelateById(context => context.Message.BookingId);
            e.OnMissingInstance(m => m.Discard());
        });
        Event(() => PaymentFailed, e =>
        {
            e.CorrelateById(context => context.Message.BookingId);
            e.OnMissingInstance(m => m.Discard());
        });

        Initially(
            When(BookingCreated)
                .Then(context =>
                {
                    var message = context.Message;
                    context.Saga.EventName = message.EventName;
                    context.Saga.CustomerEmail = message.CustomerEmail;
                    context.Saga.Quantity = message.Quantity;
                    context.Saga.Amount = message.Amount;
                    context.Saga.Currency = message.Currency;
                    context.Saga.StartedAtUtc = DateTime.UtcNow;
                })
                .Publish(context => new ProcessPayment(
                    context.Saga.CorrelationId,
                    context.Saga.CustomerEmail,
                    context.Saga.Amount,
                    context.Saga.Currency))
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When(PaymentSucceeded)
                .Publish(context => new BookingConfirmed(
                    context.Saga.CorrelationId,
                    context.Saga.EventName,
                    context.Saga.CustomerEmail,
                    context.Saga.Quantity,
                    context.Saga.Amount,
                    context.Saga.Currency))
                .Finalize(),
            When(PaymentFailed)
                .Publish(context => new BookingCancelled(
                    context.Saga.CorrelationId,
                    context.Saga.EventName,
                    context.Saga.CustomerEmail,
                    $"Payment failed: {context.Message.Reason}"))
                .Finalize());

        SetCompletedWhenFinalized();
    }

    public State AwaitingPayment { get; private set; } = null!;

    public Event<BookingCreated> BookingCreated { get; private set; } = null!;

    public Event<PaymentSucceeded> PaymentSucceeded { get; private set; } = null!;

    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;
}
