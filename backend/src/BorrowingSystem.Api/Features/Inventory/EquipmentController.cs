using BorrowingSystem.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BorrowingSystem.Api.Features.Inventory;

[ApiController]
[Authorize]
[Route("api/equipment")]
public sealed class EquipmentController(EquipmentService equipment) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<EquipmentDto>> List([FromQuery] EquipmentQuery query, CancellationToken ct) =>
        equipment.ListAsync(query, ct);

    [HttpGet("categories")]
    public Task<IReadOnlyList<string>> Categories(CancellationToken ct) => equipment.ListCategoriesAsync(ct);

    [HttpGet("availability")]
    public Task<IReadOnlyList<AvailabilityDto>> Availability(
        [FromQuery] DateTime start, [FromQuery] DateTime end, CancellationToken ct) =>
        equipment.GetAvailabilityAsync(new DateRange(start.ToUniversalTime(), end.ToUniversalTime()), ct);

    [HttpGet("{id:int}")]
    public Task<EquipmentDetailDto> Get(int id, CancellationToken ct) => equipment.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public Task<EquipmentDto> Create(SaveEquipmentRequest request, CancellationToken ct) =>
        equipment.CreateAsync(request, ct);

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public Task<EquipmentDto> Update(int id, SaveEquipmentRequest request, CancellationToken ct) =>
        equipment.UpdateAsync(id, request, ct);
}
