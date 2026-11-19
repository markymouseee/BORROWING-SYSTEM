using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Domain;

namespace BorrowingSystem.Tests;

public class DateRangeTests
{
    private static readonly DateTime Day = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static DateRange Hours(int from, int to) => new(Day.AddHours(from), Day.AddHours(to));

    [Theory]
    [InlineData(8, 12, 10, 14, true)]   // partial overlap
    [InlineData(8, 12, 9, 11, true)]    // fully inside
    [InlineData(8, 12, 12, 14, false)]  // back-to-back is allowed
    [InlineData(8, 12, 13, 15, false)]  // clearly apart
    public void Overlaps_detects_shared_time(int aStart, int aEnd, int bStart, int bEnd, bool expected)
    {
        var a = Hours(aStart, aEnd);
        var b = Hours(bStart, bEnd);

        Assert.Equal(expected, a.Overlaps(b));
        Assert.Equal(expected, b.Overlaps(a));
    }

    [Fact]
    public void End_must_be_after_start()
    {
        Assert.Throws<DomainException>(() => Hours(10, 10));
        Assert.Throws<DomainException>(() => Hours(10, 9));
    }
}
