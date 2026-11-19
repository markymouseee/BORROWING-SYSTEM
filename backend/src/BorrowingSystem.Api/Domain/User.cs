using BorrowingSystem.Api.Common;

namespace BorrowingSystem.Api.Domain;

public sealed class User : Entity
{
    private User() { } // EF Core

    public User(string fullName, string email, UserRole role, int? departmentId)
    {
        UpdateProfile(fullName, email, role, departmentId);
        IsActive = true;
    }

    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public int? DepartmentId { get; private set; }
    public Department? Department { get; private set; }
    public bool IsActive { get; private set; }

    public bool IsAdmin => Role == UserRole.Admin;

    public void UpdateProfile(string fullName, string email, UserRole role, int? departmentId)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("Full name is required.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new DomainException("A valid email is required.");
        if (role == UserRole.Faculty && departmentId is null)
            throw new DomainException("Faculty accounts must belong to a department.");

        FullName = fullName.Trim();
        Email = NormalizeEmail(email);
        Role = role;
        DepartmentId = departmentId;
    }

    public void SetPasswordHash(string hash) => PasswordHash = hash;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
