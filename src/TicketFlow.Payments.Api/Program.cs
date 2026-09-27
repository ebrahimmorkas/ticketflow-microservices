using Scalar.AspNetCore;
using TicketFlow.BuildingBlocks.Messaging;
using TicketFlow.BuildingBlocks.Persistence;
using TicketFlow.Payments.Api.Consumers;
using TicketFlow.Payments.Api.Data;
using TicketFlow.Payments.Api.Features;
using TicketFlow.Payments.Api.Gateway;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<PaymentsDbContext>("paymentsdb");
builder.AddMessaging<PaymentsDbContext>(bus => bus.AddConsumer<ProcessPaymentConsumer>());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection(PaymentGatewayOptions.SectionName));
builder.Services.AddScoped<IPaymentGateway, SimulatedPaymentGateway>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.Services.ApplyMigrationsAsync<PaymentsDbContext>();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

PaymentQueries.Map(app.MapGroup("/api").WithTags("Payments"));

app.MapDefaultEndpoints();

await app.RunAsync();

public partial class Program;
