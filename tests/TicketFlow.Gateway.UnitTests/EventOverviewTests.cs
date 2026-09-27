using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Gateway.Composition;

namespace TicketFlow.Gateway.UnitTests;

public sealed class EventOverviewTests
{
    private static readonly Guid EventId = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly object EventJson = new
    {
        id = EventId,
        name = "Rock Night",
        description = "Live",
        venue = "Arena",
        startsAtUtc = DateTime.UtcNow.AddDays(10),
        status = "Published"
    };

    private static readonly object[] AvailabilityJson =
    [
        new { ticketTypeId = Guid.NewGuid(), ticketTypeName = "General", price = 30m, currency = "USD", available = 42, onSale = true }
    ];

    private static WebApplicationFactory<Program> CreateGateway(StubHandler events, StubHandler booking) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Service discovery needs an address for the logical service names.
            builder.UseSetting("services:events-api:http:0", "http://events.test");
            builder.UseSetting("services:booking-api:http:0", "http://booking.test");
            builder.UseSetting("services:payments-api:http:0", "http://payments.test");

            builder.ConfigureServices(services =>
            {
                services.AddHttpClient(EventOverviewEndpoint.EventsClient).ConfigurePrimaryHttpMessageHandler(() => events);
                services.AddHttpClient(EventOverviewEndpoint.BookingClient).ConfigurePrimaryHttpMessageHandler(() => booking);
            });
        });

    [Fact]
    public async Task Overview_Should_Combine_Event_And_Availability()
    {
        await using var gateway = CreateGateway(
            StubHandler.Json(HttpStatusCode.OK, EventJson),
            StubHandler.Json(HttpStatusCode.OK, AvailabilityJson));

        var overview = await gateway.CreateClient()
            .GetFromJsonAsync<EventOverviewEndpoint.EventOverview>($"/api/events/{EventId}/overview", Ct);

        overview!.Event.Name.ShouldBe("Rock Night");
        overview.Availability.ShouldNotBeNull().ShouldHaveSingleItem().Available.ShouldBe(42);
        overview.AvailabilityDegraded.ShouldBeFalse();
    }

    [Fact]
    public async Task Overview_Should_Degrade_Gracefully_When_Booking_Service_Is_Down()
    {
        await using var gateway = CreateGateway(
            StubHandler.Json(HttpStatusCode.OK, EventJson),
            StubHandler.Throws());

        var overview = await gateway.CreateClient()
            .GetFromJsonAsync<EventOverviewEndpoint.EventOverview>($"/api/events/{EventId}/overview", Ct);

        overview!.Event.Id.ShouldBe(EventId);
        overview.Availability.ShouldBeNull();
        overview.AvailabilityDegraded.ShouldBeTrue();
    }

    [Fact]
    public async Task Overview_Should_Return_NotFound_For_Unknown_Event()
    {
        await using var gateway = CreateGateway(
            StubHandler.Json(HttpStatusCode.NotFound, new { }),
            StubHandler.Json(HttpStatusCode.OK, Array.Empty<object>()));

        var response = await gateway.CreateClient().GetAsync($"/api/events/{EventId}/overview", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public static StubHandler Json(HttpStatusCode status, object body) =>
            new(_ => new HttpResponseMessage(status) { Content = JsonContent.Create(body) });

        public static StubHandler Throws() =>
            new(_ => throw new HttpRequestException("Connection refused"));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
