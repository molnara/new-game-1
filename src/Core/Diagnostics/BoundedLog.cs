namespace NewGame1.Core.Diagnostics;

/// <summary>
/// Ring buffer backing console output history (FR-019). Appending past <see cref="Capacity"/>
/// drops the oldest entry.
/// </summary>
public sealed class BoundedLog
{
    private readonly Queue<string> _entries;

    public int Capacity { get; }

    public BoundedLog(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");
        }

        Capacity = capacity;
        _entries = new Queue<string>(capacity);
    }

    public IReadOnlyList<string> Entries => _entries.ToArray();

    public void Add(string entry)
    {
        if (_entries.Count == Capacity)
        {
            _entries.Dequeue();
        }

        _entries.Enqueue(entry);
    }
}
