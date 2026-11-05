using SwarmSim.Core.Configuration;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Firefly;

/// <summary>
/// Global Kuramoto oscillators with fixed display positions and synchronous forward Euler steps.
/// Frequencies are uniform on [MeanFrequency - FrequencySpread, MeanFrequency + FrequencySpread).
/// Frequency and coupling units are radians per simulation time unit; phase has no spatial meaning.
/// </summary>
public sealed class FireflySettings
{
    public float Coupling { get; init; } = 1.55f;
    public float MeanFrequency { get; init; } = 1.8f;
    /// <summary>Half-width of the uniform natural-frequency distribution, not its standard deviation.</summary>
    public float FrequencySpread { get; init; } = 0.35f;
    public float WorldWidth { get; init; } = 100f;
    public float WorldHeight { get; init; } = 60f;
    public float FixedDeltaTime { get; init; } = 0.02f;
    public uint Seed { get; init; } = 123456u;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Seed > Rng.MaxSupportedSeed) errors.Add($"Seed must be between 0 and {Rng.MaxSupportedSeed}, inclusive");
        if (!float.IsFinite(Coupling) || Coupling < 0f) errors.Add("Coupling must be finite and nonnegative");
        if (!float.IsFinite(MeanFrequency)) errors.Add("MeanFrequency must be finite");
        if (!float.IsFinite(FrequencySpread) || FrequencySpread < 0f) errors.Add("FrequencySpread must be finite and nonnegative");
        if (float.IsFinite(MeanFrequency) && float.IsFinite(FrequencySpread)
            && Math.Abs((double)MeanFrequency) + FrequencySpread > float.MaxValue)
            errors.Add("The natural-frequency distribution must fit in finite single-precision state");
        if (!float.IsFinite(WorldWidth) || WorldWidth <= 0f) errors.Add("WorldWidth must be finite and positive");
        if (!float.IsFinite(WorldHeight) || WorldHeight <= 0f) errors.Add("WorldHeight must be finite and positive");
        if (!float.IsFinite(FixedDeltaTime) || FixedDeltaTime <= 0f) errors.Add("FixedDeltaTime must be finite and positive");
        return errors;
    }

    /// <summary>Reads a JSON object; omitted fields retain their defaults.</summary>
    public static FireflySettings FromJson(string json) => ConfigurationJson.Deserialize<FireflySettings>(json, config => config.Validate());

    public string ToJson() => ConfigurationJson.Serialize(this, Validate());
}
