using SwarmSim.Core.Configuration;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.ReactionDiffusion;

/// <summary>
/// A periodic, unit-spaced Gray-Scott grid. Nonnegative rates use simulation time units.
/// ReactionRate = 1 gives the usual model; ReactionRate = Feed = Kill = 0 isolates diffusion.
/// Validation enforces the linear explicit-Euler depletion bounds. The world additionally
/// checks the current V-dependent reaction bound before every step; values are never clamped.
/// </summary>
public sealed class ReactionDiffusionSettings
{
    public const int MaxCells = 1 << 20;

    public int Columns { get; init; } = 64;
    public int Rows { get; init; } = 64;
    public float DiffusionU { get; init; } = 0.16f;
    public float DiffusionV { get; init; } = 0.08f;
    public float ReactionRate { get; init; } = 1f;
    public float Feed { get; init; } = 0.035f;
    public float Kill { get; init; } = 0.065f;
    public float FixedDeltaTime { get; init; } = 0.5f;
    /// <summary>Fraction of each grid dimension occupied by the centred patch; zero starts at equilibrium.</summary>
    public float PatchFraction { get; init; } = 0.2f;
    /// <summary>Independent additive uniform perturbation half-width around patch U = 0.5 and V = 0.25.</summary>
    public float PatchNoise { get; init; } = 0.02f;
    public uint Seed { get; init; } = 123456u;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Columns <= 0 || Rows <= 0) errors.Add("Columns and Rows must be positive");
        else if ((long)Columns * Rows > MaxCells) errors.Add($"Columns times Rows must not exceed {MaxCells}");
        if (Seed > Rng.MaxSupportedSeed) errors.Add($"Seed must be between 0 and {Rng.MaxSupportedSeed}, inclusive");
        void Nonnegative(float value, string name)
        {
            if (!float.IsFinite(value) || value < 0f) errors.Add($"{name} must be finite and nonnegative");
        }
        Nonnegative(DiffusionU, nameof(DiffusionU));
        Nonnegative(DiffusionV, nameof(DiffusionV));
        Nonnegative(ReactionRate, nameof(ReactionRate));
        Nonnegative(Feed, nameof(Feed));
        Nonnegative(Kill, nameof(Kill));
        if (!float.IsFinite(FixedDeltaTime) || FixedDeltaTime <= 0f)
            errors.Add("FixedDeltaTime must be finite and positive");
        if (!float.IsFinite(PatchFraction) || PatchFraction < 0f || PatchFraction > 1f)
            errors.Add("PatchFraction must be finite and in [0, 1]");
        if (!float.IsFinite(PatchNoise) || PatchNoise < 0f || PatchNoise > 0.25f)
            errors.Add("PatchNoise must be finite and in [0, 0.25]");
        // Four directed stencil links per cell. The same conservative bound applies to thin grids.
        if ((double)FixedDeltaTime * (4d * DiffusionU + Feed) > 1d)
            errors.Add("FixedDeltaTime * (4 * DiffusionU + Feed) must not exceed 1");
        if ((double)FixedDeltaTime * (4d * DiffusionV + Feed + Kill) > 1d)
            errors.Add("FixedDeltaTime * (4 * DiffusionV + Feed + Kill) must not exceed 1");
        return errors;
    }

    public static ReactionDiffusionSettings FromJson(string json) =>
        ConfigurationJson.Deserialize<ReactionDiffusionSettings>(json, settings => settings.Validate());

    public string ToJson() => ConfigurationJson.Serialize(this, Validate());
}
