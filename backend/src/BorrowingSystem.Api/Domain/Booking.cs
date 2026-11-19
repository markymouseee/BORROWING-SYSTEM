namespace BorrowingSystem.Api.Domain;

/// <summary>
/// A read-only projection: "this unit is held by that reservation during this period".
/// It is what the scheduling logic works with, so it never needs to load whole reservations.
/// </summary>
public sealed record Booking(
    int EquipmentId,
    int ReservationId,
    string EventName,
    string DepartmentCode,
    DateRange Period,
    ReservationStatus Status);
