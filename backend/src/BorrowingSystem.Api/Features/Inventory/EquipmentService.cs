using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Data;
using BorrowingSystem.Api.Domain;
using BorrowingSystem.Api.Features.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace BorrowingSystem.Api.Features.Inventory;

public sealed class EquipmentService(AppDbContext db, ScheduleService schedule, TimeProvider time)
{
    private static readonly TimeSpan UpcomingWindow = TimeSpan.FromDays(60);
    private const int HistorySize = 10;

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<EquipmentDto>> ListAsync(EquipmentQuery query, CancellationToken ct)
    {
        var units = db.Equipment.AsNoTracking();

        if (!query.IncludeRetired && query.Status != EquipmentStatus.Retired)
            units = units.Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Category))
            units = units.Where(e => e.Category == query.Category);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            units = units.Where(e => e.AssetTag.Contains(term) || e.Name.Contains(term) || e.Category.Contains(term));
        }

        var list = await units.OrderBy(e => e.Category).ThenBy(e => e.AssetTag).ToListAsync(ct);
        var dtos = await WithStatusAsync(list, ct);

        return query.Status is { } status ? dtos.Where(d => d.Status == status).ToList() : dtos;
    }

    public async Task<IReadOnlyList<string>> ListCategoriesAsync(CancellationToken ct) =>
        await db.Equipment.AsNoTracking().Select(e => e.Category).Distinct().OrderBy(c => c).ToListAsync(ct);

    public async Task<EquipmentDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var unit = await db.Equipment.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct)
                   ?? throw NotFoundException.For<Equipment>(id);

        var now = Now;
        var bookings = (await schedule.GetBookingsAsync(new DateRange(now, now + UpcomingWindow), [id], ct: ct))[id];
        var dto = (await WithStatusAsync([unit], ct))[0];

        var history = await db.ReservationItems.AsNoTracking()
            .Where(i => i.EquipmentId == id && i.Reservation.Status == ReservationStatus.Returned)
            .OrderByDescending(i => i.Reservation.ReturnedAt)
            .Take(HistorySize)
            .Select(i => new BookingDto(i.ReservationId, i.Reservation.EventName, i.Reservation.Department.Code,
                i.Reservation.StartAt, i.Reservation.EndAt, i.Reservation.Status))
            .ToListAsync(ct);

        return new EquipmentDetailDto(dto, bookings.Select(b => b.ToDto()).ToList(), history);
    }

    /// <summary>Every serviceable unit, flagged with whether it is free for the requested period.</summary>
    public async Task<IReadOnlyList<AvailabilityDto>> GetAvailabilityAsync(DateRange period, CancellationToken ct)
    {
        var units = await db.Equipment.AsNoTracking()
            .Where(e => e.IsActive && e.Condition == EquipmentCondition.Good)
            .OrderBy(e => e.Category).ThenBy(e => e.AssetTag)
            .ToListAsync(ct);

        var bookings = await schedule.GetBookingsAsync(period, ct: ct);
        var statusBookings = await schedule.GetBookingsAsync(StatusWindow(), ct: ct);

        return units.Select(unit =>
        {
            var conflict = bookings[unit.Id].FirstOrDefault(b => b.Period.Overlaps(period));
            var status = EquipmentStatusResolver.Resolve(unit, statusBookings[unit.Id], Now);
            return new AvailabilityDto(unit.ToDto(status, statusBookings[unit.Id].FirstOrDefault()),
                conflict is null, conflict?.ToDto());
        }).ToList();
    }

    public async Task<EquipmentDto> CreateAsync(SaveEquipmentRequest request, CancellationToken ct)
    {
        var unit = new Equipment(request.AssetTag, request.Name, request.Category, request.Description, Now);
        unit.SetCondition(request.Condition);
        await EnsureAssetTagIsFreeAsync(unit.AssetTag, exceptId: null, ct);

        db.Equipment.Add(unit);
        await db.SaveChangesAsync(ct);
        return (await WithStatusAsync([unit], ct))[0];
    }

    public async Task<EquipmentDto> UpdateAsync(int id, SaveEquipmentRequest request, CancellationToken ct)
    {
        var unit = await db.Equipment.FindAsync([id], ct) ?? throw NotFoundException.For<Equipment>(id);

        unit.UpdateDetails(request.AssetTag, request.Name, request.Category, request.Description);
        unit.SetCondition(request.Condition);
        if (request.IsActive) unit.Reinstate(); else unit.Retire();
        await EnsureAssetTagIsFreeAsync(unit.AssetTag, exceptId: id, ct);

        await db.SaveChangesAsync(ct);
        return (await WithStatusAsync([unit], ct))[0];
    }

    private async Task<List<EquipmentDto>> WithStatusAsync(IReadOnlyList<Equipment> units, CancellationToken ct)
    {
        var ids = units.Select(u => u.Id).ToList();
        var bookings = await schedule.GetBookingsAsync(StatusWindow(), ids, ct: ct);
        var now = Now;

        return units.Select(unit =>
        {
            var unitBookings = bookings[unit.Id].ToList();
            var status = EquipmentStatusResolver.Resolve(unit, unitBookings, now);
            // Show who holds it: the borrower if it is out, otherwise the next booking.
            var current = unitBookings.FirstOrDefault(b => b.Status == ReservationStatus.Borrowed)
                          ?? unitBookings.FirstOrDefault();
            return unit.ToDto(status, current);
        }).ToList();
    }

    private DateRange StatusWindow() => new(Now, Now + EquipmentStatusResolver.ReservedHorizon);

    private async Task EnsureAssetTagIsFreeAsync(string assetTag, int? exceptId, CancellationToken ct)
    {
        if (await db.Equipment.AnyAsync(e => e.AssetTag == assetTag && e.Id != exceptId, ct))
            throw new ConflictException($"Asset tag {assetTag} is already used by another unit.");
    }
}
