using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Data;
using BorrowingSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BorrowingSystem.Api.Features.Scheduling;

/// <summary>
/// The one place that knows how to read the equipment calendar. Both the equipment pages (live status)
/// and the reservation workflow (double-booking checks) go through here.
/// </summary>
public sealed class ScheduleService(AppDbContext db)
{
    /// <summary>
    /// Loads bookings that hold equipment during <paramref name="window"/>, plus every unit that is out
    /// right now (an overdue unit is still physically away even after its window ended).
    /// Returned as a lookup keyed by equipment id, so callers get O(1) access per unit.
    /// </summary>
    public async Task<ILookup<int, Booking>> GetBookingsAsync(
        DateRange window,
        IReadOnlyCollection<int>? equipmentIds = null,
        int? excludeReservationId = null,
        CancellationToken ct = default)
    {
        var query = db.ReservationItems.AsNoTracking()
            .Where(i => Reservation.BlockingStatuses.Contains(i.Reservation.Status))
            .Where(i => i.Reservation.Status == ReservationStatus.Borrowed ||
                        (i.Reservation.StartAt < window.End && i.Reservation.EndAt > window.Start));

        if (equipmentIds is not null)
            query = query.Where(i => equipmentIds.Contains(i.EquipmentId));
        if (excludeReservationId is { } excluded)
            query = query.Where(i => i.ReservationId != excluded);

        var rows = await query
            .Select(i => new
            {
                i.EquipmentId,
                i.ReservationId,
                i.Reservation.EventName,
                DepartmentCode = i.Reservation.Department.Code,
                i.Reservation.StartAt,
                i.Reservation.EndAt,
                i.Reservation.Status
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new Booking(r.EquipmentId, r.ReservationId, r.EventName, r.DepartmentCode,
                new DateRange(r.StartAt, r.EndAt), r.Status))
            .OrderBy(b => b.Period.Start)
            .ToLookup(b => b.EquipmentId);
    }

    /// <summary>Throws a <see cref="ConflictException"/> naming every unit that is already booked.</summary>
    public async Task EnsureAvailableAsync(
        IReadOnlyCollection<Equipment> equipment,
        DateRange period,
        int? excludeReservationId = null,
        CancellationToken ct = default)
    {
        var ids = equipment.Select(e => e.Id).ToList();
        var bookings = await GetBookingsAsync(period, ids, excludeReservationId, ct);

        var clashes = equipment
            .SelectMany(e => bookings[e.Id]
                .Where(b => b.Period.Overlaps(period))
                .Select(b => $"{e.AssetTag} ({b.DepartmentCode} – {b.EventName})"))
            .ToList();

        if (clashes.Count > 0)
            throw new ConflictException($"Already booked for that time: {string.Join("; ", clashes)}.");
    }

    /// <summary>Units that are still out on another (typically overdue) reservation.</summary>
    public async Task<IReadOnlyList<string>> FindUnitsStillOutAsync(
        IReadOnlyCollection<int> equipmentIds, int excludeReservationId, CancellationToken ct = default) =>
        await db.ReservationItems.AsNoTracking()
            .Where(i => equipmentIds.Contains(i.EquipmentId)
                        && i.ReservationId != excludeReservationId
                        && i.Reservation.Status == ReservationStatus.Borrowed)
            .Select(i => i.Equipment.AssetTag)
            .ToListAsync(ct);
}
