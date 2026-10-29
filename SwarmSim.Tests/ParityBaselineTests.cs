using System.Text.Json;
using System.Text.Json.Nodes;
using SwarmSim.Parity;

namespace SwarmSim.Tests;

public sealed class ParityBaselineTests
{
    [Fact]
    public void CheckedInCalibrationMatchesItsActualRecipesAndCompleteContract()
    {
        ScenarioRecipe[] recipes = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenarios"), "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => ScenarioRecipe.FromJson(File.ReadAllText(path))).ToArray();
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "baselines", "parity-v1.json"));
        ParityBaseline baseline = ParityBaseline.FromJson(json, recipes);

        Assert.Equal(100, baseline.Calibration.Length);
        Assert.Equal(9, baseline.Envelopes.Length);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Version\":1,\"Version\":1}")]
    [InlineData("{\"Version\":999}")]
    public void MissingDuplicateOrUnknownBaselineCannotPass(string json) =>
        Assert.Throws<JsonException>(() => ParityBaseline.FromJson(json, Array.Empty<ScenarioRecipe>()));

    [Fact]
    public void RecordingAnIncompleteMatrixIsAnError() => Assert.Throws<ArgumentException>(() =>
        ParityBaseline.Create(new[] { new ScenarioRecipe() }, Array.Empty<ParityRunResult>()));

    [Fact]
    public void PolarizationDifferenceOutsideLockedBandFailsTheActualComparator()
    {
        ScenarioRecipe[] recipes = Recipes();
        ParityRunResult[] calibration = Results(recipes);
        ParityBaseline baseline = ParityBaseline.Create(recipes, calibration);
        Assert.Empty(baseline.Compare(recipes, calibration));
        var changed = calibration.Select(x =>
        {
            if (x.Scenario != "S4_UNIFORM_RANDOM_512" || x.Model != "classic") return x;
            double?[] means = (double?[])x.WindowMeans.Clone();
            means[2] = 0.3;
            return x with { WindowMeans = means };
        }).ToArray();
        Assert.Contains(baseline.Compare(recipes, changed), x => x.Contains("polarization", StringComparison.Ordinal));
    }

    [Fact]
    public void ChangedRecipeOrOmittedRunCannotSilentlyReuseABaseline()
    {
        ScenarioRecipe[] recipes = Recipes();
        ParityRunResult[] calibration = Results(recipes);
        ParityBaseline baseline = ParityBaseline.Create(recipes, calibration);
        Assert.Throws<ArgumentException>(() => baseline.Compare(recipes, calibration[..^1]));
        recipes[0] = recipes[0] with { SenseRadius = 31 };
        Assert.Throws<JsonException>(() => ParityBaseline.FromJson(JsonSerializer.Serialize(baseline), recipes));
    }

    [Fact]
    public void UnjustifiedBandWideningOrMissingMetricIsRejected()
    {
        ScenarioRecipe[] recipes = Recipes();
        ParityRunResult[] calibration = Results(recipes);
        ParityBaseline baseline = ParityBaseline.Create(recipes, calibration);
        baseline.Envelopes[0] = baseline.Envelopes[0] with { AbsoluteTolerance = 99 };
        Assert.Throws<JsonException>(() => ParityBaseline.FromJson(JsonSerializer.Serialize(baseline), recipes));
        calibration[0] = calibration[0] with { WindowMeans = calibration[0].WindowMeans[..^1] };
        Assert.Throws<ArgumentException>(() => ParityBaseline.Create(recipes, calibration));
    }

    [Fact]
    public void MissingZeroValuedNestedMemberDoesNotDeserializeAsEvidence()
    {
        ScenarioRecipe[] recipes = Recipes();
        ParityBaseline baseline = ParityBaseline.Create(recipes, Results(recipes));
        JsonNode json = JsonNode.Parse(JsonSerializer.Serialize(baseline))!;
        json["Envelopes"]![0]!.AsObject().Remove("MeanObservedDifference");
        Assert.Throws<JsonException>(() => ParityBaseline.FromJson(json.ToJsonString(), recipes));
    }

    [Fact]
    public void HugeFiniteMetricsCannotTurnAnOverflowedDifferenceIntoAPass()
    {
        ScenarioRecipe[] recipes = Recipes();
        ParityRunResult[] rows = Results(recipes);
        foreach (ParityRunResult row in rows) row.WindowMeans[2] = double.MaxValue;
        Assert.Throws<ArgumentException>(() => ParityBaseline.Create(recipes, rows));
    }

    [Fact]
    public void MissingDefinedMetricCannotHideBehindNullableDiagnostics()
    {
        ScenarioRecipe[] recipes = Recipes();
        ParityRunResult[] rows = Results(recipes);
        rows[0].WindowMeans[0] = null;
        rows[0].WindowFiniteCounts[0] = 0;
        Assert.Throws<ArgumentException>(() => ParityBaseline.Create(recipes, rows));
    }

    [Fact]
    public void SharedForgedInitialIdentityCannotCompareDifferentInputs()
    {
        ScenarioRecipe[] recipes = Recipes();
        ParityRunResult[] rows = Results(recipes);
        ParityBaseline baseline = ParityBaseline.Create(recipes, rows);
        ParityRunResult[] forged = rows.Select(x => x with { RequestedInitialHash = new string('F', 64) }).ToArray();
        Assert.Throws<ArgumentException>(() => baseline.Compare(recipes, forged));
    }

    private static ScenarioRecipe[] Recipes() => new[]
    {
        new ScenarioRecipe(),
        new ScenarioRecipe { Id = "S2_RING_16", Layout = "ring", Count = 16, Ticks = 600, LayoutRadius = 24 },
        new ScenarioRecipe { Id = "S3_TWO_LINE_CROSSING", Layout = "crossing", Count = 32, Ticks = 400 },
        new ScenarioRecipe { Id = "S4_UNIFORM_RANDOM_512", Layout = "uniform", Count = 512, Ticks = 1800 },
        new ScenarioRecipe { Id = "S5_DENSE_CLUMP_256", Layout = "clump", Count = 256, Ticks = 900 }
    };

    private static ParityRunResult[] Results(ScenarioRecipe[] recipes) =>
        (from recipe in recipes from seed in ParityRunner.Seeds from model in ParityRunner.Models
         select Row(recipe, seed, model)).ToArray();

    private static ParityRunResult Row(ScenarioRecipe recipe, uint seed, string model)
    {
        (string requested, string actual) = ParityRunner.InitialIdentities(recipe, seed, model);
        return new(recipe.Id, seed, model, requested, actual, new string('0',64), new string('0',64), new string('0',64),
            Means(recipe.Count), Enumerable.Repeat(recipe.Ticks - recipe.Ticks * 4 / 5, 16).ToArray());
    }

    private static double?[] Means(int count)
    {
        var values = Enumerable.Repeat<double?>(0, 16).ToArray();
        values[0] = count;
        return values;
    }
}
