using System.ComponentModel.DataAnnotations;
using BorrowingSystem.Api.Domain;

namespace BorrowingSystem.Api.Features.Users;

public sealed record UserDto(
    int Id,
    string FullName,
    string Email,
    UserRole Role,
    int? DepartmentId,
    string? DepartmentName,
    bool IsActive);

public sealed record CreateUserRequest(
    [Required, StringLength(120)] string FullName,
    [Required, EmailAddress, StringLength(160)] string Email,
    [Required] UserRole Role,
    int? DepartmentId,
    [Required, MinLength(8)] string Password);

public sealed record UpdateUserRequest(
    [Required, StringLength(120)] string FullName,
    [Required, EmailAddress, StringLength(160)] string Email,
    [Required] UserRole Role,
    int? DepartmentId,
    bool IsActive);

public sealed record ResetPasswordRequest([Required, MinLength(8)] string NewPassword);

public static class UserMappings
{
    public static UserDto ToDto(this User user) => new(
        user.Id, user.FullName, user.Email, user.Role,
        user.DepartmentId, user.Department?.Name, user.IsActive);
}
