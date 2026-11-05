using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalSeparationDistanceTests
{
    public static IEnumerable<object[]> DirectionCases()
    {
        foreach (int exponent in new[] { 0, 80 })
            foreach (int signX in new[] { -1, 1 })
                foreach (int signY in new[] { -1, 1 })
                    foreach (bool local in new[] { false, true })
                        yield return [exponent, signX, signY, local];
    }

    [Theory]
    [MemberData(nameof(DirectionCases))]
    public void FiniteWeightedSeparationRetainsIndependentThreeFourFiveDirection(
        int exponent, int signX, int signY, bool local)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var reverse = new Vec2(signX * 3f * scale, signY * 4f * scale);

        // Public rule callers can supply finite weights larger than one. Scaling the weight
        // keeps the inverse-distance contribution above the existing accumulator cutoff.
        Vec2 actual = Evaluate(local, 10f * scale, [reverse], [10f * scale]);

        Assert.Equal(signX * 3f, actual.X);
        Assert.Equal(signY * 4f, actual.Y);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(80, false)]
    [InlineData(80, true)]
    public void MultipleNeighborsRetainInverseDistanceAndRadiusFalloff(int exponent, bool local)
    {
        float scale = MathF.ScaleB(1f, exponent);

        Vec2 actual = Evaluate(local, 12f * scale,
            [new Vec2(3f * scale, 0f), new Vec2(0f, 4f * scale)],
            [12f * scale, 12f * scale]);

        // Independently: (1 - 3/12) / 3 * 12 = 3 and (1 - 4/12) / 4 * 12 = 2.
        // The steering direction is therefore (3, 2), rescaled to the requested speed 5.
        Assert.InRange(actual.X, (float)(15d / Math.Sqrt(13d)) - 1e-6f,
            (float)(15d / Math.Sqrt(13d)) + 1e-6f);
        Assert.InRange(actual.Y, (float)(10d / Math.Sqrt(13d)) - 1e-6f,
            (float)(10d / Math.Sqrt(13d)) + 1e-6f);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(80, false)]
    [InlineData(80, true)]
    public void NeighborsAtAndBeyondTheRadiusHaveNoInfluence(int exponent, bool local)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var reverse = new Vec2(3f * scale, 4f * scale);

        Assert.Equal(Vec2.Zero, Evaluate(local, 5f * scale, [reverse], [10f * scale]));
        Assert.Equal(Vec2.Zero, Evaluate(local, 4f * scale, [reverse], [10f * scale]));
        Assert.Equal(new Vec2(3f, 4f), Evaluate(local, 10f * scale,
            [reverse, new Vec2(-10f * scale, 0f), new Vec2(-12f * scale, 0f)],
            [10f * scale, 10f * scale, 10f * scale]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFiniteDeltaWhoseLengthExceedsFloatRangeIsOutsideEveryFiniteRadius(bool local)
    {
        Vec2 actual = Evaluate(local, float.MaxValue,
            [new Vec2(float.MaxValue, float.MaxValue)], [float.MaxValue]);

        Assert.Equal(Vec2.Zero, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryResultsKeepTheirFloatRoundingAndDesiredSpeedOrder(bool local)
    {
        Vec2 actual = Evaluate(local, 7f, [new Vec2(1f, 3f), new Vec2(-2f, 1f)], [0.7f, 0.4f],
            new Vec2(0.2f, -0.3f), targetSpeed: 3.25f, ruleWeight: 0.75f, boost: 1.125f);

        Assert.Equal(unchecked((int)0xBFA05D73), BitConverter.SingleToInt32Bits(actual.X));
        Assert.Equal(0x40353FB7, BitConverter.SingleToInt32Bits(actual.Y));
    }

    public static IEnumerable<object[]> DistanceCutoffCases()
    {
        foreach ((float distance, float expectedX) in new[]
        {
            (0f, 0f), (float.Epsilon, 0f), (MathF.BitDecrement(1e-4f), 0f),
            (1e-4f, 0f), (MathF.BitIncrement(1e-4f), 5f)
        })
            foreach (bool local in new[] { false, true })
                yield return [distance, expectedX, local];
    }

    [Theory]
    [MemberData(nameof(DistanceCutoffCases))]
    public void ExistingDistanceCutoffIsPreserved(float distance, float expectedX, bool local)
    {
        Vec2 actual = Evaluate(local, 1f, [new Vec2(distance, 0f)], [1f]);

        Assert.Equal(expectedX, actual.X);
        Assert.Equal(0f, actual.Y);
    }

    public static IEnumerable<object[]> AccumulatorCutoffCases()
    {
        foreach ((float weight, float expectedX) in new[]
        {
            (MathF.BitDecrement(2e-6f), 0f), (2e-6f, 0f), (MathF.BitIncrement(2e-6f), 5f)
        })
            foreach (bool local in new[] { false, true })
                yield return [weight, expectedX, local];
    }

    [Theory]
    [MemberData(nameof(AccumulatorCutoffCases))]
    public void ExistingAccumulatorCutoffIsPreserved(float weight, float expectedX, bool local)
    {
        Vec2 actual = Evaluate(local, 2f, [new Vec2(1f, 0f)], [weight]);

        Assert.Equal(expectedX, actual.X);
        Assert.Equal(0f, actual.Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FarNeighborsWithOrdinaryWeightsRemainBelowTheAccumulatorCutoff(bool local)
    {
        float scale = MathF.ScaleB(1f, 80);

        Vec2 actual = Evaluate(local, 10f * scale, [new Vec2(3f * scale, 4f * scale)], [1f]);

        Assert.Equal(Vec2.Zero, actual);
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(0f, true)]
    [InlineData(-1f, false)]
    [InlineData(-1f, true)]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.NegativeInfinity, true)]
    public void NonpositiveNeighborWeightsRemainIgnored(float weight, bool local)
    {
        Vec2 actual = Evaluate(local, float.MaxValue, [new Vec2(1e30f, 1e30f)], [weight]);

        Assert.Equal(Vec2.Zero, actual);
    }

    [Theory]
    [InlineData(float.NaN, 1f, 2f, 1f)]
    [InlineData(float.PositiveInfinity, 1f, float.MaxValue, 1f)]
    [InlineData(1e30f, 1e30f, float.MaxValue, float.PositiveInfinity)]
    [InlineData(1e30f, 1e30f, float.PositiveInfinity, 1f)]
    [InlineData(1e30f, 1e30f, float.NaN, 1f)]
    [InlineData(1f, 1f, 2f, float.NaN)]
    public void NonfiniteInputsRetainDirectPropagationAndIntentRejection(float x, float y, float radius, float weight)
    {
        Vec2 actual = Evaluate(false, radius, [new Vec2(x, y)], [weight]);

        Assert.True(float.IsNaN(actual.X));
        Assert.True(float.IsNaN(actual.Y));
        Assert.Throws<ArgumentException>(() => Evaluate(true, radius, [new Vec2(x, y)], [weight]));
    }

    private static Vec2 Evaluate(bool local, float radius, Vec2[] reverseOffsets, float[] weights,
        Vec2 selfVelocity = default, float targetSpeed = 5f, float ruleWeight = 1f, float boost = 1f)
    {
        var rule = new SeparationRule(ruleWeight, radius);
        var self = new Boid(Vec2.Zero, selfVelocity);
        if (local)
        {
            var neighbors = new NeighborView[reverseOffsets.Length];
            for (int i = 0; i < neighbors.Length; i++)
            {
                Vec2 reverse = reverseOffsets[i];
                float distance = (float)Math.Sqrt((double)reverse.X * reverse.X + (double)reverse.Y * reverse.Y);
                neighbors[i] = new NeighborView(new Vec2(-reverse.X, -reverse.Y), reverse,
                    new Vec2(-selfVelocity.X, -selfVelocity.Y), Vec2.Zero, 0, weights[i], distance);
            }
            var observation = new LocalObservation(self, neighbors);
            var context = new LocalPolicyContext(targetSpeed, 100f, radius, -1f, 1f, boost);
            var writer = new IntentWriter(rule.Metadata, new SteeringIntent[1]);
            rule.Emit(observation, context, ref writer);
            Assert.Equal(1, writer.Count);
            return writer.Written[0].Payload;
        }

        var boids = new Boid[reverseOffsets.Length];
        var indices = new int[reverseOffsets.Length];
        for (int i = 0; i < boids.Length; i++)
        {
            Vec2 reverse = reverseOffsets[i];
            boids[i] = new Boid(new Vec2(-reverse.X, -reverse.Y), Vec2.Zero);
            indices[i] = i;
        }
        return rule.Compute(0, self, boids, indices, weights,
            new RuleContext(targetSpeed, 100f, radius, -1f, 1f, boost));
    }
}
