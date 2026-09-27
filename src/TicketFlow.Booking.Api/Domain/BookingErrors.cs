using TicketFlow.BuildingBlocks.Results;

namespace TicketFlow.Booking.Api.Domain;

public static class BookingErrors
{
    public static readonly Error ConcurrencyConflict =
        Error.Conflict("Booking.ConcurrencyConflict", "Tickets are in high demand right now. Please try again.");

    public static Error NotFound(Guid id) => Error.NotFound("Booking.NotFound", $"Booking '{id}' was not found.");

    public static Error TicketTypeNotFound(Guid id) =>
        Error.NotFound("Booking.TicketTypeNotFound", $"Ticket type '{id}' is not available for booking.");

    public static Error NotOnSale(Guid id) =>
        Error.Conflict("Booking.NotOnSale", $"Ticket type '{id}' is no longer on sale.");

    public static Error SoldOut(Guid id, int available, int requested) =>
        Error.Conflict("Booking.SoldOut", $"Only {available} ticket(s) of type '{id}' are left but {requested} were requested.");

    public static Error InvalidTransition(BookingStatus from, BookingStatus to) =>
        Error.Conflict("Booking.InvalidTransition", $"Cannot move a booking from '{from}' to '{to}'.");
}
