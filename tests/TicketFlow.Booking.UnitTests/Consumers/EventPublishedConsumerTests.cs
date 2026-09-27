using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Booking.Api.Consumers;
using TicketFlow.Booking.Api.Data;
using TicketFlow.Contracts;

namespace TicketFlow.Booking.UnitTests.Consumers;

public class EventPublishedConsumerTests
{
    private static EventPublished CreateMessage() => new(
        Guid.NewGuid(),
        "Jazz Evening",
        "Blue Hall",
        DateTime.UtcNow.AddDays(10),
        [
            new TicketTypeInfo(Guid.NewGuid(), "General", 30m, "USD", 200),
            new TicketTypeInfo(Guid.NewGuid(), "VIP", 120m, "USD", 20)
        ]);

    [Fact]
    public async Task Should_Create_Inventory_For_Each_Ticket_Type()
    {
        await using var provider = await TestHost.StartAsync(bus => bus.AddConsumer<EventPublishedConsumer>());
        var harness = provider.GetRequiredService<ITestHarness>();
        var message = CreateMessage();

        await harness.Bus.Publish(message, TestContext.Current.CancellationToken);

        (await harness.GetConsumerHarness<EventPublishedConsumer>().Consumed.Any<EventPublished>(TestContext.Current.CancellationToken)).ShouldBeTrue();

        await using var scope = provider.CreateAsyncScope();
        var inventory = await scope.ServiceProvider.GetRequiredService<BookingDbContext>()
            .Inventory.Where(i => i.EventId == message.EventId).ToListAsync(TestContext.Current.CancellationToken);

        inventory.Count.ShouldBe(2);
        inventory.Single(i => i.TicketTypeName == "VIP").Available.ShouldBe(20);
    }

    [Fact]
    public async Task Should_Be_Idempotent_When_Message_Is_Redelivered()
    {
        await using var provider = await TestHost.StartAsync(bus => bus.AddConsumer<EventPublishedConsumer>());
        var harness = provider.GetRequiredService<ITestHarness>();
        var consumer = harness.GetConsumerHarness<EventPublishedConsumer>();
        var message = CreateMessage();

        await harness.Bus.Publish(message, TestContext.Current.CancellationToken);
        await harness.Bus.Publish(message, TestContext.Current.CancellationToken);

        await harness.InactivityTask;
        consumer.Consumed.Select<EventPublished>(TestContext.Current.CancellationToken).Count().ShouldBe(2);

        await using var scope = provider.CreateAsyncScope();
        var count = await scope.ServiceProvider.GetRequiredService<BookingDbContext>()
            .Inventory.CountAsync(i => i.EventId == message.EventId, TestContext.Current.CancellationToken);

        count.ShouldBe(2);
    }
}
