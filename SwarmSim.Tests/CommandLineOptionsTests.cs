using System.Text.RegularExpressions;
using SwarmSim.Render;
using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

public class CommandLineOptionsTests
{
    // Captures the token following each "--preset" occurrence in the help text.
    private static readonly Regex AdvertisedPresetPattern = new(@"--preset\s+(\S+)", RegexOptions.Compiled);

    // The literal placeholder on the option line; not a preset ID.
    private const string PresetPlaceholder = "NAME";

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

    [Fact]
    public void HelpText_EveryAdvertisedPresetResolvesThroughRegistry()
    {
        string help = CommandLineOptions.GetHelpText();

        var advertised = AdvertisedPresetPattern.Matches(help)
            .Select(match => match.Groups[1].Value)
            .Where(token => !string.Equals(token, PresetPlaceholder, StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(advertised);
        foreach (string token in advertised)
        {
            Assert.True(
                RenderProgram.IsRegisteredPreset(token),
                $"--help advertises '--preset {token}', but the renderer's preset lookup rejects it.");
        }
    }

    [Fact]
    public void HelpText_PresetOptionParenthetical_ResolvesThroughRegistry()
    {
        string help = CommandLineOptions.GetHelpText();

        string? optionLine = help
            .Split('\n')
            .Select(text => text.TrimEnd('\r'))
            .FirstOrDefault(text => text.Contains("--preset " + PresetPlaceholder, StringComparison.Ordinal));

        Assert.NotNull(optionLine);
        string presetLine = optionLine!;

        const string marker = "(e.g., ";
        int start = presetLine.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"No '{marker}' example list on the --preset line: {presetLine}");

        int contentStart = start + marker.Length;
        int end = presetLine.IndexOf(')', contentStart);
        Assert.True(end > contentStart, $"Unterminated example list on the --preset line: {presetLine}");

        var examples = presetLine[contentStart..end]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.NotEmpty(examples);
        foreach (string example in examples)
        {
            Assert.True(
                RenderProgram.IsRegisteredPreset(example),
                $"--help lists '{example}' as a --preset example, but the preset lookup rejects it.");
        }
    }

    [Fact]
    public void HelpText_JsonConfigNamesAreNotAdvertisedAsPresets()
    {
        string help = CommandLineOptions.GetHelpText();

        Assert.DoesNotContain("--preset peaceful", help, StringComparison.Ordinal);
        Assert.DoesNotContain("--preset warbands", help, StringComparison.Ordinal);
        Assert.Contains("configs/peaceful.json", help, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpText_MatchesReadmeTranscript()
    {
        string readmePath = Path.Combine(AppContext.BaseDirectory, "README.md");
        Assert.True(
            File.Exists(readmePath),
            $"README.md was not copied to the test output directory ({readmePath}); check SwarmSim.Tests.csproj.");

        string transcript = ExtractUsageTranscript(File.ReadAllText(readmePath));
        Assert.False(
            string.IsNullOrWhiteSpace(transcript),
            "README.md has no fenced block starting with 'Usage: SwarmSim.Render'.");

        // Program.Main prints `Console.WriteLine(GetHelpText())` (help text plus a blank separator
        // line) followed by the preset listing, so the README block must equal exactly that.
        string expected = CommandLineOptions.GetHelpText() + Environment.NewLine + RenderProgram.FormatPresetList();

        Assert.Equal(Normalize(expected), Normalize(transcript));
    }

    [Fact]
    public void PresetIds_AreUniqueAndResolveThroughLookup()
    {
        var ids = RenderProgram.PresetIds;

        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id), "A registered preset has a blank ID."));
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(ids, id => Assert.True(RenderProgram.IsRegisteredPreset(id), $"Registered preset '{id}' does not resolve."));
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>
    /// Returns the body of the first fenced code block whose first line starts the CLI usage
    /// transcript, or an empty string when the README contains no such block.
    /// </summary>
    private static string ExtractUsageTranscript(string readme)
    {
        const string fence = "```";
        string normalized = readme.Replace("\r\n", "\n", StringComparison.Ordinal);

        int cursor = 0;
        while (cursor < normalized.Length)
        {
            int fenceStart = normalized.IndexOf(fence, cursor, StringComparison.Ordinal);
            if (fenceStart < 0)
            {
                break;
            }

            int bodyStart = normalized.IndexOf('\n', fenceStart);
            if (bodyStart < 0)
            {
                break;
            }

            bodyStart++;
            int fenceEnd = normalized.IndexOf("\n" + fence, bodyStart - 1, StringComparison.Ordinal);
            if (fenceEnd < 0)
            {
                break;
            }

            string body = normalized[bodyStart..(fenceEnd + 1)];
            if (body.StartsWith("Usage: SwarmSim.Render", StringComparison.Ordinal))
            {
                return body;
            }

            cursor = fenceEnd + 1 + fence.Length;
        }

        return string.Empty;
    }
}
