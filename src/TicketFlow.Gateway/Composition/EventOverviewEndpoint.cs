using System.Net;

namespace TicketFlow.Gateway.Composition;

/// <summary>
/// API composition: clients get event details (Events service) and live availability (Booking service)
/// in one round trip. Both downstream calls run in parallel; if availability is temporarily unavailable
/// the event is still returned, flagged as degraded, instead of failing the whole request.
/// </summary>
public static class EventOverviewEndpoint
{
    public const string EventsClient = "events";
    public const string BookingClient = "booking";

    public sealed record EventOverview(EventDetails Event, IReadOnlyList<Availability>? Availability, bool AvailabilityDegraded);

    public sealed record EventDetails(Guid Id, string Name, string Description, string Venue, DateTime StartsAtUtc, string Status);

    public sealed record Availability(Guid TicketTypeId, string TicketTypeName, decimal Price, string Currency, int Available, bool OnSale);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/events/{id:guid}/overview", Handle)
            .WithName("GetEventOverview")
            .WithSummary("Event details combined with live ticket availability")
            .WithTags("Composition");

    internal static async Task<IResult> Handle(Guid id, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var eventsClient = httpClientFactory.CreateClient(EventsClient);
        var bookingClient = httpClientFactory.CreateClient(BookingClient);

        var eventTask = eventsClient.GetAsync($"/api/events/{id}", cancellationToken);
        var availabilityTask = GetAvailabilityAsync(bookingClient, id, cancellationToken);

        using var eventResponse = await eventTask;
        if (eventResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return TypedResults.Problem($"Event '{id}' was not found.", title: "Event.NotFound", statusCode: StatusCodes.Status404NotFound);
        }

        eventResponse.EnsureSuccessStatusCode();
        var details = await eventResponse.Content.ReadFromJsonAsync<EventDetails>(cancellationToken);
        var availability = await availabilityTask;

        return TypedResults.Ok(new EventOverview(details!, availability, availability is null));
    }

    private static async Task<IReadOnlyList<Availability>?> GetAvailabilityAsync(HttpClient client, Guid eventId, CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetFromJsonAsync<List<Availability>>($"/api/events/{eventId}/availability", cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
