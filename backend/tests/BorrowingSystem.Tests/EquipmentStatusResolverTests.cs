using BorrowingSystem.Api.Domain;

namespace BorrowingSystem.Tests;

public class EquipmentStatusResolverTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 1, 0, 0, DateTimeKind.Utc);

    private static Equipment Unit() => new("SPK-001", "Speaker", "Speaker", null, Now);

    private static Booking BookingAt(DateTime start, double hours, ReservationStatus status) =>
        new(1, 10, "Event", "CTE", new DateRange(start, start.AddHours(hours)), status);

    [Fact]
    public void Free_unit_is_available()
    {
        Assert.Equal(EquipmentStatus.Available, EquipmentStatusResolver.Resolve(Unit(), [], Now));
    }

    [Fact]
    public void Booking_within_24_hours_shows_reserved()
    {
        var booking = BookingAt(Now.AddHours(5), 3, ReservationStatus.Approved);
        Assert.Equal(EquipmentStatus.Reserved, EquipmentStatusResolver.Resolve(Unit(), [booking], Now));
    }

    [Fact]
    public void Booking_next_week_keeps_unit_available_today()
    {
        var booking = BookingAt(Now.AddDays(7), 3, ReservationStatus.Approved);
        Assert.Equal(EquipmentStatus.Available, EquipmentStatusResolver.Resolve(Unit(), [booking], Now));
    }

    [Fact]
    public void Borrowed_wins_even_when_overdue()
    {
        var overdue = BookingAt(Now.AddDays(-2), 3, ReservationStatus.Borrowed);
        Assert.Equal(EquipmentStatus.Borrowed, EquipmentStatusResolver.Resolve(Unit(), [overdue], Now));
    }

    [Fact]
    public void Condition_overrides_schedule()
    {
        var unit = Unit();
        unit.SetCondition(EquipmentCondition.NeedsRepair);
        Assert.Equal(EquipmentStatus.UnderRepair, EquipmentStatusResolver.Resolve(unit, [], Now));

        unit.Retire();
        Assert.Equal(EquipmentStatus.Retired, EquipmentStatusResolver.Resolve(unit, [], Now));
    }
}
