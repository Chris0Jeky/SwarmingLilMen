namespace SwarmSim.Core.Canonical;

public sealed class NaiveSpatialIndex : ISpatialIndex
{
    private int _capacity;

    public void Initialize(int capacity)
    {
        _capacity = Math.Max(capacity, 1);
    }

    public void Rebuild(ReadOnlySpan<Boid> boids)
    {
        // Naive index does not require preprocessing.
    }

    public int QueryNeighbors(ReadOnlySpan<Boid> boids, int selfIndex, float radius, Span<int> results)
    {
        if (results.IsEmpty || selfIndex < 0 || selfIndex >= boids.Length)
            return 0;

        float radiusSq = radius * radius;
        int count = 0;

        Vec2 position = boids[selfIndex].Position;

        for (int i = 0; i < boids.Length && count < results.Length; i++)
        {
            if (i == selfIndex)
                continue;

            Vec2 delta = boids[i].Position - position;
            if (delta.LengthSquared <= radiusSq)
            {
                results[count++] = i;
            }
        }

        return count;
    }
}
