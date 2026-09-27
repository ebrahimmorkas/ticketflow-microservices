using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Booking.Api.Data;
using TicketFlow.Booking.Api.Features;
using TicketFlow.Booking.UnitTests.Domain;
using TicketFlow.Contracts;

namespace TicketFlow.Booking.UnitTests.Features;

public class CreateBookingTests
{
    [Fact]
    public async Task Should_Reserve_Tickets_And_Publish_BookingCreated()
    {
        await using var provider = await TestHost.StartAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var inventory = BookingDomainTests.CreateInventory(capacity: 5, price: 40m);
        dbContext.Inventory.Add(inventory);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateBooking.ReserveAsync(
            new CreateBooking.Request(inventory.TicketTypeId, 2, "fan@example.com"),
            dbContext,
            scope.ServiceProvider.GetRequiredService<IPublishEndpoint>(),
            TimeProvider.System,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        inventory.Available.ShouldBe(3);

        var published = await harness.Published.SelectAsync<BookingCreated>(TestContext.Current.CancellationToken).FirstOrDefault();
        published.ShouldNotBeNull();
        published.Context.Message.BookingId.ShouldBe(result.Value.Id);
        published.Context.Message.Amount.ShouldBe(80m);
    }

    [Fact]
    public async Task Should_Return_SoldOut_And_Not_Publish_When_Insufficient()
    {
        await using var provider = await TestHost.StartAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var inventory = BookingDomainTests.CreateInventory(capacity: 1);
        dbContext.Inventory.Add(inventory);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateBooking.ReserveAsync(
            new CreateBooking.Request(inventory.TicketTypeId, 2, "fan@example.com"),
            dbContext,
            scope.ServiceProvider.GetRequiredService<IPublishEndpoint>(),
            TimeProvider.System,
            TestContext.Current.CancellationToken);

        result.Error!.Code.ShouldBe("Booking.SoldOut");
        (await harness.Published.Any<BookingCreated>(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Return_NotFound_For_Unknown_Ticket_Type()
    {
        await using var provider = await TestHost.StartAsync();
        await using var scope = provider.CreateAsyncScope();

        var result = await CreateBooking.ReserveAsync(
            new CreateBooking.Request(Guid.NewGuid(), 1, "fan@example.com"),
            scope.ServiceProvider.GetRequiredService<BookingDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPublishEndpoint>(),
            TimeProvider.System,
            TestContext.Current.CancellationToken);

        result.Error!.Code.ShouldBe("Booking.TicketTypeNotFound");
    }
}
