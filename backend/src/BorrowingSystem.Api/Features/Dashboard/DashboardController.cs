using BorrowingSystem.Api.Data;
using BorrowingSystem.Api.Domain;
using BorrowingSystem.Api.Features.Auth;
using BorrowingSystem.Api.Features.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BorrowingSystem.Api.Features.Dashboard;

public sealed record ScheduleEntryDto(
    int ReservationId,
    string EventName,
    string DepartmentCode,
    string RequestedByName,
    DateTime StartAt,
    DateTime EndAt,
    ReservationStatus Status,
    int ItemCount);

public sealed record DashboardDto(
    IReadOnlyDictionary<EquipmentStatus, int> EquipmentByStatus,
    int TotalEquipment,
    int PendingApprovals,
    IReadOnlyList<ScheduleEntryDto> ForRelease,
    IReadOnlyList<ScheduleEntryDto> DueForReturn,
    IReadOnlyList<ScheduleEntryDto> Overdue);

/// <summary>
/// The custodian's "what needs attention now" view. Faculty get the same shape, scoped to their own requests.
/// </summary>
[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(
    AppDbContext db,
    EquipmentService equipment,
    ICurrentUser currentUser,
    TimeProvider time) : ControllerBase
{
    private static readonly TimeSpan LookAhead = TimeSpan.FromHours(48);

    [HttpGet]
    public async Task<DashboardDto> Get(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var horizon = now + LookAhead;

        var units = await equipment.ListAsync(new EquipmentQuery(null, null, null), ct);
        var byStatus = Enum.GetValues<EquipmentStatus>()
            .Where(s => s != EquipmentStatus.Retired)
            .ToDictionary(s => s, s => units.Count(u => u.Status == s));

        var scope = db.Reservations.AsNoTracking();
        if (!currentUser.IsAdmin)
            scope = scope.Where(r => r.RequestedById == currentUser.Id);

        var pending = await scope.CountAsync(r => r.Status == ReservationStatus.Pending, ct);

        var forRelease = await ToEntries(scope
            .Where(r => r.Status == ReservationStatus.Approved && r.StartAt < horizon)
            .OrderBy(r => r.StartAt), ct);

        var dueForReturn = await ToEntries(scope
            .Where(r => r.Status == ReservationStatus.Borrowed && r.EndAt >= now && r.EndAt < horizon)
            .OrderBy(r => r.EndAt), ct);

        var overdue = await ToEntries(scope
            .Where(r => r.Status == ReservationStatus.Borrowed && r.EndAt < now)
            .OrderBy(r => r.EndAt), ct);

        return new DashboardDto(byStatus, units.Count, pending, forRelease, dueForReturn, overdue);
    }

    private static Task<List<ScheduleEntryDto>> ToEntries(IQueryable<Reservation> query, CancellationToken ct) =>
        query.Take(20)
            .Select(r => new ScheduleEntryDto(r.Id, r.EventName, r.Department.Code, r.RequestedBy.FullName,
                r.StartAt, r.EndAt, r.Status, r.Items.Count))
            .ToListAsync(ct);
}
