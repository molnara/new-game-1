using NewGame1.Core.Diagnostics;
using Shouldly;

namespace NewGame1.Core.Tests.Diagnostics;

public class BoundedLogTests
{
    private static readonly string[] ExpectedThreeOldestFirst = ["one", "two", "three"];
    private static readonly string[] ExpectedAfterOldestDropped = ["two", "three"];

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

        log.Entries.ShouldBe(ExpectedThreeOldestFirst);
    }

    [Fact]
    public void AppendingAtCapacityDropsTheOldest()
    {
        var log = new BoundedLog(2);

        log.Add("one");
        log.Add("two");
        log.Add("three");

        log.Entries.ShouldBe(ExpectedAfterOldestDropped);
    }
}
