using SwarmSim.Core.Configuration;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Vicsek;

/// <summary>
/// Parameters of the standard synchronous Vicsek model. The defaults are the original paper's
/// r = 1, v = 0.03 and dt = 1 at density 4 in a 10 x 10 periodic box.
/// </summary>
public sealed class VicsekSettings
{
    /// <summary>
    /// Noise amplitude eta in radians: each angular kick is uniform on [-eta/2, eta/2). It is
    /// never a fraction of 2*pi. At eta = 2*pi the new heading is independent of the neighborhood.
    /// </summary>
    public float Noise { get; init; } = 0.5f;
    public float Speed { get; init; } = 0.03f;
    public float InteractionRadius { get; init; } = 1f;
    public float WorldWidth { get; init; } = 10f;
    public float WorldHeight { get; init; } = 10f;
    public float FixedDeltaTime { get; init; } = 1f;
    /// <summary>Supported values are 0 through <see cref="Rng.MaxSupportedSeed"/>, inclusive.</summary>
    public uint Seed { get; init; } = 123456u;

    public VicsekSettings() { }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Seed > Rng.MaxSupportedSeed) errors.Add($"Seed must be between 0 and {Rng.MaxSupportedSeed}, inclusive");
        if (!float.IsFinite(Noise) || Noise < 0f || Noise > MathF.Tau) errors.Add("Noise must be a finite angle in [0, 2*pi] radians");
        if (!float.IsFinite(Speed) || Speed <= 0f) errors.Add("Speed must be finite and positive");
        if (!float.IsFinite(InteractionRadius) || InteractionRadius <= 0f) errors.Add("InteractionRadius must be finite and positive");
        if (!float.IsFinite(WorldWidth) || WorldWidth <= 0f) errors.Add("WorldWidth must be finite and positive");
        if (!float.IsFinite(WorldHeight) || WorldHeight <= 0f) errors.Add("WorldHeight must be finite and positive");
        if (!float.IsFinite(FixedDeltaTime) || FixedDeltaTime <= 0f) errors.Add("FixedDeltaTime must be finite and positive");
        return errors;
    }

    /// <summary>Reads a JSON object; omitted fields retain their defaults.</summary>
    public static VicsekSettings FromJson(string json) => ConfigurationJson.Deserialize<VicsekSettings>(json, config => config.Validate());

    /// <summary>Serializes the complete settings after validation.</summary>
    public string ToJson() => ConfigurationJson.Serialize(this, Validate());
}
