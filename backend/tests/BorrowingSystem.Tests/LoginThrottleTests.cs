using BorrowingSystem.Api.Features.Auth;

namespace BorrowingSystem.Tests;

public class LoginThrottleTests
{
    private const string Email = "teacher@lcc.edu.ph";
    private const string Ip = "10.0.0.5";

    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero));
    private readonly LoginThrottle _throttle;

    public LoginThrottleTests() => _throttle = new LoginThrottle(_clock);

    private ThrottleStatus FailRound()
    {
        ThrottleStatus status = default;
        for (var i = 0; i < LoginThrottle.AttemptsPerRound; i++)
            status = _throttle.RegisterFailure(Email, Ip);
        return status;
    }

    [Fact]
    public void Counts_down_remaining_attempts()
    {
        Assert.Equal(4, _throttle.RegisterFailure(Email, Ip).AttemptsRemaining);
        Assert.Equal(3, _throttle.RegisterFailure(Email, Ip).AttemptsRemaining);
        Assert.False(_throttle.Check(Email, Ip).IsLocked);
    }

    [Fact]
    public void Lockouts_escalate_1_then_5_then_10_minutes_and_stay_at_10()
    {
        var expected = new[] { 1, 5, 10, 10 };

        foreach (var minutes in expected)
        {
            var status = FailRound();
            Assert.True(status.IsLocked);
            Assert.Equal(TimeSpan.FromMinutes(minutes), status.RetryAfter);

            _clock.Advance(TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(1));
            Assert.False(_throttle.Check(Email, Ip).IsLocked);
        }
    }

    [Fact]
    public void Lock_counts_down_in_real_time()
    {
        FailRound();
        _clock.Advance(TimeSpan.FromSeconds(20));

        var status = _throttle.Check(Email, Ip);
        Assert.True(status.IsLocked);
        Assert.Equal(TimeSpan.FromSeconds(40), status.RetryAfter);
    }

    [Fact]
    public void Success_resets_the_escalation()
    {
        FailRound();
        _clock.Advance(TimeSpan.FromMinutes(2));
        _throttle.RegisterSuccess(Email, Ip);

        Assert.Equal(TimeSpan.FromMinutes(1), FailRound().RetryAfter);
    }

    [Fact]
    public void Another_computer_is_not_locked_out()
    {
        FailRound();
        Assert.False(_throttle.Check(Email, "10.0.0.99").IsLocked);
    }

    [Fact]
    public void A_quiet_day_forgets_old_failures()
    {
        FailRound();
        _clock.Advance(TimeSpan.FromHours(25));

        Assert.Equal(TimeSpan.FromMinutes(1), FailRound().RetryAfter);
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
