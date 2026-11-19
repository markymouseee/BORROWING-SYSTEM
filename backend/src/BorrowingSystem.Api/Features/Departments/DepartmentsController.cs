using System.ComponentModel.DataAnnotations;
using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Data;
using BorrowingSystem.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BorrowingSystem.Api.Features.Departments;

public sealed record DepartmentDto(int Id, string Code, string Name);

public sealed record SaveDepartmentRequest(
    [Required, StringLength(20)] string Code,
    [Required, StringLength(120)] string Name);

/// <summary>Plain reference data — small enough that a separate service class would add nothing.</summary>
[ApiController]
[Authorize]
[Route("api/departments")]
public sealed class DepartmentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<DepartmentDto>> List(CancellationToken ct) =>
        await db.Departments.AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Code, d.Name))
            .ToListAsync(ct);

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<DepartmentDto>> Create(SaveDepartmentRequest request, CancellationToken ct)
    {
        var department = new Department(request.Code, request.Name);
        await EnsureCodeIsFreeAsync(department.Code, exceptId: null, ct);

        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return Ok(new DepartmentDto(department.Id, department.Code, department.Name));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<DepartmentDto> Update(int id, SaveDepartmentRequest request, CancellationToken ct)
    {
        var department = await db.Departments.FindAsync([id], ct) ?? throw NotFoundException.For<Department>(id);
        department.Update(request.Code, request.Name);
        await EnsureCodeIsFreeAsync(department.Code, exceptId: id, ct);

        await db.SaveChangesAsync(ct);
        return new DepartmentDto(department.Id, department.Code, department.Name);
    }

    private async Task EnsureCodeIsFreeAsync(string code, int? exceptId, CancellationToken ct)
    {
        if (await db.Departments.AnyAsync(d => d.Code == code && d.Id != exceptId, ct))
            throw new ConflictException($"Department code {code} is already in use.");
    }
}
