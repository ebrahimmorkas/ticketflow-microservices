using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Booking.Api.Data;

namespace TicketFlow.Booking.UnitTests;

/// <summary>
/// Builds a service provider with an in-memory database and MassTransit's in-memory test harness,
/// so consumers and handlers can be exercised without RabbitMQ or PostgreSQL.
/// </summary>
internal static class TestHost
{
    public static async Task<ServiceProvider> StartAsync(Action<IBusRegistrationConfigurator>? configure = null)
    {
        var databaseName = Guid.NewGuid().ToString();

        var provider = new ServiceCollection()
            .AddDbContext<BookingDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddSingleton(TimeProvider.System)
            .AddMassTransitTestHarness(bus => configure?.Invoke(bus))
            .BuildServiceProvider(validateScopes: true);

        await provider.GetRequiredService<ITestHarness>().Start();
        return provider;
    }
}
