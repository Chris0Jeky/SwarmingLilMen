using System.Reflection;
using System.Text.Json;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class ConfigurationJsonTests
{
    [Fact]
    public void SimConfig_FileRoundTrip_PreservesEveryNonDefaultProperty()
    {
        SimConfig original = CreateNonDefaultConfig<SimConfig>();
        string path = Path.Combine(Path.GetTempPath(), $"swarm_config_{Guid.NewGuid():N}.json");
        try
        {
            original.SaveToJson(path);
            SimConfig restored = SimConfig.LoadFromJson(path);

            AssertEveryPropertyEqual(original, restored);
            AssertEveryPropertyEqual(original, SimConfig.FromJson(original.ToJson()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CanonicalSettings_FileRoundTrip_PreservesEveryNonDefaultProperty()
    {
        CanonicalWorldSettings original = CreateNonDefaultConfig<CanonicalWorldSettings>();
        string path = Path.Combine(Path.GetTempPath(), $"swarm_canonical_{Guid.NewGuid():N}.json");
        try
        {
            original.SaveToJson(path);
            CanonicalWorldSettings restored = CanonicalWorldSettings.LoadFromJson(path);

            AssertEveryPropertyEqual(original, restored);
            AssertEveryPropertyEqual(original, CanonicalWorldSettings.FromJson(original.ToJson()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void JsonReaders_PreserveDefaultsAndExistingPermissiveSyntax()
    {
        const string json = """
            {
              // Authored files may use comments, mixed casing and trailing commas.
              "worldwidth": 800,
              "UnknownFutureField": true,
            }
            """;

        SimConfig config = SimConfig.FromJson(json);
        CanonicalWorldSettings settings = CanonicalWorldSettings.FromJson(json);

        Assert.Equal(800f, config.WorldWidth);
        Assert.Equal(new SimConfig().MaxSpeed, config.MaxSpeed);
        Assert.Equal(800f, settings.WorldWidth);
        Assert.Equal(new CanonicalWorldSettings().TargetSpeed, settings.TargetSpeed);
    }

    [Theory]
    [InlineData("{\"TargetSpeed\":0}")]
    [InlineData("{\"FixedDeltaTime\":-1}")]
    [InlineData("{\"SeparationWeight\":1e100}")]
    [InlineData("{\"SeparationRadius\":1e100}")]
    [InlineData("{\"WanderRate\":-1}")]
    [InlineData("{\"SeparationSpeedDroop\":1.5}")]
    public void CanonicalSettings_JsonReaderRejectsInvalidActiveValues(string json)
    {
        Assert.Throws<ArgumentException>(() => CanonicalWorldSettings.FromJson(json));
    }

    [Fact]
    public void CanonicalSettings_JsonReaderPreservesExistingClampedModes()
    {
        CanonicalWorldSettings settings = CanonicalWorldSettings.FromJson("""
            {"InitialCapacity":0,"MaxNeighbors":-1,"SeparationRadius":-1,
             "SeparationWeight":-1,"AlignmentWeight":-1,"CohesionWeight":-1,
             "WhiskerTimeHorizon":-1,"SeparationPriorityRadiusFactor":-1,
             "SeparationPriorityExitFactor":-1,"SeparationPriorityHoldTime":-1,
             "SeparationPriorityRampInTime":-1,"SeparationPriorityRampOutTime":-1}
            """);

        Assert.Empty(settings.Validate());
        Assert.Equal(-1, settings.MaxNeighbors);
        Assert.Equal(-1f, settings.SeparationRadius);
        Assert.NotNull(new CanonicalWorld(settings, new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight)));
    }

    [Fact]
    public void SavingInvalidConfig_LeavesExistingFileIntact()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_config_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "existing content");

            Assert.Throws<ArgumentException>(() => new SimConfig { MaxSpeed = -1f }.SaveToJson(path));
            Assert.Equal("existing content", File.ReadAllText(path));
            Assert.Throws<ArgumentException>(() => new CanonicalWorldSettings { TargetSpeed = -1f }.SaveToJson(path));
            Assert.Equal("existing content", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void JsonReaders_RejectNullAndInvalidMatrixShapes()
    {
        Assert.Throws<InvalidOperationException>(() => SimConfig.FromJson("null"));
        Assert.Throws<InvalidOperationException>(() => CanonicalWorldSettings.FromJson("null"));
        Assert.Throws<ArgumentException>(() => SimConfig.FromJson("""{"AggressionMatrix":null}"""));
        Assert.Throws<ArgumentException>(() => SimConfig.FromJson("""{"AggressionMatrix":[[0,1,2],[1,0,2]]}"""));
    }

    [Fact]
    public void SimConfig_DefaultSerializer_RoundTripsAggressionRows()
    {
        var original = new SimConfig { AggressionMatrix = new float[,] { { 0f, -0.75f }, { 0.5f, 1f } } };

        string json = JsonSerializer.Serialize(original);
        SimConfig restored = JsonSerializer.Deserialize<SimConfig>(json)!;

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(-0.75f, document.RootElement.GetProperty("AggressionMatrix")[0][1].GetSingle());
        Assert.Equal(0.5f, restored.AggressionMatrix[1, 0]);
        AssertEveryPropertyEqual(original, restored);
        Assert.NotSame(original.AggressionMatrix, restored.AggressionMatrix);
    }

    [Fact]
    public void SimConfig_LoadFromJson_AcceptsNestedAggressionRows()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_config_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"AggressionMatrix":[[0,-1],[0.5,0]]}""");

            SimConfig config = SimConfig.LoadFromJson(path);

            Assert.Equal(2, config.AggressionMatrix.GetLength(0));
            Assert.Equal(2, config.AggressionMatrix.GetLength(1));
            Assert.Equal(-1f, config.AggressionMatrix[0, 1]);
            Assert.Equal(0.5f, config.AggressionMatrix[1, 0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("[[0,1],[0]]")]
    [InlineData("[[0,1],null]")]
    [InlineData("[0,1]")]
    [InlineData("[[0,\"hostile\"],[1,0]]")]
    public void SimConfig_RejectsMalformedAggressionRows(string matrix)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SimConfig>($"{{\"AggressionMatrix\":{matrix}}}"));
    }

    private static void AssertEveryPropertyEqual<T>(T expected, T actual) where T : class
    {
        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetValue(expected) is float[,] expectedMatrix)
            {
                var actualMatrix = Assert.IsType<float[,]>(property.GetValue(actual));
                Assert.Equal(expectedMatrix.GetLength(0), actualMatrix.GetLength(0));
                Assert.Equal(expectedMatrix.GetLength(1), actualMatrix.GetLength(1));
                for (int row = 0; row < expectedMatrix.GetLength(0); row++)
                    for (int column = 0; column < expectedMatrix.GetLength(1); column++)
                        Assert.Equal(expectedMatrix[row, column], actualMatrix[row, column]);
            }
            else
            {
                Assert.Equal(property.GetValue(expected), property.GetValue(actual));
            }
        }
    }

    private static T CreateNonDefaultConfig<T>() where T : class, new()
    {
        var config = new T();
        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            object value = property.PropertyType == typeof(float) ?
                (typeof(T) == typeof(CanonicalWorldSettings) && property.Name == nameof(CanonicalWorldSettings.FieldOfView) ? 360f : 0.375f)
                : property.PropertyType == typeof(uint) ? 37u
                : property.PropertyType == typeof(int) ? (property.Name == nameof(SimConfig.MaxCapacity) ? 17 : 7)
                : property.PropertyType == typeof(BoundaryMode) ? BoundaryMode.Reflect
                : property.PropertyType == typeof(SpeedModel) ? SpeedModel.Damped
                : property.PropertyType == typeof(CanonicalMode) ? CanonicalMode.ClassicReynolds
                : property.PropertyType == typeof(float[,]) ? new float[,] { { 0.1f, -0.8f }, { 0.9f, 0.2f } }
                : throw new InvalidOperationException($"Add a non-default fixture value for {property.Name}.");
            Assert.NotEqual(property.GetValue(config), value);
            property.SetValue(config, value);
        }
        return config;
    }
}
