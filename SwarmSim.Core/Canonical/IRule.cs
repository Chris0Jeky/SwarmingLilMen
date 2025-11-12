namespace SwarmSim.Core.Canonical;

public interface IRule
{
    Vec2 Compute(Boid self, ReadOnlySpan<Boid> neighbors, RuleContext context);
}
