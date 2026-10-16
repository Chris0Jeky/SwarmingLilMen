using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

/// <summary>
/// Pins the documented CLI exit codes (README "Exit codes"; issues #53 and #146):
/// 0 = success, 1 = config file failed to load, 2 = usage error
/// (unknown preset, or a missing or malformed option value).
/// </summary>
public sealed class CliExitCodeTests
{
    [Fact]
    public void UnknownPreset_ExitsTwo()
    {
        int exitCode = RenderProgram.Main(new[] { "--preset", "definitely-not-a-preset" });
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void MissingConfigFile_ExitsOne()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"slm-missing-{Guid.NewGuid():N}.json");
        int exitCode = RenderProgram.Main(new[] { "--config", missing });
        Assert.Equal(1, exitCode);
    }

    // Every case carries --benchmark so a parser that swallows the bad value runs the
    // headless benchmark and returns 0 instead of opening a window.
    [Theory]
    [InlineData("--agent-count", "abc")]
    [InlineData("-n", "12.5")]
    [InlineData("-n", "0")]
    [InlineData("-n", "-5")]
    [InlineData("-n", "99999999999")]
    public void MalformedAgentCount_ExitsTwo(string option, string value)
    {
        int exitCode = RenderProgram.Main(new[] { "--benchmark", option, value });
        Assert.Equal(2, exitCode);
    }

    [Theory]
    [InlineData("--agent-count")]
    [InlineData("--preset")]
    [InlineData("--config")]
    public void MissingOptionValue_ExitsTwo(string option)
    {
        int exitCode = RenderProgram.Main(new[] { "--benchmark", option });
        Assert.Equal(2, exitCode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--list-presets")]
    public void HelpAndListPresets_ExitZero(string flag)
    {
        int exitCode = RenderProgram.Main(new[] { flag });
        Assert.Equal(0, exitCode);
    }
}
