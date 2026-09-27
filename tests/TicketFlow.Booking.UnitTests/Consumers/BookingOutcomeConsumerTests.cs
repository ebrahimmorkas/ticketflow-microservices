using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Booking.Api.Consumers;
using TicketFlow.Booking.Api.Data;
using TicketFlow.Booking.Api.Domain;
using TicketFlow.Booking.UnitTests.Domain;
using TicketFlow.Contracts;
using BookingEntity = TicketFlow.Booking.Api.Domain.Booking;

namespace TicketFlow.Booking.UnitTests.Consumers;

public class BookingOutcomeConsumerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(ServiceProvider Provider, BookingEntity Booking, TicketInventory Inventory)> ArrangeAsync()
    {
        var provider = await TestHost.StartAsync(bus =>
        {
            bus.AddConsumer<BookingConfirmedConsumer>();
            bus.AddConsumer<BookingCancelledConsumer>();
        });

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var inventory = BookingDomainTests.CreateInventory(capacity: 10);
        inventory.Reserve(4);
        var booking = BookingEntity.Create(inventory, "fan@example.com", 4, DateTime.UtcNow);
        dbContext.AddRange(inventory, booking);
        await dbContext.SaveChangesAsync(Ct);

        return (provider, booking, inventory);
    }

    private static async Task<(BookingEntity Booking, TicketInventory Inventory)> ReloadAsync(ServiceProvider provider, Guid bookingId)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var booking = await dbContext.Bookings.SingleAsync(b => b.Id == bookingId, Ct);
        var inventory = await dbContext.Inventory.SingleAsync(i => i.TicketTypeId == booking.TicketTypeId, Ct);
        return (booking, inventory);
    }

    [Fact]
    public async Task BookingConfirmed_Should_Confirm_Booking()
    {
        var (provider, booking, _) = await ArrangeAsync();
        await using var _ = provider;
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Bus.Publish(new BookingConfirmed(booking.Id, booking.EventName, booking.CustomerEmail, 4, booking.Total, "USD"), Ct);
        await harness.InactivityTask;

        (await ReloadAsync(provider, booking.Id)).Booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task BookingCancelled_Should_Cancel_And_Release_Seats()
    {
        var (provider, booking, _) = await ArrangeAsync();
        await using var _ = provider;
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Bus.Publish(new BookingCancelled(booking.Id, booking.EventName, booking.CustomerEmail, "Payment failed"), Ct);
        await harness.InactivityTask;

        var (reloaded, inventory) = await ReloadAsync(provider, booking.Id);
        reloaded.Status.ShouldBe(BookingStatus.Cancelled);
        inventory.Available.ShouldBe(10);
    }

    [Fact]
    public async Task Duplicate_BookingCancelled_Should_Release_Seats_Only_Once()
    {
        var (provider, booking, _) = await ArrangeAsync();
        await using var _ = provider;
        var harness = provider.GetRequiredService<ITestHarness>();

        // Another booking holds seats too, so a double release would be visible.
        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var inventory = await dbContext.Inventory.SingleAsync(i => i.TicketTypeId == booking.TicketTypeId, Ct);
            inventory.Reserve(3);
            await dbContext.SaveChangesAsync(Ct);
        }

        var message = new BookingCancelled(booking.Id, booking.EventName, booking.CustomerEmail, "Payment failed");
        await harness.Bus.Publish(message, Ct);
        await harness.InactivityTask;
        await harness.Bus.Publish(message, Ct);
        await harness.InactivityTask;

        (await ReloadAsync(provider, booking.Id)).Inventory.Available.ShouldBe(7);
    }
}
