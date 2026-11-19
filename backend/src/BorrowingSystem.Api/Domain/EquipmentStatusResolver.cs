namespace BorrowingSystem.Api.Domain;

/// <summary>
/// Works out the live status of a unit. Status is derived from condition + bookings rather than
/// stored, so it can never drift out of sync with the reservations that actually exist.
/// </summary>
public static class EquipmentStatusResolver
{
    /// <summary>A unit counts as "Reserved" when a booking touches the next 24 hours.</summary>
    public static readonly TimeSpan ReservedHorizon = TimeSpan.FromHours(24);

    public static EquipmentStatus Resolve(Equipment equipment, IEnumerable<Booking> bookings, DateTime now)
    {
        if (!equipment.IsActive) return EquipmentStatus.Retired;

        switch (equipment.Condition)
        {
            case EquipmentCondition.Missing: return EquipmentStatus.Missing;
            case EquipmentCondition.NeedsRepair: return EquipmentStatus.UnderRepair;
        }

        var horizon = new DateRange(now, now + ReservedHorizon);
        var isReserved = false;

        foreach (var booking in bookings)
        {
            if (booking.Status == ReservationStatus.Borrowed) return EquipmentStatus.Borrowed;
            if (booking.Period.Overlaps(horizon)) isReserved = true;
        }

        return isReserved ? EquipmentStatus.Reserved : EquipmentStatus.Available;
    }
}
