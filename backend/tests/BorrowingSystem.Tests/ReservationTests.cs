using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Domain;

namespace BorrowingSystem.Tests;

public class ReservationTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateRange NextWeek = new(Now.AddDays(7), Now.AddDays(7).AddHours(4));

    private readonly User _faculty = new("Ana Cruz", "ana@lcc.edu.ph", UserRole.Faculty, departmentId: 1);
    private readonly User _custodian = new("Custodian", "admin@lcc.edu.ph", UserRole.Admin, departmentId: null);

    private static Equipment Unit(string tag) => new(tag, "Unit " + tag, "Speaker", null, Now);

    private Reservation NewReservation(params Equipment[] units) =>
        Reservation.Create(_faculty, 1, "Foundation Day", "Gym", null, NextWeek,
            units.Length == 0 ? [Unit("SPK-001")] : units, Now);

    [Fact]
    public void New_reservation_starts_pending()
    {
        var reservation = NewReservation();

        Assert.Equal(ReservationStatus.Pending, reservation.Status);
        Assert.Single(reservation.Items);
    }

    [Fact]
    public void Cannot_reserve_without_equipment()
    {
        Assert.Throws<DomainException>(() =>
            Reservation.Create(_faculty, 1, "Event", null, null, NextWeek, [], Now));
    }

    [Fact]
    public void Cannot_reserve_a_unit_under_repair()
    {
        var broken = Unit("PRJ-001");
        broken.SetCondition(EquipmentCondition.NeedsRepair);

        var error = Assert.Throws<DomainException>(() => NewReservation(broken));
        Assert.Contains("PRJ-001", error.Message);
    }

    [Fact]
    public void Cannot_reserve_in_the_past()
    {
        var past = new DateRange(Now.AddHours(-5), Now.AddHours(-1));
        Assert.Throws<DomainException>(() =>
            Reservation.Create(_faculty, 1, "Event", null, null, past, [Unit("SPK-001")], Now));
    }

    [Fact]
    public void Full_lifecycle_pending_to_returned()
    {
        var reservation = NewReservation();

        reservation.Approve(_custodian, Now);
        reservation.Release(_custodian, Now.AddDays(7));
        reservation.Receive(_custodian, new Dictionary<int, ReturnCondition>(), "All complete", Now.AddDays(7).AddHours(5));

        Assert.Equal(ReservationStatus.Returned, reservation.Status);
        Assert.Equal("All complete", reservation.ReturnRemarks);
    }

    [Fact]
    public void Cannot_release_before_approval()
    {
        var reservation = NewReservation();
        Assert.Throws<DomainException>(() => reservation.Release(_custodian, Now));
    }

    [Fact]
    public void Cannot_cancel_once_borrowed()
    {
        var reservation = NewReservation();
        reservation.Approve(_custodian, Now);
        reservation.Release(_custodian, Now);

        Assert.Throws<DomainException>(() => reservation.Cancel());
    }

    [Fact]
    public void Rejecting_requires_a_reason()
    {
        var reservation = NewReservation();
        Assert.Throws<DomainException>(() => reservation.Reject(_custodian, " ", Now));
    }

    [Fact]
    public void Damaged_return_flags_the_unit_for_repair()
    {
        var unit = Unit("MIC-001");
        var reservation = NewReservation(unit);
        reservation.Approve(_custodian, Now);
        reservation.Release(_custodian, Now);

        reservation.Receive(_custodian, new Dictionary<int, ReturnCondition> { [unit.Id] = ReturnCondition.Damaged },
            null, Now.AddHours(1));

        Assert.Equal(EquipmentCondition.NeedsRepair, unit.Condition);
        Assert.False(unit.IsServiceable);
    }

    [Fact]
    public void Overdue_only_when_borrowed_past_end()
    {
        var reservation = NewReservation();
        reservation.Approve(_custodian, Now);
        Assert.False(reservation.IsOverdue(NextWeek.End.AddHours(1)));

        reservation.Release(_custodian, NextWeek.Start);
        Assert.False(reservation.IsOverdue(NextWeek.End.AddMinutes(-1)));
        Assert.True(reservation.IsOverdue(NextWeek.End.AddMinutes(1)));
    }
}
