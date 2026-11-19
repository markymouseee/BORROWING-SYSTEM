using BorrowingSystem.Api.Common;

namespace BorrowingSystem.Api.Domain;

/// <summary>
/// A request to use one or more equipment units for an event. This is the aggregate root:
/// every status change goes through a method here so the lifecycle rules live in one place.
///
///   Pending ──approve──▶ Approved ──release──▶ Borrowed ──receive──▶ Returned
///      │                    │
///      ├──reject──▶ Rejected│
///      └──cancel──▶ Cancelled ◀──cancel──┘
/// </summary>
public sealed class Reservation : Entity
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(14);

    /// <summary>Statuses that hold equipment on the calendar and therefore block other bookings.</summary>
    public static readonly IReadOnlyCollection<ReservationStatus> BlockingStatuses =
        [ReservationStatus.Pending, ReservationStatus.Approved, ReservationStatus.Borrowed];

    private readonly List<ReservationItem> _items = [];

    private Reservation() { } // EF Core

    private Reservation(User requestedBy, int departmentId, string eventName, string? venue,
        string? purpose, DateRange period, DateTime createdAt)
    {
        RequestedBy = requestedBy;
        RequestedById = requestedBy.Id;
        DepartmentId = departmentId;
        EventName = eventName.Trim();
        Venue = string.IsNullOrWhiteSpace(venue) ? null : venue.Trim();
        Purpose = string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim();
        StartAt = period.Start;
        EndAt = period.End;
        Status = ReservationStatus.Pending;
        CreatedAt = createdAt;
    }

    public string EventName { get; private set; } = null!;
    public string? Venue { get; private set; }
    public string? Purpose { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public int DepartmentId { get; private set; }
    public Department Department { get; private set; } = null!;
    public int RequestedById { get; private set; }
    public User RequestedBy { get; private set; } = null!;

    public int? ReviewedById { get; private set; }
    public User? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    public int? ReleasedById { get; private set; }
    public User? ReleasedBy { get; private set; }
    public DateTime? BorrowedAt { get; private set; }

    public int? ReceivedById { get; private set; }
    public User? ReceivedBy { get; private set; }
    public DateTime? ReturnedAt { get; private set; }
    public string? ReturnRemarks { get; private set; }

    public IReadOnlyCollection<ReservationItem> Items => _items;

    public DateRange Period => new(StartAt, EndAt);

    public string Code => FormatCode(Id);

    public static string FormatCode(int id) => $"RSV-{id:D5}";

    public bool IsOverdue(DateTime now) => Status == ReservationStatus.Borrowed && now > EndAt;

    public static Reservation Create(User requestedBy, int departmentId, string eventName, string? venue,
        string? purpose, DateRange period, IReadOnlyCollection<Equipment> equipment, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new DomainException("Event name is required.");
        if (period.End <= now)
            throw new DomainException("The schedule has already passed.");
        if (period.Duration > MaxDuration)
            throw new DomainException($"A reservation can last at most {MaxDuration.TotalDays:0} days.");
        if (equipment.Count == 0)
            throw new DomainException("Select at least one piece of equipment.");

        var unusable = equipment.Where(e => !e.IsServiceable).Select(e => e.AssetTag).ToList();
        if (unusable.Count > 0)
            throw new DomainException($"Not available for borrowing: {string.Join(", ", unusable)}.");

        var reservation = new Reservation(requestedBy, departmentId, eventName, venue, purpose, period, now);
        foreach (var unit in equipment.DistinctBy(e => e.Id))
            reservation._items.Add(new ReservationItem(unit));

        return reservation;
    }

    public void Approve(User reviewer, DateTime now)
    {
        EnsureStatus("approve", ReservationStatus.Pending);
        Status = ReservationStatus.Approved;
        ReviewedBy = reviewer;
        ReviewedAt = now;
    }

    public void Reject(User reviewer, string reason, DateTime now)
    {
        EnsureStatus("reject", ReservationStatus.Pending);
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Please give a reason for rejecting the request.");

        Status = ReservationStatus.Rejected;
        RejectionReason = reason.Trim();
        ReviewedBy = reviewer;
        ReviewedAt = now;
    }

    public void Cancel()
    {
        EnsureStatus("cancel", ReservationStatus.Pending, ReservationStatus.Approved);
        Status = ReservationStatus.Cancelled;
    }

    /// <summary>The custodian hands the equipment over to the borrower.</summary>
    public void Release(User custodian, DateTime now)
    {
        EnsureStatus("release", ReservationStatus.Approved);

        var unusable = _items.Where(i => !i.Equipment.IsServiceable).Select(i => i.Equipment.AssetTag).ToList();
        if (unusable.Count > 0)
            throw new DomainException(
                $"Cannot release: {string.Join(", ", unusable)} is no longer serviceable. Update the request first.");

        Status = ReservationStatus.Borrowed;
        ReleasedBy = custodian;
        BorrowedAt = now;
    }

    /// <summary>The custodian receives the equipment back and notes the condition of each unit.</summary>
    public void Receive(User custodian, IReadOnlyDictionary<int, ReturnCondition> conditionByEquipmentId,
        string? remarks, DateTime now)
    {
        EnsureStatus("return", ReservationStatus.Borrowed);

        foreach (var item in _items)
        {
            // Anything the custodian did not flag is assumed to have come back in good shape.
            var condition = conditionByEquipmentId.GetValueOrDefault(item.EquipmentId, ReturnCondition.Good);
            item.RecordReturn(condition);
        }

        Status = ReservationStatus.Returned;
        ReceivedBy = custodian;
        ReturnedAt = now;
        ReturnRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
    }

    private void EnsureStatus(string action, params ReservationStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new DomainException($"Cannot {action} a reservation that is {Status.ToString().ToLowerInvariant()}.");
    }
}
