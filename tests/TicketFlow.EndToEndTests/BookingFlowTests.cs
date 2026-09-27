using System.Net;
using System.Net.Http.Json;

namespace TicketFlow.EndToEndTests;

/// <summary>
/// Drives complete business flows through the public gateway: HTTP → Events service → RabbitMQ →
/// Booking inventory → saga → Payments → saga → Booking status (+ Notifications).
/// </summary>
[Collection(EndToEndCollection.Name)]
public sealed class BookingFlowTests(TicketFlowAppFixture fixture)
{
    private static readonly TimeSpan EventualConsistencyTimeout = TimeSpan.FromSeconds(60);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient Gateway => fixture.Gateway;

    [Fact]
    public async Task Successful_Payment_Should_Confirm_Booking_And_Reduce_Availability()
    {
        Assert.SkipUnless(TicketFlowAppFixture.IsEnabled, "End-to-end tests need Docker; set RUN_E2E=true to run them.");

        var (eventId, ticketTypeId) = await CreatePublishedEventAsync(capacity: 5, price: 40m);

        var booking = await CreateBookingAsync(ticketTypeId, quantity: 2, email: "fan@example.com");
        var final = await WaitForBookingStatusAsync(booking.Id, "Confirmed");

        final.Total.ShouldBe(80m);
        (await GetAvailableAsync(eventId, ticketTypeId)).ShouldBe(3);

        var payment = await Gateway.GetFromJsonAsync<PaymentDto>($"/api/payments/{booking.Id}", Ct);
        payment!.Status.ShouldBe("Succeeded");
    }

    [Fact]
    public async Task Declined_Payment_Should_Cancel_Booking_And_Release_Seats()
    {
        Assert.SkipUnless(TicketFlowAppFixture.IsEnabled, "End-to-end tests need Docker; set RUN_E2E=true to run them.");

        var (eventId, ticketTypeId) = await CreatePublishedEventAsync(capacity: 4, price: 25m);

        var booking = await CreateBookingAsync(ticketTypeId, quantity: 3, email: "fan+decline@example.com");
        var final = await WaitForBookingStatusAsync(booking.Id, "Cancelled");

        final.CancellationReason.ShouldNotBeNull().ShouldContain("Payment failed");
        await WaitUntilAsync(async () => await GetAvailableAsync(eventId, ticketTypeId) == 4);
    }

    [Fact]
    public async Task Booking_More_Than_Available_Should_Be_Rejected()
    {
        Assert.SkipUnless(TicketFlowAppFixture.IsEnabled, "End-to-end tests need Docker; set RUN_E2E=true to run them.");

        var (_, ticketTypeId) = await CreatePublishedEventAsync(capacity: 1, price: 10m);

        var response = await Gateway.PostAsJsonAsync(
            "/api/bookings",
            new { ticketTypeId, quantity = 2, customerEmail = "fan@example.com" },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Overview_Should_Compose_Event_And_Availability()
    {
        Assert.SkipUnless(TicketFlowAppFixture.IsEnabled, "End-to-end tests need Docker; set RUN_E2E=true to run them.");

        var (eventId, _) = await CreatePublishedEventAsync(capacity: 7, price: 15m);

        var overview = await Gateway.GetFromJsonAsync<OverviewDto>($"/api/events/{eventId}/overview", Ct);

        overview!.Event.Id.ShouldBe(eventId);
        overview.AvailabilityDegraded.ShouldBeFalse();
        overview.Availability.ShouldNotBeNull().ShouldHaveSingleItem().Available.ShouldBe(7);
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> CreatePublishedEventAsync(int capacity, decimal price)
    {
        var create = await Gateway.PostAsJsonAsync(
            "/api/events",
            new
            {
                name = $"E2E Concert {Guid.NewGuid():N}",
                description = "End-to-end test event",
                venue = "Test Arena",
                startsAtUtc = DateTime.UtcNow.AddDays(30),
                ticketTypes = new[] { new { name = "General", price, currency = "USD", capacity } }
            },
            Ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var eventId = (await create.Content.ReadFromJsonAsync<IdDto>(Ct))!.Id;

        (await Gateway.PostAsync($"/api/events/{eventId}/publish", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Inventory reaches the Booking service asynchronously via RabbitMQ.
        List<AvailabilityDto>? availability = null;
        await WaitUntilAsync(async () =>
        {
            availability = await Gateway.GetFromJsonAsync<List<AvailabilityDto>>($"/api/events/{eventId}/availability", Ct);
            return availability is { Count: > 0 };
        });

        return (eventId, availability!.Single().TicketTypeId);
    }

    private async Task<BookingDto> CreateBookingAsync(Guid ticketTypeId, int quantity, string email)
    {
        var response = await Gateway.PostAsJsonAsync("/api/bookings", new { ticketTypeId, quantity, customerEmail = email }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var booking = (await response.Content.ReadFromJsonAsync<BookingDto>(Ct))!;
        booking.Status.ShouldBe("PendingPayment");
        return booking;
    }

    private async Task<BookingDto> WaitForBookingStatusAsync(Guid bookingId, string status)
    {
        BookingDto? booking = null;
        await WaitUntilAsync(async () =>
        {
            booking = await Gateway.GetFromJsonAsync<BookingDto>($"/api/bookings/{bookingId}", Ct);
            return booking!.Status == status;
        });
        return booking!;
    }

    private async Task<int> GetAvailableAsync(Guid eventId, Guid ticketTypeId)
    {
        var availability = await Gateway.GetFromJsonAsync<List<AvailabilityDto>>($"/api/events/{eventId}/availability", Ct);
        return availability!.Single(a => a.TicketTypeId == ticketTypeId).Available;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(EventualConsistencyTimeout);

        while (!await condition())
        {
            await Task.Delay(500, timeout.Token);
        }
    }

    private sealed record IdDto(Guid Id);

    private sealed record BookingDto(Guid Id, string Status, decimal Total, string? CancellationReason);

    private sealed record AvailabilityDto(Guid TicketTypeId, string TicketTypeName, int Available);

    private sealed record PaymentDto(Guid BookingId, string Status);

    private sealed record OverviewDto(EventDto Event, List<AvailabilityDto>? Availability, bool AvailabilityDegraded);

    private sealed record EventDto(Guid Id, string Name);
}
