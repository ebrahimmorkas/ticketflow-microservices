using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Booking.Api.Saga;
using TicketFlow.Contracts;

namespace TicketFlow.Booking.UnitTests.Saga;

public sealed class BookingStateMachineTests : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ITestHarness _harness;
    private readonly ISagaStateMachineTestHarness<BookingStateMachine, BookingState> _saga;

    public BookingStateMachineTests()
    {
        _provider = new ServiceCollection()
            .AddMassTransitTestHarness(bus => bus.AddSagaStateMachine<BookingStateMachine, BookingState>().InMemoryRepository())
            .BuildServiceProvider(validateScopes: true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        _saga = _harness.GetSagaStateMachineHarness<BookingStateMachine, BookingState>();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static BookingCreated CreateBooking(Guid bookingId) =>
        new(bookingId, Guid.NewGuid(), "Rock Night", "fan@example.com", 2, 100m, "USD");

    [Fact]
    public async Task BookingCreated_Should_Request_Payment_And_Await_Result()
    {
        await _harness.Start();
        var bookingId = Guid.NewGuid();

        await _harness.Bus.Publish(CreateBooking(bookingId), Ct);

        (await _saga.Created.Any(x => x.CorrelationId == bookingId, Ct)).ShouldBeTrue();
        (await _saga.Exists(bookingId, x => x.AwaitingPayment, TimeSpan.FromSeconds(5))).ShouldNotBeNull();

        var request = await _harness.Published.SelectAsync<ProcessPayment>(Ct).FirstOrDefault();
        request.ShouldNotBeNull();
        request.Context.Message.BookingId.ShouldBe(bookingId);
        request.Context.Message.Amount.ShouldBe(100m);
    }

    [Fact]
    public async Task PaymentSucceeded_Should_Confirm_Booking_And_Complete_Saga()
    {
        await _harness.Start();
        var bookingId = Guid.NewGuid();
        await _harness.Bus.Publish(CreateBooking(bookingId), Ct);
        await _saga.Exists(bookingId, x => x.AwaitingPayment, TimeSpan.FromSeconds(5));

        await _harness.Bus.Publish(new PaymentSucceeded(bookingId, Guid.NewGuid()), Ct);

        (await _harness.Published.Any<BookingConfirmed>(x => x.Context.Message.BookingId == bookingId, Ct)).ShouldBeTrue();
        (await _saga.NotExists(bookingId, TimeSpan.FromSeconds(5))).ShouldBeNull();
    }

    [Fact]
    public async Task PaymentFailed_Should_Cancel_Booking_With_Reason()
    {
        await _harness.Start();
        var bookingId = Guid.NewGuid();
        await _harness.Bus.Publish(CreateBooking(bookingId), Ct);
        await _saga.Exists(bookingId, x => x.AwaitingPayment, TimeSpan.FromSeconds(5));

        await _harness.Bus.Publish(new PaymentFailed(bookingId, "Card declined"), Ct);

        var cancelled = await _harness.Published.SelectAsync<BookingCancelled>(Ct).FirstOrDefault();
        cancelled.ShouldNotBeNull();
        cancelled.Context.Message.Reason.ShouldBe("Payment failed: Card declined");
        (await _harness.Published.Any<BookingConfirmed>(Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Payment_Result_For_Unknown_Booking_Should_Not_Create_Saga()
    {
        await _harness.Start();
        var bookingId = Guid.NewGuid();

        await _harness.Bus.Publish(new PaymentSucceeded(bookingId, Guid.NewGuid()), Ct);
        await _harness.InactivityTask;

        (await _saga.Created.Any(x => x.CorrelationId == bookingId, Ct)).ShouldBeFalse();
        (await _harness.Published.Any<BookingConfirmed>(Ct)).ShouldBeFalse();
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
}
