using BorrowingSystem.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BorrowingSystem.Api.Features.Reservations;

[ApiController]
[Authorize]
[Route("api/reservations")]
public sealed class ReservationsController(ReservationService reservations) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ReservationSummaryDto>> List([FromQuery] ReservationQuery query, CancellationToken ct) =>
        reservations.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<ReservationDetailDto> Get(int id, CancellationToken ct) => reservations.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<ReservationDetailDto>> Create(CreateReservationRequest request, CancellationToken ct)
    {
        var created = await reservations.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/approve")]
    public Task<ReservationDetailDto> Approve(int id, CancellationToken ct) => reservations.ApproveAsync(id, ct);

    [HttpPost("{id:int}/reject")]
    public Task<ReservationDetailDto> Reject(int id, RejectReservationRequest request, CancellationToken ct) =>
        reservations.RejectAsync(id, request.Reason, ct);

    [HttpPost("{id:int}/cancel")]
    public Task<ReservationDetailDto> Cancel(int id, CancellationToken ct) => reservations.CancelAsync(id, ct);

    /// <summary>Equipment handed to the borrower.</summary>
    [HttpPost("{id:int}/release")]
    public Task<ReservationDetailDto> Release(int id, CancellationToken ct) => reservations.ReleaseAsync(id, ct);

    /// <summary>Equipment received back by the custodian.</summary>
    [HttpPost("{id:int}/receive")]
    public Task<ReservationDetailDto> Receive(int id, ReceiveReservationRequest request, CancellationToken ct) =>
        reservations.ReceiveAsync(id, request, ct);
}
