namespace SwarmSim.Core.Canonical;

public interface IRule
{
    Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids, ReadOnlySpan<int> neighborIndices, RuleContext context);
}
