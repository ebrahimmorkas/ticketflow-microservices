var builder = DistributedApplication.CreateBuilder(args);

// ---------- infrastructure ----------
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var eventsDb = postgres.AddDatabase("eventsdb");
var bookingDb = postgres.AddDatabase("bookingdb");
var paymentsDb = postgres.AddDatabase("paymentsdb");

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// Local SMTP server with a web inbox for viewing the emails the platform sends.
var mailpit = builder.AddMailPit("mailpit");

// ---------- services ----------
var eventsApi = builder.AddProject<Projects.TicketFlow_Events_Api>("events-api")
    .WithHttpHealthCheck("/health")
    .WithReference(eventsDb)
    .WithReference(messaging)
    .WaitFor(eventsDb)
    .WaitFor(messaging);

var bookingApi = builder.AddProject<Projects.TicketFlow_Booking_Api>("booking-api")
    .WithHttpHealthCheck("/health")
    .WithReference(bookingDb)
    .WithReference(messaging)
    .WaitFor(bookingDb)
    .WaitFor(messaging);

var paymentsApi = builder.AddProject<Projects.TicketFlow_Payments_Api>("payments-api")
    .WithHttpHealthCheck("/health")
    .WithReference(paymentsDb)
    .WithReference(messaging)
    .WaitFor(paymentsDb)
    .WaitFor(messaging);

builder.AddProject<Projects.TicketFlow_Notifications_Worker>("notifications-worker")
    .WithReference(messaging)
    .WithReference(mailpit)
    .WaitFor(messaging)
    .WaitFor(mailpit);

// Single public entry point; the individual services are not exposed externally.
builder.AddProject<Projects.TicketFlow_Gateway>("gateway")
    .WithHttpHealthCheck("/health")
    .WithReference(eventsApi)
    .WithReference(bookingApi)
    .WithReference(paymentsApi)
    .WaitFor(eventsApi)
    .WaitFor(bookingApi)
    .WaitFor(paymentsApi)
    .WithExternalHttpEndpoints();

builder.Build().Run();
