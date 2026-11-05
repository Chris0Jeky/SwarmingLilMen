using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.Simulation;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.ReactionDiffusion;

/// <summary>Concentrations in one field cell; neither component represents an agent or a velocity.</summary>
public readonly record struct CellState(float U, float V);

/// <summary>
/// Field-only Gray-Scott reaction diffusion with a periodic five-point stencil and synchronous
/// explicit Euler integration. Grid spacing is one. State is binary32; all update intermediates
/// are binary64. Every next cell is checked before commit, with no silent concentration clipping.
/// See docs/REACTION_DIFFUSION.md for the equation, stability guard and initialization contract.
/// </summary>
public sealed class ReactionDiffusionWorld
{
    public const string PatchStream = "reaction-diffusion.patch";
    public const string ModelStateProfile = "reaction-diffusion-state-v1";

    private readonly DoubleBufferedState<CellState> _state;
    private readonly SimulationSchedule _schedule;

    public ReactionDiffusionWorld(ReactionDiffusionSettings settings)
        : this(settings, Initialize(settings)) { }

    /// <summary>Copies exactly Columns * Rows nonnegative finite cells in row-major order.</summary>
    public ReactionDiffusionWorld(ReactionDiffusionSettings settings, ReadOnlySpan<CellState> cells)
        : this(settings, Copy(settings, cells)) { }

    private ReactionDiffusionWorld(ReactionDiffusionSettings settings, CellState[] cells)
    {
        Settings = settings;
        if (DepletionBound(settings, cells) > 1d)
            throw new ArgumentException("Initial V requires a smaller FixedDeltaTime: dt * (4 * DiffusionU + Feed + ReactionRate * max(V)^2) must not exceed 1.", nameof(cells));
        _state = DoubleBufferedState<CellState>.TakeOwnership(cells);
        _schedule = new SimulationSchedule(
            new SimulationPhase("stability", RequireStableStep),
            new SimulationPhase("reaction-diffusion", ComputeNext),
            new SimulationPhase("commit", CommitTick));
    }

    public ReactionDiffusionSettings Settings { get; }
    public int Columns => Settings.Columns;
    public int Rows => Settings.Rows;
    public int CellCount => _state.Count;
    public int AgentCount => 0;
    public long TickCount { get; private set; }
    /// <summary>Borrowed row-major field state, valid until the next successful commit.</summary>
    public ReadOnlySpan<CellState> Cells => _state.Current;
    public IReadOnlyList<string> PhaseNames => _schedule.PhaseNames;

    public void Step() => _schedule.Run();

    private void RequireStableStep()
    {
        if (DepletionBound(Settings, Cells) > 1d)
            throw new InvalidOperationException("The current field requires a smaller FixedDeltaTime: dt * (4 * DiffusionU + Feed + ReactionRate * max(V)^2) exceeds 1. No new field was committed.");
    }

    private static double DepletionBound(ReactionDiffusionSettings settings, ReadOnlySpan<CellState> cells)
    {
        double maxV = 0d;
        foreach (CellState cell in cells)
            maxV = Math.Max(maxV, cell.V);
        return settings.FixedDeltaTime * (4d * settings.DiffusionU + settings.Feed
            + settings.ReactionRate * (maxV * maxV));
    }

    private void ComputeNext()
    {
        ReadOnlySpan<CellState> current = Cells;
        Span<CellState> next = _state.Next;
        double dt = Settings.FixedDeltaTime;
        double du = Settings.DiffusionU, dv = Settings.DiffusionV;
        double feed = Settings.Feed, kill = Settings.Kill, rate = Settings.ReactionRate;
        double diffusionStepU = dt * du, diffusionStepV = dt * dv;
        double retainedV = 1d - dt * (4d * dv + feed + kill);
        int columns = Columns, rows = Rows;
        for (int y = 0; y < rows; y++)
        {
            int row = y * columns;
            int north = (y == 0 ? rows - 1 : y - 1) * columns;
            int south = (y + 1 == rows ? 0 : y + 1) * columns;
            for (int x = 0; x < columns; x++)
            {
                int index = row + x;
                CellState self = current[index];
                CellState left = current[row + (x == 0 ? columns - 1 : x - 1)];
                CellState right = current[row + (x + 1 == columns ? 0 : x + 1)];
                CellState up = current[north + x], down = current[south + x];
                double sumU = (double)left.U + right.U + up.U + down.U;
                double sumV = (double)left.V + right.V + up.V + down.V;
                double reactionMultiplier = rate * ((double)self.V * self.V);
                double reaction = self.U * reactionMultiplier;
                // Algebraically the standard Euler equation, grouped into nonnegative terms.
                // This avoids subtractive negative roundoff at the permitted depletion boundary.
                double u = self.U * (1d - dt * (4d * du + feed + reactionMultiplier))
                    + diffusionStepU * sumU + dt * feed;
                double v = self.V * retainedV + diffusionStepV * sumV + dt * reaction;
                if (!double.IsFinite(u) || !double.IsFinite(v) || u < 0d || v < 0d)
                    throw new InvalidOperationException("The proposed field contains an invalid concentration. No new field was committed; reduce the timestep or initial concentrations.");
                // Validate representability after the ordinary binary32 rounding operation.
                // A double just above float.MaxValue may still round to that finite value.
                float storedU = (float)u, storedV = (float)v;
                if (!float.IsFinite(storedU) || !float.IsFinite(storedV))
                    throw new InvalidOperationException("The proposed field overflows stored concentration range. No new field was committed; reduce the timestep or initial concentrations.");
                next[index] = new CellState(storedU, storedV);
            }
        }
    }

    private void CommitTick()
    {
        _state.Commit();
        TickCount++;
    }

    /// <summary>No kinematic agents exist. Returns zero without reading or changing the destination.</summary>
    public int CopyKinematics(Span<KinematicSample> destination) => 0;

    /// <summary>
    /// Versioned settings, tick and ordered U/V bits. Initialization RNG is discarded and the
    /// next buffer is overwritten before commit. Observation only, not a restorable checkpoint;
    /// do not call concurrently with mutation.
    /// </summary>
    public ModelStateHash StateHash()
    {
        using var hash = new StateHashWriter(ModelStateProfile);
        hash.WriteInt32(Settings.Columns);
        hash.WriteInt32(Settings.Rows);
        hash.WriteSingle(Settings.DiffusionU);
        hash.WriteSingle(Settings.DiffusionV);
        hash.WriteSingle(Settings.ReactionRate);
        hash.WriteSingle(Settings.Feed);
        hash.WriteSingle(Settings.Kill);
        hash.WriteSingle(Settings.FixedDeltaTime);
        hash.WriteSingle(Settings.PatchFraction);
        hash.WriteSingle(Settings.PatchNoise);
        hash.WriteUInt32(Settings.Seed);
        hash.WriteInt64(TickCount);
        hash.WriteInt32(CellCount);
        foreach (CellState cell in Cells)
        {
            hash.WriteSingle(cell.U);
            hash.WriteSingle(cell.V);
        }
        return hash.Finish();
    }

    private static CellState[] Initialize(ReactionDiffusionSettings settings)
    {
        Validate(settings);
        var cells = new CellState[settings.Columns * settings.Rows];
        Array.Fill(cells, new CellState(1f, 0f));
        if (settings.PatchFraction == 0f) return cells;
        int width = Math.Max(1, (int)Math.Ceiling(settings.Columns * (double)settings.PatchFraction));
        int height = Math.Max(1, (int)Math.Ceiling(settings.Rows * (double)settings.PatchFraction));
        int startX = (settings.Columns - width) / 2, startY = (settings.Rows - height) / 2;
        Pcg32Rng random = new RandomStreams(settings.Seed).CreateStream(PatchStream);
        for (int y = startY; y < startY + height; y++)
            for (int x = startX; x < startX + width; x++)
            {
                float u = (float)(0.5d + (random.NextFloat() * 2d - 1d) * settings.PatchNoise);
                float v = (float)(0.25d + (random.NextFloat() * 2d - 1d) * settings.PatchNoise);
                cells[y * settings.Columns + x] = new CellState(u, v);
            }
        return cells;
    }

    private static CellState[] Copy(ReactionDiffusionSettings settings, ReadOnlySpan<CellState> cells)
    {
        Validate(settings);
        if (cells.Length != settings.Columns * settings.Rows)
            throw new ArgumentException("Initial field length must equal Columns times Rows.", nameof(cells));
        foreach (CellState cell in cells)
            if (!float.IsFinite(cell.U) || !float.IsFinite(cell.V) || cell.U < 0f || cell.V < 0f)
                throw new ArgumentException("Initial concentrations must be finite and nonnegative.", nameof(cells));
        return cells.ToArray();
    }

    private static void Validate(ReactionDiffusionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        List<string> errors = settings.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid reaction-diffusion settings: {string.Join(", ", errors)}", nameof(settings));
    }
}
