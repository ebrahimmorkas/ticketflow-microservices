using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Contracts;
using TicketFlow.Notifications.Worker.Consumers;
using TicketFlow.Notifications.Worker.Email;

namespace TicketFlow.Notifications.UnitTests;

public class NotificationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Confirmation_Template_Should_Include_Booking_Details()
    {
        var email = EmailTemplates.BookingConfirmed(new BookingConfirmed(Guid.NewGuid(), "Rock Night", "fan@example.com", 3, 1234.5m, "USD"));

        email.To.ShouldBe("fan@example.com");
        email.Subject.ShouldContain("Rock Night");
        email.HtmlBody.ShouldContain("1,234.50 USD");
        email.HtmlBody.ShouldContain("<td>3</td>");
    }

    [Fact]
    public void Templates_Should_Html_Encode_User_Supplied_Values()
    {
        var email = EmailTemplates.BookingCancelled(
            new BookingCancelled(Guid.NewGuid(), "<script>alert(1)</script>", "fan@example.com", "Card <b>declined</b>"));

        email.HtmlBody.ShouldNotContain("<script>");
        email.HtmlBody.ShouldContain("&lt;script&gt;");
        email.HtmlBody.ShouldContain("Card &lt;b&gt;declined&lt;/b&gt;");
    }

    [Fact]
    public async Task Consumers_Should_Send_One_Email_Per_Message()
    {
        var sender = new FakeEmailSender();
        await using var provider = new ServiceCollection()
            .AddSingleton<IEmailSender>(sender)
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<SendBookingConfirmationEmailConsumer>();
                bus.AddConsumer<SendBookingCancellationEmailConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new BookingConfirmed(Guid.NewGuid(), "Rock Night", "a@example.com", 1, 50m, "USD"), Ct);
        await harness.Bus.Publish(new BookingCancelled(Guid.NewGuid(), "Jazz", "b@example.com", "Payment failed"), Ct);
        await harness.InactivityTask;

        sender.Sent.Count.ShouldBe(2);
        sender.Sent.ShouldContain(m => m.To == "a@example.com" && m.Subject.Contains("confirmed"));
        sender.Sent.ShouldContain(m => m.To == "b@example.com" && m.Subject.Contains("cancelled"));
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            lock (Sent)
            {
                Sent.Add(message);
            }

            return Task.CompletedTask;
        }
    }
}
