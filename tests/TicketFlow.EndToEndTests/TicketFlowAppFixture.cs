using Microsoft.Extensions.DependencyInjection;

namespace TicketFlow.EndToEndTests;

/// <summary>
/// Starts the whole distributed application (PostgreSQL, RabbitMQ, MailPit, every service and the
/// gateway) once for all end-to-end tests, exactly as the Aspire AppHost runs it locally.
/// </summary>
public sealed class TicketFlowAppFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private static readonly string[] Resources = ["events-api", "booking-api", "payments-api", "gateway"];

    private DistributedApplication? _app;

    /// <summary>Docker is required; runs in CI or when RUN_E2E=true.</summary>
    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("RUN_E2E"), "true", StringComparison.OrdinalIgnoreCase);

    public HttpClient Gateway { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        using var cts = new CancellationTokenSource(StartupTimeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TicketFlow_AppHost>(cts.Token);
        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        _app = await builder.BuildAsync(cts.Token);
        await _app.StartAsync(cts.Token);

        foreach (var resource in Resources)
        {
            await _app.ResourceNotifications.WaitForResourceHealthyAsync(resource, cts.Token);
        }

        await _app.ResourceNotifications.WaitForResourceAsync("notifications-worker", KnownResourceStates.Running, cts.Token);

        Gateway = _app.CreateHttpClient("gateway");
    }

    public async ValueTask DisposeAsync()
    {
        Gateway?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class EndToEndCollection : ICollectionFixture<TicketFlowAppFixture>
{
    public const string Name = "EndToEnd";
}
