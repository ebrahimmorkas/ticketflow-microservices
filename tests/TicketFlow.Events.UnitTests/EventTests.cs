using TicketFlow.Events.Api.Domain;

namespace TicketFlow.Events.UnitTests;

public class EventTests
{
    private static readonly DateTime UtcNow = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Event CreateDraft() =>
        Event.Create("Rock Night", "Live music", "Arena", UtcNow.AddDays(30), UtcNow).Value;

    [Fact]
    public void Create_Should_Fail_When_Event_Starts_In_The_Past()
    {
        Event.Create("Past", "", "Hall", UtcNow.AddMinutes(-1), UtcNow).Error.ShouldBe(EventErrors.StartsInPast);
    }

    [Fact]
    public void Create_Should_Normalize_Start_Time_To_Utc()
    {
        var unspecified = DateTime.SpecifyKind(UtcNow.AddDays(1), DateTimeKind.Unspecified);

        Event.Create("Show", "", "Hall", unspecified, UtcNow).Value.StartsAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void AddTicketType_Should_Reject_Duplicate_Names()
    {
        var @event = CreateDraft();
        @event.AddTicketType("VIP", 100m, "usd", 50);

        @event.AddTicketType("vip", 120m, "USD", 10).Error!.Code.ShouldBe("Event.DuplicateTicketType");
    }

    [Fact]
    public void AddTicketType_Should_Normalize_Currency()
    {
        CreateDraft().AddTicketType("General", 25m, "eur", 100).Value.Currency.ShouldBe("EUR");
    }

    [Fact]
    public void Publish_Should_Fail_Without_Ticket_Types()
    {
        CreateDraft().Publish(UtcNow).Error.ShouldBe(EventErrors.NoTicketTypes);
    }

    [Fact]
    public void Publish_Should_Move_Draft_To_Published()
    {
        var @event = CreateDraft();
        @event.AddTicketType("General", 25m, "USD", 100);

        @event.Publish(UtcNow).IsSuccess.ShouldBeTrue();

        @event.Status.ShouldBe(EventStatus.Published);
        @event.PublishedAtUtc.ShouldBe(UtcNow);
    }

    [Fact]
    public void Published_Event_Should_Not_Accept_New_Ticket_Types()
    {
        var @event = CreateDraft();
        @event.AddTicketType("General", 25m, "USD", 100);
        @event.Publish(UtcNow);

        @event.AddTicketType("VIP", 100m, "USD", 10).Error.ShouldBe(EventErrors.NotDraft);
        @event.Publish(UtcNow).Error.ShouldBe(EventErrors.NotDraft);
    }

    [Fact]
    public void Cancel_Should_Fail_When_Already_Cancelled()
    {
        var @event = CreateDraft();
        @event.Cancel();

        @event.Cancel().Error.ShouldBe(EventErrors.AlreadyCancelled);
    }
}
