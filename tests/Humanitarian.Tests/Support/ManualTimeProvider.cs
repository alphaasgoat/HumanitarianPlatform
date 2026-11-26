namespace Humanitarian.Tests.Support;

/// <summary>A clock the test moves by hand (no sleeping in tests).</summary>
public class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
