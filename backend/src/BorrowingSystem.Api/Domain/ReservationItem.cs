namespace BorrowingSystem.Api.Domain;

/// <summary>A single equipment unit inside a reservation.</summary>
public sealed class ReservationItem
{
    private ReservationItem() { } // EF Core

    internal ReservationItem(Equipment equipment)
    {
        Equipment = equipment;
        EquipmentId = equipment.Id;
    }

    public int ReservationId { get; private set; }
    public Reservation Reservation { get; private set; } = null!;
    public int EquipmentId { get; private set; }
    public Equipment Equipment { get; private set; } = null!;
    public ReturnCondition? ReturnCondition { get; private set; }

    internal void RecordReturn(ReturnCondition condition)
    {
        ReturnCondition = condition;
        Equipment.ApplyReturnCondition(condition);
    }
}
