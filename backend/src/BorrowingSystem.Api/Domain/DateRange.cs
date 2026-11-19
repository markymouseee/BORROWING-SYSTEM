using BorrowingSystem.Api.Common;

namespace BorrowingSystem.Api.Domain;

/// <summary>
/// A half-open time interval [Start, End). Two bookings conflict exactly when their ranges overlap,
/// so back-to-back bookings (one ends at 10:00, the next starts at 10:00) are allowed.
/// </summary>
public readonly record struct DateRange
{
    public DateTime Start { get; }
    public DateTime End { get; }

    public DateRange(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new DomainException("The end of the schedule must be after its start.");

        Start = start;
        End = end;
    }

    public TimeSpan Duration => End - Start;

    public bool Overlaps(DateRange other) => Start < other.End && other.Start < End;

    public bool Contains(DateTime instant) => Start <= instant && instant < End;
}
