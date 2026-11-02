using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalShapedAvoidanceDistanceTests
{
    public static IEnumerable<object[]> StageCases()
    {
        foreach (int exponent in new[] { 0, 80 })
            foreach (int direction in new[] { 0, -1, 1 })
                yield return [exponent, direction];
    }

    [Theory]
    [MemberData(nameof(StageCases))]
    public void StageUsesTheRepresentableDistanceAndAwayDirection(int exponent, int direction)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var settings = Settings(scale);
        var stage = new ShapedAvoidanceStage(settings);
        var self = new Boid(Vec2.Zero, new Vec2(1f, 0f));
        var context = new VelocityStageContext(self, NeighborDelta(scale, direction), 0f, 1f, 1f);

        Vec2 actual = stage.Apply(context, self.Velocity);

        AssertExpectedVelocity(actual, direction);
    }

    public static IEnumerable<object[]> WorldCases()
    {
        foreach (object[] stageCase in StageCases())
            foreach (bool grid in new[] { false, true })
                foreach (bool seam in new[] { false, true })
                    yield return [stageCase[0], stageCase[1], grid, seam];
    }

    [Theory]
    [MemberData(nameof(WorldCases))]
    public void WorldSuppliesTheRecoveredNearestDeltaToShapedAvoidance(
        int exponent, int direction, bool grid, bool seam)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var settings = Settings(scale);
        ISpatialIndex index = grid
            ? new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight)
            : new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight);
        var world = new CanonicalWorld(settings, index,
            new CanonicalPipeline([], new PriorityForceBudgetResolver(), [new ShapedAvoidanceStage(settings)]));
        float coordinate = seam ? direction < 0 ? 2f : 62f : 16f;
        var position = new Vec2(coordinate * scale, coordinate * scale);
        Assert.True(world.TryAddBoid(position, new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(position + NeighborDelta(scale, direction), new Vec2(1f, 0f)));

        world.Step(settings.FixedDeltaTime);

        AssertExpectedVelocity(world.Boids[0].Velocity, direction);
        Assert.Equal(2, world.GetStageExecutionCount(CanonicalStageIds.ShapedAvoidance));
        Assert.Equal(0f, world.Instrumentation.SteeringMagnitudesSquared[0]);
        var telemetry = new CanonicalTelemetry(2);
        world.CaptureTelemetryInto(telemetry);
        Assert.Equal((1, 1, 1f), telemetry.NeighborCountStats);
        Assert.Equal(5f * scale, telemetry.NearestDistances[0]);
        Assert.Equal(0, telemetry.TruncatedCount);
    }

    [Theory]
    [InlineData(0, 20f)]
    [InlineData(0, 21f)]
    [InlineData(80, 20f)]
    [InlineData(80, 21f)]
    public void GradualRadiusRemainsAnExclusiveBoundary(int exponent, float distance)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var stage = new ShapedAvoidanceStage(Settings(scale));
        var self = new Boid(Vec2.Zero, new Vec2(1f, 0f));
        var context = new VelocityStageContext(self, new Vec2(distance * scale, 0f), 1f, 1f, 1f);

        Vec2 actual = stage.Apply(context, self.Velocity);

        Assert.Equal(1f, actual.X);
        Assert.Equal(0f, actual.Y);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1e-7f)]
    [InlineData(float.Epsilon)]
    public void NearlyZeroCutoffStillSuppressesAvoidance(float distance)
    {
        var stage = new ShapedAvoidanceStage(Settings(1f));
        var self = new Boid(Vec2.Zero, new Vec2(1f, 0f));
        var context = new VelocityStageContext(self, new Vec2(distance, 0f), 1f, 1f, 1f);

        Vec2 actual = stage.Apply(context, self.Velocity);

        Assert.Equal(1f, actual.X);
        Assert.Equal(0f, actual.Y);
    }

    private static CanonicalWorldSettings Settings(float scale) => new()
    {
        InitialCapacity = 2,
        WorldWidth = 64f * scale,
        WorldHeight = 64f * scale,
        SenseRadius = 20f * scale,
        SeparationRadius = 10f * scale,
        SeparationPriorityRadiusFactor = 0.25f,
        FieldOfView = 360f,
        TargetSpeed = 1f,
        WanderStrength = 0f
    };

    private static Vec2 NeighborDelta(float scale, int direction) => direction == 0
        ? new Vec2(5f * scale, 0f) : new Vec2(0f, direction * 5f * scale);

    private static void AssertExpectedVelocity(Vec2 actual, int direction)
    {
        // Distance equals the hard radius, so SmoothStep is zero and the distance ratio
        // is one. With no priority, influence is 0.5 and interpolation weight is 0.35.
        // The unit away vector is (-1,0) for the front neighbor and (0,-direction) laterally.
        Assert.Equal(direction == 0 ? 0.3f : 0.65f, actual.X);
        Assert.Equal(direction == 0 ? 0f : -direction * 0.35f, actual.Y);
    }
}
