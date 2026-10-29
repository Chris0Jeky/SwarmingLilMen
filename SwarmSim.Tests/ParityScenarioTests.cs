using System.Text.Json;
using SwarmSim.Core.Metrics;
using SwarmSim.Parity;

namespace SwarmSim.Tests;

public sealed class ParityScenarioTests
{
    [Fact]
    public void HeadOnRecipeGeneratesOpposingMotionAtExplicitDistance()
    {
        KinematicSample[] samples = new ScenarioRecipe().Generate(7);
        Assert.Equal(new KinematicSample(118, 128, 30, 0), samples[0]);
        Assert.Equal(new KinematicSample(138, 128, -30, 0), samples[1]);
    }

    [Fact]
    public void RandomRecipeRepeatsAndChangesWithItsSeed()
    {
        var recipe = new ScenarioRecipe { Id = "S4_UNIFORM_RANDOM_512", Layout = "uniform", Count = 512, Ticks = 1800 };
        KinematicSample[] first = recipe.Generate(7);
        Assert.Equal(first, recipe.Generate(7));
        Assert.NotEqual(first[0], recipe.Generate(19)[0]);
        Assert.All(first, x =>
        {
            Assert.InRange(x.X, 0f, MathF.BitDecrement(256));
            Assert.InRange(x.Y, 0f, MathF.BitDecrement(256));
            Assert.InRange(MathF.Sqrt(x.Vx * x.Vx + x.Vy * x.Vy), 29.99999f, 30.00001f);
        });
    }

    [Theory]
    [InlineData("{\"Version\":1}")]
    [InlineData("{\"Version\":1,\"Version\":1}")]
    [InlineData("{\"Typo\":0}")]
    public void IncompleteDuplicateOrUnknownRecipeCannotBecomeAnExperiment(string json) =>
        Assert.Throws<JsonException>(() => ScenarioRecipe.FromJson(json));

    [Fact]
    public void IndependentReferencePreservesStraightMotionAndWrapsOvershoot()
    {
        var recipe = new ScenarioRecipe { Count = 1, Layout = "uniform", Width = 10, Height = 10, TargetSpeed = 2, DeltaTime = 1 };
        var reference = new ReynoldsReference(recipe, new[] { new KinematicSample(9, 5, 2, 0) });
        reference.Step();
        Assert.Equal(new KinematicSample(1, 5, 2, 0), reference.Agents[0]);
    }

    [Fact]
    public void IndependentReferenceAlignsAcrossPeriodicSeam()
    {
        var recipe = new ScenarioRecipe { Width = 100, Height = 100, TargetSpeed = 1, DeltaTime = 0.5f,
            MaxForce = 1, SenseRadius = 5, SeparationRadius = 0, SeparationWeight = 0, CohesionWeight = 0 };
        var reference = new ReynoldsReference(recipe, new[] {
            new KinematicSample(1, 50, 1, 0), new KinematicSample(99, 50, 0, 1) });
        reference.Step();
        // The other boid is two units away on the torus and must influence heading.
        Assert.True(reference.Agents[0].Vy > 0.1f);
        Assert.True(reference.Agents[1].Vx > 0.1f);
        Assert.InRange(MathF.Sqrt(reference.Agents[0].Vx * reference.Agents[0].Vx +
            reference.Agents[0].Vy * reference.Agents[0].Vy), 0.999999f, 1.000001f);
    }

    [Fact]
    public void ReferenceForceReadsOldStateForEveryAgent()
    {
        var recipe = new ScenarioRecipe { Count = 2, Width = 100, Height = 100, TargetSpeed = 1,
            DeltaTime = 1, MaxForce = 0.1f, SenseRadius = 10, SeparationRadius = 0 };
        KinematicSample[] initial = { new(40, 50, 0, 1), new(44, 50, 0, -1) };
        var forward = new ReynoldsReference(recipe, initial);
        var reversed = new ReynoldsReference(recipe, new[] { initial[1], initial[0] });
        forward.Step(); reversed.Step();
        Assert.Equal(forward.Agents[0], reversed.Agents[1]);
        Assert.Equal(forward.Agents[1], reversed.Agents[0]);
    }

    [Fact]
    public void ExactSeparationRadiusDoesNotInventZeroInfluenceBraking()
    {
        var recipe = new ScenarioRecipe { Count = 2, Width = 100, Height = 100, TargetSpeed = 1,
            DeltaTime = 0.5f, MaxForce = 0.5f, SenseRadius = 32, SeparationRadius = 8 };
        var reference = new ReynoldsReference(recipe, new[] { new KinematicSample(40,50,0,1), new(48,50,0,1) });
        reference.Step();
        // Only cohesion contributes: normalized (sqrt(0.5)/4, 1-sqrt(0.5)/4).
        Assert.InRange(reference.Agents[0].Vx, 0.2099f, 0.2100f);
        Assert.InRange(reference.Agents[0].Vy, 0.9777f, 0.9778f);
    }
}
