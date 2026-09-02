using NewGame1.Core.Diagnostics;
using Shouldly;

namespace NewGame1.Core.Tests.Diagnostics;

public class LogRetentionPolicyTests
{
    // Session log names are sortable by their embedded timestamp: session-<yyyyMMddTHHmmssfff>.log
    private static string Session(string timestamp) => $"session-{timestamp}.log";

    [Fact]
    public void KeepsNewestAndReturnsOlderForDeletion()
    {
        var existing = new[]
        {
            Session("20260101T000000000"),
            Session("20260102T000000000"),
            Session("20260103T000000000"),
        };

        var toDelete = LogRetentionPolicy.SelectForDeletion(existing, keep: 2);

        toDelete.ShouldBe(new[] { Session("20260101T000000000") });
    }

    [Fact]
    public void OrdersByTimestampEmbeddedInNameNotByInputOrder()
    {
        var existing = new[]
        {
            Session("20260103T000000000"),
            Session("20260101T000000000"),
            Session("20260102T000000000"),
        };

        var toDelete = LogRetentionPolicy.SelectForDeletion(existing, keep: 1);

        toDelete.ShouldBe(new[]
        {
            Session("20260101T000000000"),
            Session("20260102T000000000"),
        });
    }

    [Fact]
    public void DefaultsKeepToTen()
    {
        var existing = Enumerable.Range(1, 11)
            .Select(i => Session($"202601{i:D2}T000000000"))
            .ToArray();

        var toDelete = LogRetentionPolicy.SelectForDeletion(existing);

        toDelete.ShouldBe(new[] { Session("202601" + "01" + "T000000000") });
    }

    [Fact]
    public void NothingToDeleteWhenAtOrBelowKeep()
    {
        var existing = new[]
        {
            Session("20260101T000000000"),
            Session("20260102T000000000"),
        };

        LogRetentionPolicy.SelectForDeletion(existing, keep: 2).ShouldBeEmpty();
        LogRetentionPolicy.SelectForDeletion(existing, keep: 5).ShouldBeEmpty();
    }

    [Fact]
    public void NeverReturnsGodotsOwnLogFile()
    {
        var existing = new[]
        {
            "godot.log",
            Session("20260101T000000000"),
            Session("20260102T000000000"),
            Session("20260103T000000000"),
        };

        var toDelete = LogRetentionPolicy.SelectForDeletion(existing, keep: 1);

        toDelete.ShouldNotContain("godot.log");
        toDelete.ShouldBe(new[] { Session("20260101T000000000"), Session("20260102T000000000") });
    }

    [Fact]
    public void NeverReturnsFilesNotMatchingTheSessionLogPattern()
    {
        var existing = new[]
        {
            "godot.log",
            "notes.txt",
            "session-broken.log",
            Session("20260101T000000000"),
        };

        var toDelete = LogRetentionPolicy.SelectForDeletion(existing, keep: 0);

        toDelete.ShouldBe(new[] { Session("20260101T000000000") });
    }

    [Fact]
    public void MatchesNamesCarryingAProcessIdDisambiguator()
    {
        var existing = new[]
        {
            "session-20260101T000000000-1234.log",
            "session-20260102T000000000-5678.log",
        };

        var toDelete = LogRetentionPolicy.SelectForDeletion(existing, keep: 1);

        var expected = new[] { "session-20260101T000000000-1234.log" };
        toDelete.ShouldBe(expected);
    }

    [Fact]
    public void EmptyInputProducesNoDeletions()
    {
        LogRetentionPolicy.SelectForDeletion(Array.Empty<string>()).ShouldBeEmpty();
    }

    [Fact]
    public void ExcludesASessionLogWhoseOwningProcessIsStillAlive()
    {
        var existing = new[]
        {
            "session-20260101T000000000-111.log",
            "session-20260102T000000000-222.log",
            "session-20260103T000000000-333.log",
        };

        var toDelete = LogRetentionPolicy.SelectForDeletion(existing, keep: 1, isProcessAlive: pid => pid == 111);

        toDelete.ShouldBe(new[] { "session-20260102T000000000-222.log" });
    }

    [Fact]
    public void NamesWithoutAProcessIdAreEligibleRegardlessOfPredicate()
    {
        var existing = new[]
        {
            "session-20260101T000000000.log",
            "session-20260102T000000000-222.log",
        };

        var toDelete = LogRetentionPolicy.SelectForDeletion(existing, keep: 0, isProcessAlive: _ => true);

        toDelete.ShouldBe(new[] { "session-20260101T000000000.log" });
    }

    [Fact]
    public void NullPredicateNeverExcludesAnyCandidate()
    {
        var existing = new[]
        {
            "session-20260101T000000000-111.log",
            "session-20260102T000000000-222.log",
        };

        var toDelete = LogRetentionPolicy.SelectForDeletion(existing, keep: 0, isProcessAlive: null);

        toDelete.ShouldBe(existing);
    }
}
