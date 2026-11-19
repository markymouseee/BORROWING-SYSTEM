using BorrowingSystem.Api.Common;

namespace BorrowingSystem.Api.Domain;

public sealed class Department : Entity
{
    private Department() { } // EF Core

    public Department(string code, string name)
    {
        Update(code, name);
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    public void Update(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("Department code is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Department name is required.");

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
    }
}
