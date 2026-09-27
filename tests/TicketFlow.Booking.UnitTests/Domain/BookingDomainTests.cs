using TicketFlow.Booking.Api.Domain;
using BookingEntity = TicketFlow.Booking.Api.Domain.Booking;

namespace TicketFlow.Booking.UnitTests.Domain;

public class BookingDomainTests
{
    private static readonly DateTime UtcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    internal static TicketInventory CreateInventory(int capacity = 10, decimal price = 50m) =>
        TicketInventory.Create(Guid.NewGuid(), Guid.NewGuid(), "Rock Night", "General", price, "USD", capacity);

    [Fact]
    public void Reserve_Should_Reduce_Availability()
    {
        var inventory = CreateInventory(capacity: 10);

        inventory.Reserve(3).IsSuccess.ShouldBeTrue();

        inventory.Available.ShouldBe(7);
    }

    [Fact]
    public void Reserve_Should_Fail_When_Not_Enough_Tickets_Remain()
    {
        var inventory = CreateInventory(capacity: 2);

        var result = inventory.Reserve(3);

        result.Error!.Code.ShouldBe("Booking.SoldOut");
        inventory.Available.ShouldBe(2);
    }

    [Fact]
    public void Reserve_Should_Fail_When_Sales_Stopped()
    {
        var inventory = CreateInventory();
        inventory.StopSales();

        inventory.Reserve(1).Error!.Code.ShouldBe("Booking.NotOnSale");
    }

    [Fact]
    public void Release_Should_Never_Go_Below_Zero()
    {
        var inventory = CreateInventory(capacity: 5);
        inventory.Reserve(2);

        inventory.Release(10);

        inventory.Available.ShouldBe(5);
    }

    [Fact]
    public void Booking_Should_Snapshot_Price_And_Normalize_Email()
    {
        var inventory = CreateInventory(price: 75m);

        var booking = BookingEntity.Create(inventory, "  Jane@Example.COM ", 2, UtcNow);

        booking.Total.ShouldBe(150m);
        booking.CustomerEmail.ShouldBe("jane@example.com");
        booking.Status.ShouldBe(BookingStatus.PendingPayment);
    }

    [Fact]
    public void Confirm_Should_Only_Work_For_Pending_Bookings()
    {
        var booking = BookingEntity.Create(CreateInventory(), "a@b.com", 1, UtcNow);
        booking.Cancel("payment failed", UtcNow);

        booking.Confirm(UtcNow).Error!.Code.ShouldBe("Booking.InvalidTransition");
    }

    [Fact]
    public void Cancel_Should_Record_Reason()
    {
        var booking = BookingEntity.Create(CreateInventory(), "a@b.com", 1, UtcNow);

        booking.Cancel("Card declined", UtcNow).IsSuccess.ShouldBeTrue();

        booking.Status.ShouldBe(BookingStatus.Cancelled);
        booking.CancellationReason.ShouldBe("Card declined");
        booking.Cancel("again", UtcNow).IsFailure.ShouldBeTrue();
    }
}
