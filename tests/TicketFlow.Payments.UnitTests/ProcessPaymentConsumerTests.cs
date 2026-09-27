using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TicketFlow.Contracts;
using TicketFlow.Payments.Api.Consumers;
using TicketFlow.Payments.Api.Data;
using TicketFlow.Payments.Api.Domain;
using TicketFlow.Payments.Api.Gateway;

namespace TicketFlow.Payments.UnitTests;

public sealed class ProcessPaymentConsumerTests : IAsyncDisposable
{
    private readonly CountingGateway _gateway = new();
    private readonly ServiceProvider _provider;
    private readonly ITestHarness _harness;

    public ProcessPaymentConsumerTests()
    {
        var databaseName = Guid.NewGuid().ToString();

        _provider = new ServiceCollection()
            .AddDbContext<PaymentsDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IPaymentGateway>(_gateway)
            .AddLogging()
            .AddMassTransitTestHarness(bus => bus.AddConsumer<ProcessPaymentConsumer>())
            .BuildServiceProvider(validateScopes: true);

        _harness = _provider.GetRequiredService<ITestHarness>();
    }

    [Fact]
    public async Task Approved_Charge_Should_Store_Payment_And_Publish_PaymentSucceeded()
    {
        await _harness.Start();
        var bookingId = Guid.NewGuid();

        await _harness.Bus.Publish(new ProcessPayment(bookingId, "fan@example.com", 80m, "USD"), TestContext.Current.CancellationToken);

        (await _harness.Published.Any<PaymentSucceeded>(x => x.Context.Message.BookingId == bookingId, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await WaitForConsumerAsync();
        (await GetPaymentsAsync(bookingId)).ShouldHaveSingleItem().Status.ShouldBe(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task Declined_Charge_Should_Publish_PaymentFailed_With_Reason()
    {
        await _harness.Start();
        var bookingId = Guid.NewGuid();

        await _harness.Bus.Publish(new ProcessPayment(bookingId, "fan+decline@example.com", 80m, "USD"), TestContext.Current.CancellationToken);

        (await _harness.Published.Any<PaymentFailed>(x => x.Context.Message.BookingId == bookingId, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await WaitForConsumerAsync();
        var payment = (await GetPaymentsAsync(bookingId)).ShouldHaveSingleItem();
        payment.Status.ShouldBe(PaymentStatus.Failed);
        payment.FailureReason.ShouldBe("Card declined by issuer.");
    }

    [Fact]
    public async Task Redelivered_Command_Should_Not_Charge_Twice()
    {
        await _harness.Start();
        var bookingId = Guid.NewGuid();
        var command = new ProcessPayment(bookingId, "fan@example.com", 80m, "USD");

        await _harness.Bus.Publish(command, TestContext.Current.CancellationToken);
        await _harness.InactivityTask;
        await _harness.Bus.Publish(command, TestContext.Current.CancellationToken);
        await _harness.InactivityTask;

        _gateway.Calls.ShouldBe(1);
        (await GetPaymentsAsync(bookingId)).Count.ShouldBe(1);
        _harness.Published.Select<PaymentSucceeded>(TestContext.Current.CancellationToken).Count().ShouldBe(2);
    }

    [Theory]
    [InlineData(5000.00, "fan@example.com", true)]
    [InlineData(5000.01, "fan@example.com", false)]
    [InlineData(10.00, "FAN+DECLINE@example.com", false)]
    public async Task SimulatedGateway_Should_Apply_Decline_Rules(decimal amount, string email, bool approved)
    {
        var gateway = new SimulatedPaymentGateway(Options.Create(new PaymentGatewayOptions { SimulatedLatencyMilliseconds = 0 }));

        var result = await gateway.ChargeAsync(new ChargeRequest(Guid.NewGuid(), email, amount, "USD"), TestContext.Current.CancellationToken);

        result.Approved.ShouldBe(approved);
    }

    // The harness records a Publish as soon as it happens, which (without the outbox used in production)
    // is before SaveChanges. Waiting for the consume to finish avoids reading the database too early.
    private async Task WaitForConsumerAsync() =>
        (await _harness.GetConsumerHarness<ProcessPaymentConsumer>().Consumed.Any<ProcessPayment>(TestContext.Current.CancellationToken)).ShouldBeTrue();

    private async Task<List<Payment>> GetPaymentsAsync(Guid bookingId)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Payments.Where(p => p.BookingId == bookingId).ToListAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private sealed class CountingGateway : IPaymentGateway
    {
        private readonly SimulatedPaymentGateway _inner =
            new(Options.Create(new PaymentGatewayOptions { SimulatedLatencyMilliseconds = 0 }));

        private int _calls;

        public int Calls => _calls;

        public Task<GatewayResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return _inner.ChargeAsync(request, cancellationToken);
        }
    }
}
