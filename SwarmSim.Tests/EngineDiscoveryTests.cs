using System.Text.Json;
using SwarmSim.Cli;

namespace SwarmSim.Tests;

public sealed class EngineDiscoveryTests
{
    [Fact]
    public void EngineListIsGeneratedFromTheRegistry()
    {
        var result = SweepCliTests.Run("--list-engines");
        Assert.True(result.Code == 0, result.Error);
        foreach (EngineDefinition definition in EngineRegistry.All)
            Assert.Contains(definition.Name + " — ", result.Output, StringComparison.Ordinal);
        Assert.Contains("--describe-engine", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonListHasAVersionAndExactlyTheRegisteredEngines()
    {
        var result = SweepCliTests.Run("--list-engines", "--json");
        Assert.True(result.Code == 0, result.Error);
        using JsonDocument json = JsonDocument.Parse(result.Output);
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(EngineRegistry.All.Select(x => x.Name), json.RootElement.GetProperty("engines")
            .EnumerateArray().Select(x => x.GetProperty("name").GetString()));
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("canonical")]
    [InlineData("vicsek")]
    [InlineData("aco")]
    public void DescriptionExposesNativeDefaultsMetricsAndStateWithoutStartingARun(string engine)
    {
        var result = SweepCliTests.Run("--engine", engine, "--describe-engine", "--json");
        Assert.True(result.Code == 0, result.Error);
        using JsonDocument json = JsonDocument.Parse(result.Output);
        JsonElement description = json.RootElement.GetProperty("engine");
        Assert.Equal(engine, description.GetProperty("name").GetString());
        Assert.NotEmpty(description.GetProperty("state").EnumerateArray());
        Assert.Equal(EngineRegistry.Get(engine).AcceptsScenario, description.GetProperty("acceptsScenario").GetBoolean());
        Assert.Equal(EngineRegistry.Get(engine).SpawnModel, description.GetProperty("spawnModel").GetString());
        using JsonDocument defaults = JsonDocument.Parse(EngineRegistry.Get(engine).DefaultSettingsJson());
        Assert.Equal(JsonSerializer.Serialize(defaults.RootElement), JsonSerializer.Serialize(description.GetProperty("defaultConfiguration")));
        string[] names = description.GetProperty("metrics").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()!).ToArray();
        Assert.Contains("population", names);
        Assert.True(MetricCatalog.ValidateSelection(engine, names.Where(x => !x.StartsWith("runtime.", StringComparison.Ordinal)).ToArray()));
        Assert.False(description.GetProperty("checkpointRestore").GetBoolean());
    }

    [Fact]
    public void TextDescriptionExplainsHowToRunAndInspectTheSelectedModel()
    {
        var result = SweepCliTests.Run("--engine", "aco", "--describe-engine");
        Assert.True(result.Code == 0, result.Error);
        Assert.Contains("pheromone", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aco.foodReturned", result.Output, StringComparison.Ordinal);
        Assert.Contains("--engine aco", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--json")]
    [InlineData("--list-engines", "--engine", "aco")]
    [InlineData("--list-engines", "--describe-engine")]
    [InlineData("--list-engines", "--out", "should-not-be-created")]
    [InlineData("--describe-engine", "--agents", "2147483647")]
    [InlineData("--describe-engine", "--visual")]
    [InlineData("--describe-engine", "--list-metrics")]
    public void DiscoveryRefusesRunOptionsAndAmbiguousModes(params string[] args)
    {
        var result = SweepCliTests.Run(args);
        Assert.Equal(2, result.Code);
        Assert.NotEmpty(result.Error);
    }
}
