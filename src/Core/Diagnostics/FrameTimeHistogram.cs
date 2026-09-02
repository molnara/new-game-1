namespace NewGame1.Core.Diagnostics;

/// <summary>
/// Bounded-memory accumulator for a whole session's frame times (FR-041, FR-045a; research R12).
/// Fixed 0.1 ms buckets from 0 to ~100 ms plus one overflow bucket keep memory use constant for the
/// life of the process regardless of session length, at the cost of percentiles accurate only to
/// bucket width — average and worst frame stay exact.
/// </summary>
public sealed class FrameTimeHistogram
{
    /// <summary>Number of fixed-width buckets covering 0 to ~100 ms; one further overflow bucket sits above these.</summary>
    public const int BucketCount = 1000;

    private readonly long[] _buckets = new long[BucketCount + 1]; // + 1 overflow bucket

    /// <summary>Width of each bucket in milliseconds, and thus the precision of <see cref="Snapshot"/>'s percentiles.</summary>
    public double BucketWidthMs { get; } = 0.1;

    /// <summary>Total number of samples accepted by <see cref="Add"/> so far.</summary>
    public long Count { get; private set; }

    /// <summary>Running sum of all accepted samples in milliseconds, used to derive the average.</summary>
    public double SumMs { get; private set; }

    /// <summary>Exact largest sample accepted so far, in milliseconds, including samples in the overflow bucket.</summary>
    public double WorstMs { get; private set; }

    /// <summary>
    /// Increments the matching bucket, count, sum, and running maximum. Constant time, no
    /// allocation. A negative or NaN sample is rejected rather than recorded. A sample above the
    /// top bucket lands in the overflow bucket and still updates <see cref="WorstMs"/> exactly.
    /// </summary>
    public void Add(double frameMs)
    {
        if (double.IsNaN(frameMs) || frameMs < 0.0)
        {
            return;
        }

        _buckets[BucketIndex(frameMs)]++;
        Count++;
        SumMs += frameMs;

        if (frameMs > WorstMs)
        {
            WorstMs = frameMs;
        }
    }

    /// <summary>Produces a <see cref="FrameTimeStatistics"/> without mutating or resetting.</summary>
    public FrameTimeStatistics Snapshot(FrameTimeStatisticsKind kind = FrameTimeStatisticsKind.Interim)
    {
        var average = Count == 0 ? 0.0 : SumMs / Count;

        return new FrameTimeStatistics(
            AverageMs: average,
            P95Ms: Percentile(0.95),
            P99Ms: Percentile(0.99),
            WorstMs: WorstMs,
            SampleCount: Count,
            Kind: kind);
    }

    private double? Percentile(double p)
    {
        if (Count == 0)
        {
            return null;
        }

        var target = (long)Math.Floor(p * Count);
        long cumulative = 0;
        double? result = null;
        double? firstNonEmptyBucketValue = null;

        for (var i = 0; i < _buckets.Length; i++)
        {
            var bucketCount = _buckets[i];
            if (bucketCount == 0)
            {
                continue;
            }

            firstNonEmptyBucketValue ??= BucketValue(i);

            var newCumulative = cumulative + bucketCount;
            if (newCumulative > target)
            {
                break;
            }

            cumulative = newCumulative;
            result = BucketValue(i);
        }

        return result ?? firstNonEmptyBucketValue;
    }

    private int BucketIndex(double frameMs)
    {
        var index = (int)(frameMs / BucketWidthMs);
        return index >= BucketCount ? BucketCount : index;
    }

    private double BucketValue(int index) => index * BucketWidthMs;
}
