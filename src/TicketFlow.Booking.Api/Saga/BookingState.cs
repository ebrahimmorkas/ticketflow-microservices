using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TicketFlow.Booking.Api.Saga;

/// <summary>
/// Persisted state of one booking's checkout process. The correlation id is the booking id.
/// </summary>
public sealed class BookingState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public string EventName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    /// <summary>Maps to PostgreSQL's xmin for optimistic concurrency between competing messages.</summary>
    public uint RowVersion { get; set; }
}

internal sealed class BookingStateMap : SagaClassMap<BookingState>
{
    protected override void Configure(EntityTypeBuilder<BookingState> entity, ModelBuilder model)
    {
        entity.ToTable("booking_sagas");
        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.EventName).HasMaxLength(200);
        entity.Property(x => x.CustomerEmail).HasMaxLength(256);
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.Currency).HasMaxLength(3);
        entity.Property(x => x.RowVersion).IsRowVersion();
    }
}
