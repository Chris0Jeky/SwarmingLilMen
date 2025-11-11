using SwarmSim.Core;

namespace SwarmSim.Tests;

public class ConfigTests
{
    [Fact]
    public void LoadFromJson_ReadsValues()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_config_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
            {
              "MaxSpeed": 12,
              "SenseRadius": 80,
              "SeparationWeight": 4.5
            }
            """);

            var config = SimConfig.LoadFromJson(path);

            Assert.Equal(12f, config.MaxSpeed);
            Assert.Equal(80f, config.SenseRadius);
            Assert.Equal(4.5f, config.SeparationWeight);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
