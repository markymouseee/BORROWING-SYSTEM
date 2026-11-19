using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Data;
using BorrowingSystem.Api.Domain;
using BorrowingSystem.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BorrowingSystem.Api.Features.Users;

public sealed class UserService(AppDbContext db, IPasswordHasher<User> hasher, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Include(u => u.Department)
            .OrderBy(u => u.FullName).ToListAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        await EnsureDepartmentExistsAsync(request.DepartmentId, ct);

        var user = new User(request.FullName, request.Email, request.Role, request.DepartmentId);
        await EnsureEmailIsFreeAsync(user.Email, exceptId: null, ct);
        user.SetPasswordHash(hasher.HashPassword(user, request.Password));

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        await EnsureDepartmentExistsAsync(request.DepartmentId, ct);

        if (id == currentUser.Id && (!request.IsActive || request.Role != UserRole.Admin))
            throw new DomainException("You cannot deactivate or demote your own account.");

        user.UpdateProfile(request.FullName, request.Email, request.Role, request.DepartmentId);
        await EnsureEmailIsFreeAsync(user.Email, exceptId: id, ct);
        if (request.IsActive) user.Activate(); else user.Deactivate();

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task ResetPasswordAsync(int id, string newPassword, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        user.SetPasswordHash(hasher.HashPassword(user, newPassword));
        await db.SaveChangesAsync(ct);
    }

    private async Task<UserDto> GetAsync(int id, CancellationToken ct) =>
        (await db.Users.AsNoTracking().Include(u => u.Department).FirstAsync(u => u.Id == id, ct)).ToDto();

    private async Task<User> FindAsync(int id, CancellationToken ct) =>
        await db.Users.FindAsync([id], ct) ?? throw NotFoundException.For<User>(id);

    private async Task EnsureEmailIsFreeAsync(string email, int? exceptId, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != exceptId, ct))
            throw new ConflictException($"{email} already has an account.");
    }

    private async Task EnsureDepartmentExistsAsync(int? departmentId, CancellationToken ct)
    {
        if (departmentId is { } id && !await db.Departments.AnyAsync(d => d.Id == id, ct))
            throw new DomainException("The selected department does not exist.");
    }
}
