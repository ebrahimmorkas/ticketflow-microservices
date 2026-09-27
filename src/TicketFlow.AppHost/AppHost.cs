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
builder.AddProject<Projects.TicketFlow_Events_Api>("events-api")
    .WithReference(eventsDb)
    .WithReference(messaging)
    .WaitFor(eventsDb)
    .WaitFor(messaging);

builder.AddProject<Projects.TicketFlow_Booking_Api>("booking-api")
    .WithReference(bookingDb)
    .WithReference(messaging)
    .WaitFor(bookingDb)
    .WaitFor(messaging);

builder.AddProject<Projects.TicketFlow_Payments_Api>("payments-api")
    .WithReference(paymentsDb)
    .WithReference(messaging)
    .WaitFor(paymentsDb)
    .WaitFor(messaging);

builder.AddProject<Projects.TicketFlow_Notifications_Worker>("notifications-worker")
    .WithReference(messaging)
    .WithReference(mailpit)
    .WaitFor(messaging)
    .WaitFor(mailpit);

builder.Build().Run();
