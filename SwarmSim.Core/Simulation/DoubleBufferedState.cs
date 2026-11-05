namespace SwarmSim.Core.Simulation;

/// <summary>
/// Two reusable generations of typed state. Construction copies the initial values; no buffer
/// allocation or copy occurs during commit. A compute phase reads Current and writes every Next
/// slot before committing. Next is not cleared: after commit it holds the preceding generation.
/// Spans are borrowed until the next commit; retain stable IDs, not a span across ticks.
/// This container does not deep-copy references inside T or provide thread synchronization.
/// </summary>
public sealed class DoubleBufferedState<T> where T : struct
{
    private T[] _current;
    private T[] _next;

    public DoubleBufferedState(ReadOnlySpan<T> initialState)
        : this(initialState.ToArray(), new T[initialState.Length]) { }

    private DoubleBufferedState(T[] current, T[] next)
    {
        _current = current;
        _next = next;
    }

    /// <summary>For freshly constructed, model-owned arrays whose references never escape.</summary>
    internal static DoubleBufferedState<T> TakeOwnership(T[] initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        return new DoubleBufferedState<T>(initialState, new T[initialState.Length]);
    }

    public int Count => _current.Length;
    public ReadOnlySpan<T> Current => _current;
    public Span<T> Next => _next;

    /// <summary>Publishes the complete next generation, reusing the old buffer for later writes.</summary>
    public void Commit() => (_current, _next) = (_next, _current);
}
