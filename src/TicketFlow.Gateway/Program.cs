using System.Threading.RateLimiting;
using TicketFlow.Gateway.Composition;
using Yarp.ReverseProxy.Transforms;

const string CorrelationHeader = "X-Correlation-Id";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .AddTransforms(transforms =>
    {
        // Propagate (or create) a correlation id so a request can be followed across services and logs.
        transforms.AddRequestTransform(context =>
        {
            var correlationId = context.HttpContext.Request.Headers[CorrelationHeader].FirstOrDefault()
                ?? context.HttpContext.TraceIdentifier;
            context.ProxyRequest.Headers.Remove(CorrelationHeader);
            context.ProxyRequest.Headers.Add(CorrelationHeader, correlationId);
            context.HttpContext.Response.Headers[CorrelationHeader] = correlationId;
            return ValueTask.CompletedTask;
        });
    });

builder.Services.AddHttpClient(EventOverviewEndpoint.EventsClient, c => c.BaseAddress = new Uri("https+http://events-api"));
builder.Services.AddHttpClient(EventOverviewEndpoint.BookingClient, c => c.BaseAddress = new Uri("https+http://booking-api"));

var permitsPerMinute = builder.Configuration.GetValue("RateLimiting:PermitsPerMinute", 120);
var bookingPermitsPerMinute = builder.Configuration.GetValue("RateLimiting:BookingPermitsPerMinute", 10);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) }));

    // Booking creation is the expensive, abuse-prone operation (ticket scalping bots).
    options.AddPolicy("bookings", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = bookingPermitsPerMinute,
                TokensPerPeriod = bookingPermitsPerMinute,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

app.UseRateLimiter();

EventOverviewEndpoint.Map(app);
app.MapReverseProxy();
app.MapDefaultEndpoints();

await app.RunAsync();

public partial class Program;
