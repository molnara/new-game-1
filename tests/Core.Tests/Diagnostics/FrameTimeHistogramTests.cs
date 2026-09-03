using NewGame1.Core.Diagnostics;
using Shouldly;

namespace NewGame1.Core.Tests.Diagnostics;

public class FrameTimeHistogramTests
{
    [Fact]
    public void AverageIsExact()
    {
        var histogram = new FrameTimeHistogram();

        histogram.Add(10.0);
        histogram.Add(20.0);
        histogram.Add(30.0);

        histogram.Snapshot().AverageMs.ShouldBe(20.0);
    }

    [Fact]
    public void WorstFrameIsExactNotBucketed()
    {
        var histogram = new FrameTimeHistogram();

        histogram.Add(16.001);
        histogram.Add(16.037);
        histogram.Add(16.002);

        histogram.Snapshot().WorstMs.ShouldBe(16.037);
    }

    [Fact]
    public void PercentilesAreAccurateToBucketWidth()
    {
        var histogram = new FrameTimeHistogram();

        // 100 samples: 95 at 10ms, 5 at 50ms. p95 sits right at the boundary between the two groups.
        for (var i = 0; i < 95; i++)
        {
            histogram.Add(10.0);
        }

        for (var i = 0; i < 5; i++)
        {
            histogram.Add(50.0);
        }

        var stats = histogram.Snapshot();

        stats.P95Ms.ShouldNotBeNull().ShouldBeInRange(9.9, 10.1);
        stats.P99Ms.ShouldNotBeNull().ShouldBeInRange(9.9, 10.1);
    }

    [Fact]
    public void SampleAboveTopBucketLandsInOverflowAndStillUpdatesWorstExactly()
    {
        var histogram = new FrameTimeHistogram();

        histogram.Add(10.0);
        histogram.Add(5000.0); // catastrophic stall, far above the ~100ms top bucket

        var stats = histogram.Snapshot();

        stats.WorstMs.ShouldBe(5000.0);
        stats.SampleCount.ShouldBe(2);
    }

    [Fact]
    public void NegativeSamplesAreRejected()
    {
        var histogram = new FrameTimeHistogram();

        histogram.Add(-1.0);

        histogram.Snapshot().SampleCount.ShouldBe(0);
    }

    [Fact]
    public void NaNSamplesAreRejected()
    {
        var histogram = new FrameTimeHistogram();

        histogram.Add(double.NaN);

        histogram.Snapshot().SampleCount.ShouldBe(0);
    }

    [Fact]
    public void AddIsAllocationFreeRegardlessOfHowManySamplesHaveAccumulated()
    {
        // Allocation-free Add is what makes memory use fixed for the life of the process
        // (FR-045a): if adding a sample never allocates, accumulated memory cannot grow with
        // session length, however long the session runs.
        var histogram = new FrameTimeHistogram();
        histogram.Add(16.0); // warm up (JIT, any one-time lazy init)

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++)
        {
            histogram.Add(16.0 + (i % 1000) * 0.01);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
    }
}
