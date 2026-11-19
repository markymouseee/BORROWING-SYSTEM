using System.Collections.Concurrent;

namespace BorrowingSystem.Api.Features.Auth;

/// <summary>Result of asking whether a sign-in may be attempted right now.</summary>
public readonly record struct ThrottleStatus(bool IsLocked, TimeSpan RetryAfter, int AttemptsRemaining);

/// <summary>
/// Brute-force protection for the sign-in endpoint (OWASP ASVS 2.2.1).
///
/// Every <see cref="AttemptsPerRound"/> failed attempts trigger a lockout, and each lockout is longer
/// than the last: 1 min → 5 min → 10 min → 10 min … A successful sign-in, or a full day without
/// failures, resets the counter.
///
/// Attempts are keyed by <b>email + client IP</b>: someone guessing from one machine is stopped, but they
/// cannot lock a teacher out of their account from somewhere else. A separate per-IP rate limit in
/// Program.cs covers one machine spraying many different emails.
///
/// State is in memory: a restart clears it, which is acceptable for a single school server.
/// </summary>
public sealed class LoginThrottle(TimeProvider time)
{
    public const int AttemptsPerRound = 5;

    /// <summary>Lockout length after the 1st, 2nd and 3rd+ full rounds of failures.</summary>
    public static readonly TimeSpan[] LockoutSteps =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10)
    ];

    private static readonly TimeSpan ForgetAfter = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private int _writes;

    public ThrottleStatus Check(string email, string? ip)
    {
        var now = time.GetUtcNow();
        if (!_entries.TryGetValue(Key(email, ip), out var entry))
            return new ThrottleStatus(false, TimeSpan.Zero, AttemptsPerRound);

        lock (entry)
        {
            Expire(entry, now);
            return Status(entry, now);
        }
    }

    /// <summary>Records a failed attempt and returns the state the caller should report.</summary>
    public ThrottleStatus RegisterFailure(string email, string? ip)
    {
        var now = time.GetUtcNow();
        PruneOccasionally(now);
        var entry = _entries.GetOrAdd(Key(email, ip), _ => new Entry());

        lock (entry)
        {
            Expire(entry, now);
            entry.LastFailure = now;
            entry.Failures++;

            if (entry.Failures >= AttemptsPerRound)
            {
                var step = Math.Min(entry.Lockouts, LockoutSteps.Length - 1);
                entry.LockedUntil = now + LockoutSteps[step];
                entry.Lockouts++;
                entry.Failures = 0;
            }

            return Status(entry, now);
        }
    }

    public void RegisterSuccess(string email, string? ip) => _entries.TryRemove(Key(email, ip), out _);

    private static ThrottleStatus Status(Entry entry, DateTimeOffset now) =>
        entry.LockedUntil is { } until && until > now
            ? new ThrottleStatus(true, until - now, 0)
            : new ThrottleStatus(false, TimeSpan.Zero, AttemptsPerRound - entry.Failures);

    private static void Expire(Entry entry, DateTimeOffset now)
    {
        var stillLocked = entry.LockedUntil > now;
        if (entry.LastFailure is { } last && now - last > ForgetAfter && !stillLocked)
        {
            entry.Failures = 0;
            entry.Lockouts = 0;
            entry.LockedUntil = null;
        }
    }

    /// <summary>Stops the table growing forever when someone tries thousands of made-up emails.</summary>
    private void PruneOccasionally(DateTimeOffset now)
    {
        if (Interlocked.Increment(ref _writes) % 500 != 0) return;
        foreach (var (key, entry) in _entries)
        {
            if (entry.LastFailure is { } last && now - last > ForgetAfter && !(entry.LockedUntil > now))
                _entries.TryRemove(key, out _);
        }
    }

    private static string Key(string email, string? ip) => $"{email.Trim().ToLowerInvariant()}|{ip ?? "unknown"}";

    private sealed class Entry
    {
        public int Failures;
        public int Lockouts;
        public DateTimeOffset? LockedUntil;
        public DateTimeOffset? LastFailure;
    }
}
