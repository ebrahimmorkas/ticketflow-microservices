using System.Globalization;
using System.Net;
using TicketFlow.Contracts;

namespace TicketFlow.Notifications.Worker.Email;

/// <summary>
/// Renders notification emails. All user-supplied values are HTML-encoded.
/// </summary>
public static class EmailTemplates
{
    public static EmailMessage BookingConfirmed(BookingConfirmed message)
    {
        var eventName = WebUtility.HtmlEncode(message.EventName);
        var total = message.Amount.ToString("N2", CultureInfo.InvariantCulture);

        return new EmailMessage(
            message.CustomerEmail,
            $"Your tickets for {message.EventName} are confirmed",
            $"""
            <h2>You're going to {eventName}! 🎉</h2>
            <p>Your booking <strong>{message.BookingId}</strong> is confirmed.</p>
            <table>
              <tr><td>Tickets</td><td>{message.Quantity}</td></tr>
              <tr><td>Total paid</td><td>{total} {message.Currency}</td></tr>
            </table>
            <p>Show this email at the entrance. Enjoy the event!</p>
            """);
    }

    public static EmailMessage BookingCancelled(BookingCancelled message)
    {
        var eventName = WebUtility.HtmlEncode(message.EventName);
        var reason = WebUtility.HtmlEncode(message.Reason);

        return new EmailMessage(
            message.CustomerEmail,
            $"Your booking for {message.EventName} was cancelled",
            $"""
            <h2>Booking cancelled</h2>
            <p>Unfortunately your booking <strong>{message.BookingId}</strong> for <strong>{eventName}</strong> was cancelled.</p>
            <p>Reason: {reason}</p>
            <p>If you were charged, a full refund will be issued automatically.</p>
            """);
    }
}
