using BorrowingSystem.Api.Common;

namespace BorrowingSystem.Api.Domain;

/// <summary>
/// One physical, tagged unit (e.g. "PRJ-002, Epson projector"). Tracking units instead of
/// quantities lets the office answer "which projector did Science borrow?" and lets a damaged
/// unit be pulled from circulation without affecting the others.
/// </summary>
public sealed class Equipment : Entity
{
    private Equipment() { } // EF Core

    public Equipment(string assetTag, string name, string category, string? description, DateTime createdAt)
    {
        UpdateDetails(assetTag, name, category, description);
        Condition = EquipmentCondition.Good;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public string AssetTag { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public string? Description { get; private set; }
    public EquipmentCondition Condition { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>Can this unit be handed out at all, ignoring the schedule?</summary>
    public bool IsServiceable => IsActive && Condition == EquipmentCondition.Good;

    public void UpdateDetails(string assetTag, string name, string category, string? description)
    {
        if (string.IsNullOrWhiteSpace(assetTag)) throw new DomainException("Asset tag is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Equipment name is required.");
        if (string.IsNullOrWhiteSpace(category)) throw new DomainException("Category is required.");

        AssetTag = assetTag.Trim().ToUpperInvariant();
        Name = name.Trim();
        Category = category.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetCondition(EquipmentCondition condition) => Condition = condition;

    /// <summary>Records what the custodian found when the unit came back.</summary>
    public void ApplyReturnCondition(ReturnCondition returned) => Condition = returned switch
    {
        ReturnCondition.Damaged => EquipmentCondition.NeedsRepair,
        ReturnCondition.Missing => EquipmentCondition.Missing,
        _ => Condition
    };

    public void Retire() => IsActive = false;

    public void Reinstate() => IsActive = true;
}
