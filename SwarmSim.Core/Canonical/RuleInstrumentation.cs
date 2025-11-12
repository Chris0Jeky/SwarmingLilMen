namespace SwarmSim.Core.Canonical;

public sealed class RuleInstrumentation
{
    private readonly int[] _neighborCounts;
    private readonly float[] _neighborWeightSums;
    private readonly float[] _separationMagnitudes;
    private readonly float[] _alignmentMagnitudes;
    private readonly float[] _cohesionMagnitudes;
    private int _activeCount;

    public RuleInstrumentation(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be positive", nameof(capacity));

        _neighborCounts = new int[capacity];
        _neighborWeightSums = new float[capacity];
        _separationMagnitudes = new float[capacity];
        _alignmentMagnitudes = new float[capacity];
        _cohesionMagnitudes = new float[capacity];
    }

    internal void Prepare(int count)
    {
        _activeCount = Math.Min(count, _neighborCounts.Length);
        Array.Clear(_neighborCounts, 0, _activeCount);
        Array.Clear(_neighborWeightSums, 0, _activeCount);
        Array.Clear(_separationMagnitudes, 0, _activeCount);
        Array.Clear(_alignmentMagnitudes, 0, _activeCount);
        Array.Clear(_cohesionMagnitudes, 0, _activeCount);
    }

    internal void SetNeighborCount(int index, int value)
    {
        if (index < _activeCount)
            _neighborCounts[index] = value;
    }

    internal void SetNeighborWeightSum(int index, float value)
    {
        if (index < _activeCount)
            _neighborWeightSums[index] = value;
    }

    internal void RecordSeparation(int index, float magnitude)
    {
        if (index < _activeCount)
            _separationMagnitudes[index] = magnitude;
    }

    internal void RecordAlignment(int index, float magnitude)
    {
        if (index < _activeCount)
            _alignmentMagnitudes[index] = magnitude;
    }

    internal void RecordCohesion(int index, float magnitude)
    {
        if (index < _activeCount)
            _cohesionMagnitudes[index] = magnitude;
    }

    public ReadOnlySpan<int> NeighborCounts => _neighborCounts.AsSpan(0, _activeCount);

    public float AverageNeighborWeight
    {
        get
        {
            if (_activeCount == 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < _activeCount; i++)
            {
                total += _neighborWeightSums[i];
            }
            return total / _activeCount;
        }
    }

    public (int Min, int Max, float Avg) NeighborCountStats
    {
        get
        {
            if (_activeCount == 0)
                return (0, 0, 0f);

            int min = int.MaxValue;
            int max = int.MinValue;
            int total = 0;
            for (int i = 0; i < _activeCount; i++)
            {
                int value = _neighborCounts[i];
                min = Math.Min(min, value);
                max = Math.Max(max, value);
                total += value;
            }

            return (min, max, (float)total / _activeCount);
        }
    }

    public float AverageSeparationMagnitude => ComputeAverage(_separationMagnitudes);
    public float AverageAlignmentMagnitude => ComputeAverage(_alignmentMagnitudes);
    public float AverageCohesionMagnitude => ComputeAverage(_cohesionMagnitudes);

    private float ComputeAverage(float[] buffer)
    {
        if (_activeCount == 0)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < _activeCount; i++)
        {
            sum += buffer[i];
        }

        return sum / _activeCount;
    }
}
