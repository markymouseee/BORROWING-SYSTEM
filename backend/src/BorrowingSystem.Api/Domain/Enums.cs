namespace BorrowingSystem.Api.Domain;

public enum UserRole
{
    /// <summary>Property custodian / office staff: manages inventory, approves, releases and receives.</summary>
    Admin,

    /// <summary>Teacher or department staff: browses availability and reserves equipment.</summary>
    Faculty
}

public enum ReservationStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Borrowed,
    Returned
}

/// <summary>Physical condition recorded on the equipment itself.</summary>
public enum EquipmentCondition
{
    Good,
    NeedsRepair,
    Missing
}

/// <summary>Condition of a unit as checked by the custodian when it comes back.</summary>
public enum ReturnCondition
{
    Good,
    Damaged,
    Missing
}

/// <summary>Live status shown to users. Derived, never stored — see <see cref="EquipmentStatusResolver"/>.</summary>
public enum EquipmentStatus
{
    Available,
    Reserved,
    Borrowed,
    UnderRepair,
    Missing,
    Retired
}
