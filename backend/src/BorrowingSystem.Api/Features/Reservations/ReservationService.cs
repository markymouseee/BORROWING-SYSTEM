using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Data;
using BorrowingSystem.Api.Domain;
using BorrowingSystem.Api.Features.Auth;
using BorrowingSystem.Api.Features.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace BorrowingSystem.Api.Features.Reservations;

/// <summary>
/// Application service for the borrowing workflow. It loads the aggregate, checks who is asking,
/// calls the domain method that enforces the rule, and saves. Business rules live in <see cref="Reservation"/>.
/// </summary>
public sealed class ReservationService(
    AppDbContext db,
    ScheduleService schedule,
    ICurrentUser currentUser,
    TimeProvider time)
{
    /// <summary>
    /// Serialises the "check calendar, then write" steps so two departments clicking Reserve at the same
    /// moment cannot both get the same projector. Enough for a single API instance; if the API is ever
    /// scaled out, replace with a database-level lock (see docs/ARCHITECTURE.md).
    /// </summary>
    private static readonly SemaphoreSlim ScheduleGate = new(1, 1);

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<ReservationSummaryDto>> ListAsync(ReservationQuery query, CancellationToken ct)
    {
        var now = Now;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 5, 100);

        var reservations = db.Reservations.AsNoTracking();

        // Faculty only ever see their own requests; admins can opt in to "mine".
        if (query.Mine || !currentUser.IsAdmin)
            reservations = reservations.Where(r => r.RequestedById == currentUser.Id);
        if (query.Status is { } status)
            reservations = reservations.Where(r => r.Status == status);
        if (query.OverdueOnly)
            reservations = reservations.Where(r => r.Status == ReservationStatus.Borrowed && r.EndAt < now);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            reservations = reservations.Where(r => r.EventName.Contains(term)
                                                   || r.RequestedBy.FullName.Contains(term)
                                                   || r.Department.Code.Contains(term));
        }

        var total = await reservations.CountAsync(ct);
        var rows = await reservations
            .OrderByDescending(r => r.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id, r.EventName, r.Venue, DepartmentCode = r.Department.Code, RequestedBy = r.RequestedBy.FullName,
                r.StartAt, r.EndAt, r.Status, r.CreatedAt,
                AssetTags = r.Items.Select(i => i.Equipment.AssetTag).ToList()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ReservationSummaryDto(
            r.Id, Reservation.FormatCode(r.Id),r.EventName, r.Venue, r.DepartmentCode, r.RequestedBy, r.StartAt, r.EndAt,
            r.Status, r.Status == ReservationStatus.Borrowed && now > r.EndAt, r.AssetTags, r.CreatedAt)).ToList();

        return new PagedResult<ReservationSummaryDto>(items, page, pageSize, total);
    }

    public async Task<ReservationDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var reservation = await LoadAsync(id, ct);
        EnsureCanView(reservation);
        return ToDetail(reservation);
    }

    public async Task<ReservationDetailDto> CreateAsync(CreateReservationRequest request, CancellationToken ct)
    {
        var requester = await currentUser.GetAsync(ct);
        var departmentId = await ResolveDepartmentAsync(requester, request.DepartmentId, ct);
        var period = new DateRange(request.StartAt.ToUniversalTime(), request.EndAt.ToUniversalTime());

        var ids = request.EquipmentIds.Distinct().ToList();
        var equipment = await db.Equipment.Where(e => ids.Contains(e.Id)).ToListAsync(ct);
        if (equipment.Count != ids.Count)
            throw new DomainException("One or more selected items no longer exist.");

        await ScheduleGate.WaitAsync(ct);
        try
        {
            await schedule.EnsureAvailableAsync(equipment, period, ct: ct);

            var reservation = Reservation.Create(requester, departmentId, request.EventName, request.Venue,
                request.Purpose, period, equipment, Now);

            // Requests logged by the custodian are already vetted, so they skip the approval queue.
            if (requester.IsAdmin)
                reservation.Approve(requester, Now);

            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);
            return await GetAsync(reservation.Id, ct);
        }
        finally
        {
            ScheduleGate.Release();
        }
    }

    public Task<ReservationDetailDto> ApproveAsync(int id, CancellationToken ct) =>
        UpdateAsync(id, adminOnly: true, (r, user) => r.Approve(user, Now), ct);

    public Task<ReservationDetailDto> RejectAsync(int id, string reason, CancellationToken ct) =>
        UpdateAsync(id, adminOnly: true, (r, user) => r.Reject(user, reason, Now), ct);

    public Task<ReservationDetailDto> CancelAsync(int id, CancellationToken ct) =>
        UpdateAsync(id, adminOnly: false, (r, _) => r.Cancel(), ct);

    public async Task<ReservationDetailDto> ReleaseAsync(int id, CancellationToken ct)
    {
        await ScheduleGate.WaitAsync(ct);
        try
        {
            var reservation = await LoadAsync(id, ct);
            var stillOut = await schedule.FindUnitsStillOutAsync(
                reservation.Items.Select(i => i.EquipmentId).ToList(), reservation.Id, ct);
            if (stillOut.Count > 0)
                throw new ConflictException(
                    $"Not yet returned from an earlier borrowing: {string.Join(", ", stillOut)}.");

            return await UpdateAsync(reservation, adminOnly: true, (r, user) => r.Release(user, Now), ct);
        }
        finally
        {
            ScheduleGate.Release();
        }
    }

    public Task<ReservationDetailDto> ReceiveAsync(int id, ReceiveReservationRequest request, CancellationToken ct)
    {
        var conditions = (request.Items ?? [])
            .GroupBy(i => i.EquipmentId)
            .ToDictionary(g => g.Key, g => g.Last().Condition);

        return UpdateAsync(id, adminOnly: true, (r, user) => r.Receive(user, conditions, request.Remarks, Now), ct);
    }

    private async Task<ReservationDetailDto> UpdateAsync(int id, bool adminOnly, Action<Reservation, User> change,
        CancellationToken ct) =>
        await UpdateAsync(await LoadAsync(id, ct), adminOnly, change, ct);

    private async Task<ReservationDetailDto> UpdateAsync(Reservation reservation, bool adminOnly,
        Action<Reservation, User> change, CancellationToken ct)
    {
        var user = await currentUser.GetAsync(ct);
        if (adminOnly && !user.IsAdmin)
            throw new ForbiddenException("Only the property custodian can do this.");
        EnsureCanView(reservation);

        change(reservation, user);
        await db.SaveChangesAsync(ct);
        return ToDetail(reservation);
    }

    private async Task<Reservation> LoadAsync(int id, CancellationToken ct) =>
        await db.Reservations
            .Include(r => r.Department)
            .Include(r => r.RequestedBy)
            .Include(r => r.ReviewedBy)
            .Include(r => r.ReleasedBy)
            .Include(r => r.ReceivedBy)
            .Include(r => r.Items).ThenInclude(i => i.Equipment)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw NotFoundException.For<Reservation>(id);

    private async Task<int> ResolveDepartmentAsync(User requester, int? requestedDepartmentId, CancellationToken ct)
    {
        // Faculty always borrow for their own department; the custodian can log a request for any department.
        var departmentId = requester.IsAdmin ? requestedDepartmentId : requester.DepartmentId;
        if (departmentId is null)
            throw new DomainException("Choose the department that will use the equipment.");
        if (!await db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            throw new DomainException("The selected department does not exist.");
        return departmentId.Value;
    }

    private void EnsureCanView(Reservation reservation)
    {
        if (!currentUser.IsAdmin && reservation.RequestedById != currentUser.Id)
            throw new ForbiddenException("You can only view your own reservations.");
    }

    private ReservationDetailDto ToDetail(Reservation r) => new(
        r.Id, r.Code, r.EventName, r.Venue, r.Purpose,
        r.DepartmentId, r.Department.Code, r.Department.Name,
        r.RequestedById, r.RequestedBy.FullName,
        r.StartAt, r.EndAt, r.Status, r.IsOverdue(Now), r.CreatedAt,
        r.ReviewedBy?.FullName, r.ReviewedAt, r.RejectionReason,
        r.ReleasedBy?.FullName, r.BorrowedAt,
        r.ReceivedBy?.FullName, r.ReturnedAt, r.ReturnRemarks,
        r.Items
            .OrderBy(i => i.Equipment.Category).ThenBy(i => i.Equipment.AssetTag)
            .Select(i => new ReservationItemDto(i.EquipmentId, i.Equipment.AssetTag, i.Equipment.Name,
                i.Equipment.Category, i.ReturnCondition))
            .ToList(),
        AllowedActionsFor(r));

    private List<ReservationAction> AllowedActionsFor(Reservation r)
    {
        var isOwner = r.RequestedById == currentUser.Id;
        var actions = new List<ReservationAction>();

        switch (r.Status)
        {
            case ReservationStatus.Pending when currentUser.IsAdmin:
                actions.AddRange([ReservationAction.Approve, ReservationAction.Reject, ReservationAction.Cancel]);
                break;
            case ReservationStatus.Pending when isOwner:
                actions.Add(ReservationAction.Cancel);
                break;
            case ReservationStatus.Approved:
                if (currentUser.IsAdmin) actions.Add(ReservationAction.Release);
                if (currentUser.IsAdmin || isOwner) actions.Add(ReservationAction.Cancel);
                break;
            case ReservationStatus.Borrowed when currentUser.IsAdmin:
                actions.Add(ReservationAction.Receive);
                break;
        }

        return actions;
    }
}
