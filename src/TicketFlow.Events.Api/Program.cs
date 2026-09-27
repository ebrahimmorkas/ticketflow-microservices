using FluentValidation;
using Scalar.AspNetCore;
using TicketFlow.BuildingBlocks.Messaging;
using TicketFlow.BuildingBlocks.Persistence;
using TicketFlow.Events.Api.Data;
using TicketFlow.Events.Api.Features;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<EventsDbContext>("eventsdb");
builder.AddMessaging<EventsDbContext>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.Services.ApplyMigrationsAsync<EventsDbContext>();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

var api = app.MapGroup("/api").WithTags("Events");
CreateEvent.Map(api);
GetEvents.Map(api);
PublishEvent.Map(api);
CancelEvent.Map(api);

app.MapDefaultEndpoints();

await app.RunAsync();

public partial class Program;
