using System.ComponentModel.DataAnnotations;
using BorrowingSystem.Api.Domain;

namespace BorrowingSystem.Api.Features.Inventory;

public sealed record BookingDto(
    int ReservationId,
    string EventName,
    string DepartmentCode,
    DateTime StartAt,
    DateTime EndAt,
    ReservationStatus Status);

public sealed record EquipmentDto(
    int Id,
    string AssetTag,
    string Name,
    string Category,
    string? Description,
    EquipmentCondition Condition,
    bool IsActive,
    EquipmentStatus Status,
    BookingDto? CurrentBooking);

public sealed record EquipmentDetailDto(
    EquipmentDto Equipment,
    IReadOnlyList<BookingDto> Upcoming,
    IReadOnlyList<BookingDto> History);

public sealed record AvailabilityDto(EquipmentDto Equipment, bool IsAvailable, BookingDto? Conflict);

public sealed record EquipmentQuery(string? Search, string? Category, EquipmentStatus? Status, bool IncludeRetired = false);

public sealed record SaveEquipmentRequest(
    [Required, StringLength(30)] string AssetTag,
    [Required, StringLength(120)] string Name,
    [Required, StringLength(60)] string Category,
    [StringLength(500)] string? Description,
    EquipmentCondition Condition = EquipmentCondition.Good,
    bool IsActive = true);

internal static class EquipmentMappings
{
    public static BookingDto ToDto(this Booking b) =>
        new(b.ReservationId, b.EventName, b.DepartmentCode, b.Period.Start, b.Period.End, b.Status);

    public static EquipmentDto ToDto(this Equipment e, EquipmentStatus status, Booking? currentBooking) =>
        new(e.Id, e.AssetTag, e.Name, e.Category, e.Description, e.Condition, e.IsActive, status,
            currentBooking?.ToDto());
}
