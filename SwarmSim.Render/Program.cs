    using SwarmSim.Core.Canonical;

    private static Vec2 ComputeMeanHeading(CanonicalWorld world)
    {
        Vec2 sum = Vec2.Zero;
        foreach (var boid in world.Boids)
        {
            if (boid.Velocity.IsNearlyZero())
                continue;

            sum += boid.Velocity.Normalized;
        }

        if (sum.IsNearlyZero())
            return new Vec2(1f, 0f);

        return sum.Normalized;
    }
