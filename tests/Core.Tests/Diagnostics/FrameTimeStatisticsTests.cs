using NewGame1.Core.Diagnostics;
using Shouldly;

namespace NewGame1.Core.Tests.Diagnostics;

public class FrameTimeStatisticsTests
{
    [Fact]
    public void EmptySnapshotIsConstructibleAndLowConfidence()
    {
        var stats = new FrameTimeHistogram().Snapshot();

        stats.SampleCount.ShouldBe(0);
        stats.IsLowConfidence.ShouldBeTrue();
    }

    [Fact]
    public void EmptySnapshotReportsPercentilesAsAbsentRatherThanZero()
    {
        var stats = new FrameTimeHistogram().Snapshot();

        stats.P95Ms.ShouldBeNull();
        stats.P99Ms.ShouldBeNull();
    }

    [Fact]
    public void IsLowConfidenceBelowOneThousandSamples()
    {
        var histogram = new FrameTimeHistogram();
        for (var i = 0; i < 999; i++)
        {
            histogram.Add(16.0);
        }

        histogram.Snapshot().IsLowConfidence.ShouldBeTrue();
    }

    [Fact]
    public void IsNotLowConfidenceAtOneThousandSamples()
    {
        var histogram = new FrameTimeHistogram();
        for (var i = 0; i < 1000; i++)
        {
            histogram.Add(16.0);
        }

        histogram.Snapshot().IsLowConfidence.ShouldBeFalse();
    }

    [Fact]
    public void SampleCountIsCarriedOnTheRecord()
    {
        var histogram = new FrameTimeHistogram();
        for (var i = 0; i < 42; i++)
        {
            histogram.Add(16.0);
        }

        histogram.Snapshot().SampleCount.ShouldBe(42);
    }

    [Fact]
    public void KindDistinguishesInterimFromFinal()
    {
        var histogram = new FrameTimeHistogram();
        histogram.Add(16.0);

        var interim = histogram.Snapshot(FrameTimeStatisticsKind.Interim);
        var final = histogram.Snapshot(FrameTimeStatisticsKind.Final);

        interim.Kind.ShouldBe(FrameTimeStatisticsKind.Interim);
        final.Kind.ShouldBe(FrameTimeStatisticsKind.Final);
    }

    [Fact]
    public void KindDefaultsToInterim()
    {
        var stats = new FrameTimeHistogram().Snapshot();

        stats.Kind.ShouldBe(FrameTimeStatisticsKind.Interim);
    }
}
