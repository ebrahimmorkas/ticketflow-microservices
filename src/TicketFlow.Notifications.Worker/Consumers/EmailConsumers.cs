using MassTransit;
using TicketFlow.Contracts;
using TicketFlow.Notifications.Worker.Email;

namespace TicketFlow.Notifications.Worker.Consumers;

// Consumer class names drive queue names, so they are deliberately distinct from the Booking
// service's consumers of the same messages; otherwise both services would compete on one queue.

public sealed class SendBookingConfirmationEmailConsumer(IEmailSender emailSender) : IConsumer<BookingConfirmed>
{
    public Task Consume(ConsumeContext<BookingConfirmed> context) =>
        emailSender.SendAsync(EmailTemplates.BookingConfirmed(context.Message), context.CancellationToken);
}

public sealed class SendBookingCancellationEmailConsumer(IEmailSender emailSender) : IConsumer<BookingCancelled>
{
    public Task Consume(ConsumeContext<BookingCancelled> context) =>
        emailSender.SendAsync(EmailTemplates.BookingCancelled(context.Message), context.CancellationToken);
}
