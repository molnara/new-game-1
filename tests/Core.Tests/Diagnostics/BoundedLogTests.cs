using NewGame1.Core.Diagnostics;
using Shouldly;

namespace NewGame1.Core.Tests.Diagnostics;

public class BoundedLogTests
{
    [Fact]
    public void CapacityIsFixedAtConstruction()
    {
        var log = new BoundedLog(3);

        log.Capacity.ShouldBe(3);
    }

    [Fact]
    public void CapacityMustBeGreaterThanZero()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BoundedLog(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new BoundedLog(-1));
    }

    [Fact]
    public void EntriesAreReadOldestFirst()
    {
        var log = new BoundedLog(5);

        log.Add("one");
        log.Add("two");
        log.Add("three");

        log.Entries.ShouldBe(new[] { "one", "two", "three" });
    }

    [Fact]
    public void AppendingAtCapacityDropsTheOldest()
    {
        var log = new BoundedLog(2);

        log.Add("one");
        log.Add("two");
        log.Add("three");

        log.Entries.ShouldBe(new[] { "two", "three" });
    }
}
