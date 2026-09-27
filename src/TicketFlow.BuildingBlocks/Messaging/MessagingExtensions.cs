using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TicketFlow.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    /// <summary>Name of the RabbitMQ resource declared in the Aspire AppHost.</summary>
    public const string ConnectionName = "messaging";

    /// <summary>
    /// Configures MassTransit over RabbitMQ with the EF Core transactional outbox and inbox:
    /// messages are stored in the service database in the same transaction as business data and
    /// delivered afterwards, and incoming messages are de-duplicated so consumers are idempotent.
    /// </summary>
    public static IHostApplicationBuilder AddMessaging<TDbContext>(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configure = null)
        where TDbContext : DbContext
    {
        builder.Services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.AddEntityFrameworkOutbox<TDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
            });

            bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(retry => retry.Intervals(100, 500, 1_000, 5_000));
                endpoint.UseEntityFrameworkOutbox<TDbContext>(context);
            });

            configure?.Invoke(bus);

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(GetConnectionUri(builder));
                rabbit.ConfigureEndpoints(context);
            });
        });

        return builder;
    }

    /// <summary>
    /// Configures MassTransit over RabbitMQ for stateless services that own no database.
    /// </summary>
    public static IHostApplicationBuilder AddMessaging(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        builder.Services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.AddConfigureEndpointsCallback((_, _, endpoint) =>
                endpoint.UseMessageRetry(retry => retry.Intervals(100, 500, 1_000, 5_000)));

            configure?.Invoke(bus);

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(GetConnectionUri(builder));
                rabbit.ConfigureEndpoints(context);
            });
        });

        return builder;
    }

    private static Uri GetConnectionUri(IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionName);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"Connection string '{ConnectionName}' is not configured.")
            : new Uri(connectionString);
    }
}
