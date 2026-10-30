using System;

namespace SwarmSim.Core.Canonical;

/// <summary>
/// Reusable last-tick researcher diagnostics recorded while steering is composed. Reading
/// these views does not query current positions; use <see cref="CanonicalWorld.CaptureTelemetryInto"/>
/// to retain the whole-world telemetry values in caller-owned buffers.
/// </summary>
public sealed class RuleInstrumentation
{
    private readonly int[] _neighborCounts;
    private readonly float[] _neighborWeightSums;
    private readonly float[] _separationMagnitudes;
    private readonly float[] _alignmentMagnitudes;
    private readonly float[] _cohesionMagnitudes;
    private readonly float[] _steeringMagnitudesSquared;
    private readonly Vec2[] _steeringVectors;
    private readonly bool[] _queryTruncated;
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
        _steeringMagnitudesSquared = new float[capacity];
        _steeringVectors = new Vec2[capacity];
        _queryTruncated = new bool[capacity];
    }

    internal void Prepare(int count)
    {
        _activeCount = Math.Min(count, _neighborCounts.Length);
        Array.Clear(_neighborCounts, 0, _activeCount);
        Array.Clear(_neighborWeightSums, 0, _activeCount);
        Array.Clear(_separationMagnitudes, 0, _activeCount);
        Array.Clear(_alignmentMagnitudes, 0, _activeCount);
        Array.Clear(_cohesionMagnitudes, 0, _activeCount);
        Array.Clear(_steeringMagnitudesSquared, 0, _activeCount);
        Array.Clear(_steeringVectors, 0, _activeCount);
        Array.Clear(_queryTruncated, 0, _activeCount);
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

    internal void RecordTruncation(int index, bool isTruncated)
    {
        if (index < _activeCount)
            _queryTruncated[index] = isTruncated;
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

    /// <summary>
    /// Records the total composed steering vector a boid is about to integrate and its squared magnitude.
    /// </summary>
    /// <remarks>
    /// <see cref="CanonicalWorld.Step"/> calls this after every contribution (whisker avoidance,
    /// separation, alignment, cohesion, wander) has been composed and immediately before velocity
    /// integration. The composed steering vector is otherwise a method local that later gets
    /// reshaped by avoidance blending and renormalized to the allowed speed, so this is the only
    /// point at which the enhanced pipeline's per-tick <see cref="CanonicalWorldSettings.MaxForce"/>
    /// budget is observable from outside. Classic Reynolds instead clamps each rule separately;
    /// its recorded summed force can exceed MaxForce by design.
    /// </remarks>
    internal void RecordSteering(int index, Vec2 steering)
    {
        if (index < _activeCount)
        {
            _steeringVectors[index] = steering;
            _steeringMagnitudesSquared[index] = steering.LengthSquared;
        }
    }

    /// <summary>Last-tick resolved forces, before velocity stages; these are not velocity deltas.</summary>
    public ReadOnlySpan<Vec2> SteeringVectors => _steeringVectors.AsSpan(0, _activeCount);

    public ReadOnlySpan<int> NeighborCounts => _neighborCounts.AsSpan(0, _activeCount);

    /// <summary>
    /// Gets the squared magnitude of the total composed steering vector each active boid
    /// integrated on the most recent <see cref="CanonicalWorld.Step"/>. Compare against
    /// <see cref="CanonicalWorldSettings.MaxForce"/> squared to check the enhanced pipeline's
    /// per-tick shared budget. Classic Reynolds has an independent per-rule bound, not this total bound.
    /// </summary>
    public ReadOnlySpan<float> SteeringMagnitudesSquared => _steeringMagnitudesSquared.AsSpan(0, _activeCount);

    /// <summary>
    /// Gets whether each active boid's neighbor query was truncated on the most recent
    /// <see cref="CanonicalWorld.Step"/>, that is, whether qualifying neighbors did not fit
    /// in the simulation's per-boid candidate buffer.
    /// </summary>
    public ReadOnlySpan<bool> QueryTruncated => _queryTruncated.AsSpan(0, _activeCount);

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

    /// <summary>
    /// Gets the number of active boids whose neighbor query was truncated on the most recent
    /// <see cref="CanonicalWorld.Step"/>. Equals the number of true entries in
    /// <see cref="QueryTruncated"/>.
    /// </summary>
    public int TruncatedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _activeCount; i++)
            {
                if (_queryTruncated[i])
                    count++;
            }
            return count;
        }
    }

    public float AverageSeparationMagnitude => ComputeAverage(_separationMagnitudes);
    public float AverageAlignmentMagnitude => ComputeAverage(_alignmentMagnitudes);
    public float AverageCohesionMagnitude => ComputeAverage(_cohesionMagnitudes);

    public bool TryGetMetrics(int index, out Metrics metrics)
    {
        metrics = default;
        if (index < 0 || index >= _activeCount)
            return false;

        metrics = new Metrics(
            _neighborCounts[index],
            _neighborWeightSums[index],
            _separationMagnitudes[index],
            _alignmentMagnitudes[index],
            _cohesionMagnitudes[index]);
        return true;
    }

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

    public readonly struct Metrics
    {
        public readonly int NeighborCount;
        public readonly float NeighborWeightSum;
        public readonly float SeparationMagnitude;
        public readonly float AlignmentMagnitude;
        public readonly float CohesionMagnitude;

        public Metrics(int neighborCount, float neighborWeightSum, float separationMagnitude, float alignmentMagnitude, float cohesionMagnitude)
        {
            NeighborCount = neighborCount;
            NeighborWeightSum = neighborWeightSum;
            SeparationMagnitude = separationMagnitude;
            AlignmentMagnitude = alignmentMagnitude;
            CohesionMagnitude = cohesionMagnitude;
        }
    }
}
