using SwarmSim.Render;

namespace SwarmSim.Tests;

public class CommandLineOptionsTests
{
    [Fact]
    public void Parse_AssignsPresetAgentCountAndBenchmark()
    {
        var options = CommandLineOptions.Parse(new[]
        {
            "--preset", "fast-loose",
            "--agent-count", "5000",
            "--benchmark"
        });

        Assert.Equal("fast-loose", options.PresetName);
        Assert.Equal(5000, options.AgentCount);
        Assert.True(options.BenchmarkMode);
    }

    [Fact]
    public void Parse_RecognizesHelpAndListFlags()
    {
        var options = CommandLineOptions.Parse(new[] { "--help", "--list-presets" });
        Assert.True(options.ShowHelp);
        Assert.True(options.ListPresets);
    }
}
