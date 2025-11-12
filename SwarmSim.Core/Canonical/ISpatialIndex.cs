namespace SwarmSim.Core.Canonical;

public interface ISpatialIndex
{
    void Initialize(int capacity);
    void Rebuild(ReadOnlySpan<Boid> boids);
    int QueryNeighbors(ReadOnlySpan<Boid> boids, int selfIndex, float radius, Span<int> results);
}
