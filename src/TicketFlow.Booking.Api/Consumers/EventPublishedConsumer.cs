using MassTransit;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Booking.Api.Data;
using TicketFlow.Booking.Api.Domain;
using TicketFlow.Contracts;

namespace TicketFlow.Booking.Api.Consumers;

/// <summary>
/// Builds the local ticket inventory when an event goes on sale. Idempotent: redelivered messages
/// don't duplicate rows (and the EF inbox already de-duplicates by message id).
/// </summary>
public sealed class EventPublishedConsumer(BookingDbContext dbContext) : IConsumer<EventPublished>
{
    public async Task Consume(ConsumeContext<EventPublished> context)
    {
        var message = context.Message;
        var ticketTypeIds = message.TicketTypes.Select(t => t.TicketTypeId).ToList();

        var existing = await dbContext.Inventory
            .Where(i => ticketTypeIds.Contains(i.TicketTypeId))
            .Select(i => i.TicketTypeId)
            .ToListAsync(context.CancellationToken);

        foreach (var ticketType in message.TicketTypes.Where(t => !existing.Contains(t.TicketTypeId)))
        {
            dbContext.Inventory.Add(TicketInventory.Create(
                ticketType.TicketTypeId,
                message.EventId,
                message.Name,
                ticketType.Name,
                ticketType.Price,
                ticketType.Currency,
                ticketType.Capacity));
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
