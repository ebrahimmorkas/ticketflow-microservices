using FluentValidation;
using MassTransit;
using Scalar.AspNetCore;
using TicketFlow.Booking.Api.Consumers;
using TicketFlow.Booking.Api.Data;
using TicketFlow.Booking.Api.Features;
using TicketFlow.Booking.Api.Saga;
using TicketFlow.BuildingBlocks.Messaging;
using TicketFlow.BuildingBlocks.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<BookingDbContext>("bookingdb");
builder.AddMessaging<BookingDbContext>(bus =>
{
    bus.AddConsumer<EventPublishedConsumer>();
    bus.AddConsumer<EventCancelledConsumer>();
    bus.AddConsumer<BookingConfirmedConsumer>();
    bus.AddConsumer<BookingCancelledConsumer>();

    bus.AddSagaStateMachine<BookingStateMachine, BookingState>()
        .EntityFrameworkRepository(repository =>
        {
            repository.ExistingDbContext<BookingDbContext>();
            repository.UsePostgres();
            repository.ConcurrencyMode = ConcurrencyMode.Optimistic;
        });
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.Services.ApplyMigrationsAsync<BookingDbContext>();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

var api = app.MapGroup("/api").WithTags("Bookings");
CreateBooking.Map(api);
BookingQueries.Map(api);

app.MapDefaultEndpoints();

await app.RunAsync();

public partial class Program;
