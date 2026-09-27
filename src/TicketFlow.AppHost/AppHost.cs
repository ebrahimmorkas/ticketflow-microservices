var builder = DistributedApplication.CreateBuilder(args);

// ---------- infrastructure ----------
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var eventsDb = postgres.AddDatabase("eventsdb");

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// ---------- services ----------
builder.AddProject<Projects.TicketFlow_Events_Api>("events-api")
    .WithReference(eventsDb)
    .WithReference(messaging)
    .WaitFor(eventsDb)
    .WaitFor(messaging);

builder.Build().Run();
