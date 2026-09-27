using TicketFlow.BuildingBlocks.Messaging;
using TicketFlow.Notifications.Worker.Consumers;
using TicketFlow.Notifications.Worker.Email;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddMessaging(bus =>
{
    bus.AddConsumer<SendBookingConfirmationEmailConsumer>();
    bus.AddConsumer<SendBookingCancellationEmailConsumer>();
});

builder.Services.Configure<EmailOptions>(options =>
{
    builder.Configuration.GetSection(EmailOptions.SectionName).Bind(options);

    // Aspire's MailPit resource exposes "Endpoint=smtp://host:port".
    var connectionString = builder.Configuration.GetConnectionString("mailpit");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.SmtpEndpoint = new Uri(connectionString.Replace("Endpoint=", string.Empty, StringComparison.OrdinalIgnoreCase));
    }
});
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

await builder.Build().RunAsync();
