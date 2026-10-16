using SwarmSim.Core;

namespace SwarmSim.Tests;

public class SimConfigLoadFromJsonTests
{
    [Fact]
    public void LoadFromJson_InvalidValue_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_config_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
            {
              "FixedDeltaTime": -5
            }
            """);

            Assert.Throws<ArgumentException>(() => SimConfig.LoadFromJson(path));
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
