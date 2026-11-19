namespace BorrowingSystem.Api.Common;

/// <summary>Base type for anything persisted with its own identity.</summary>
public abstract class Entity
{
    public int Id { get; protected set; }
}
