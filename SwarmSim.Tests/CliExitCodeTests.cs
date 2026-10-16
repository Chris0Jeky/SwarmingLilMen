using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

/// <summary>
/// Pins the documented CLI exit codes (README "Exit codes"; issues #53 and #146):
/// 0 = success, 1 = config file failed to load, 2 = usage error
/// (unknown preset, an unrecognised option or unexpected argument, or a missing,
/// empty or malformed option value). Issues #146 and #148.
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

    // An option name after an option that needs a value is a missing value, not the value.
    [Theory]
    [InlineData("--config", "--benchmark")]
    [InlineData("-c", "-b")]
    [InlineData("--preset", "--benchmark")]
    public void OptionNameAsValue_ExitsTwo(string option, string next)
    {
        int exitCode = RenderProgram.Main(new[] { option, next });
        Assert.Equal(2, exitCode);
    }

    // The benchmark flag makes a parser that ignores the stray token return 0.
    [Theory]
    [InlineData("--agnet-count")]
    [InlineData("--bogus")]
    [InlineData("extra")]
    public void UnrecognisedOptionOrPositional_ExitsTwo(string stray)
    {
        int exitCode = RenderProgram.Main(new[] { "--benchmark", stray });
        Assert.Equal(2, exitCode);
    }

    [Theory]
    [InlineData("--preset")]
    [InlineData("--config")]
    [InlineData("-p")]
    [InlineData("-c")]
    public void EmptyOptionValue_ExitsTwo(string option)
    {
        int exitCode = RenderProgram.Main(new[] { "--benchmark", option, "" });
        Assert.Equal(2, exitCode);
    }

    // Pins the precedence decision (#148): a bad value wins over --help, --version and --list-presets.
    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    [InlineData("--list-presets")]
    public void BadValueBeatsInformationalFlags_ExitsTwo(string flag)
    {
        int exitCode = RenderProgram.Main(new[] { flag, "-n", "abc" });
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void UnknownPreset_ReportsOnStderrNotStdout()
    {
        var (exitCode, stdout, stderr) = RunCapturingConsole("--preset", "definitely-not-a-preset");

        Assert.Equal(2, exitCode);
        Assert.Contains("definitely-not-a-preset", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("definitely-not-a-preset", stdout, StringComparison.Ordinal);
    }

    // Console redirection is process-wide; this is the only test class that calls Program.Main,
    // and xUnit runs the tests inside one class serially.
    private static (int ExitCode, string Stdout, string Stderr) RunCapturingConsole(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            int exitCode = RenderProgram.Main(args);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
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
