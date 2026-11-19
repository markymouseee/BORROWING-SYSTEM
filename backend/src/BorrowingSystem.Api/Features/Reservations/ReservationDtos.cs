using System.ComponentModel.DataAnnotations;
using BorrowingSystem.Api.Domain;

namespace BorrowingSystem.Api.Features.Reservations;

/// <summary>What the signed-in user may do next. Computed on the server so the UI never re-implements the rules.</summary>
public enum ReservationAction
{
    Approve,
    Reject,
    Cancel,
    Release,
    Receive
}

public sealed record ReservationSummaryDto(
    int Id,
    string Code,
    string EventName,
    string? Venue,
    string DepartmentCode,
    string RequestedByName,
    DateTime StartAt,
    DateTime EndAt,
    ReservationStatus Status,
    bool IsOverdue,
    IReadOnlyList<string> AssetTags,
    DateTime CreatedAt);

public sealed record ReservationItemDto(
    int EquipmentId,
    string AssetTag,
    string Name,
    string Category,
    ReturnCondition? ReturnCondition);

public sealed record ReservationDetailDto(
    int Id,
    string Code,
    string EventName,
    string? Venue,
    string? Purpose,
    int DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    int RequestedById,
    string RequestedByName,
    DateTime StartAt,
    DateTime EndAt,
    ReservationStatus Status,
    bool IsOverdue,
    DateTime CreatedAt,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? RejectionReason,
    string? ReleasedByName,
    DateTime? BorrowedAt,
    string? ReceivedByName,
    DateTime? ReturnedAt,
    string? ReturnRemarks,
    IReadOnlyList<ReservationItemDto> Items,
    IReadOnlyList<ReservationAction> AllowedActions);

public sealed record ReservationQuery(
    ReservationStatus? Status,
    bool OverdueOnly = false,
    bool Mine = false,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed record CreateReservationRequest(
    [Required, StringLength(150)] string EventName,
    [StringLength(120)] string? Venue,
    [StringLength(500)] string? Purpose,
    int? DepartmentId,
    [Required] DateTime StartAt,
    [Required] DateTime EndAt,
    [Required, MinLength(1)] IReadOnlyList<int> EquipmentIds);

public sealed record RejectReservationRequest([Required, StringLength(300)] string Reason);

public sealed record ReturnItemRequest(int EquipmentId, ReturnCondition Condition);

public sealed record ReceiveReservationRequest(
    IReadOnlyList<ReturnItemRequest>? Items,
    [StringLength(500)] string? Remarks);
