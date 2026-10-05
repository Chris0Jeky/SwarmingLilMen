using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

/// <summary>
/// Guards CLI exit codes for early-exit branches (issue #53).
/// Unknown --preset and unloadable --config must exit non-zero;
/// --help and --list-presets stay at 0.
/// </summary>
public sealed class CliExitCodeTests
{
    [Fact]
    public void UnknownPreset_ExitsNonZero()
    {
        int exitCode = RenderProgram.Main(new[] { "--preset", "definitely-not-a-preset" });
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void MissingConfigFile_ExitsNonZero()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"slm-missing-{Guid.NewGuid():N}.json");
        int exitCode = RenderProgram.Main(new[] { "--config", missing });
        Assert.NotEqual(0, exitCode);
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
