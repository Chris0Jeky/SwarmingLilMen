using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Parity;

internal interface IParityModel
{
    string ConfigurationJson { get; }
    void Step();
    void Capture(Span<KinematicSample> destination);
}

internal static class ParityModels
{
    internal static IParityModel Create(string model, ScenarioRecipe recipe, uint seed,
        KinematicSample[] initial, bool naive, bool alignmentDisabled)
    {
        if (naive && model is not ("enhanced" or "classic")) throw new ArgumentException("Only canonical models have a selectable index.");
        if (alignmentDisabled && model != "classic") throw new ArgumentException("The alignment mutation applies only to classic.");
        return model switch
        {
            "enhanced" or "classic" => new CanonicalModel(recipe, seed, initial, model == "classic", naive, alignmentDisabled),
            "legacy" => new LegacyModel(recipe, seed, initial),
            "reference" => new ReferenceModel(recipe, initial),
            _ => throw new ArgumentException($"Unknown parity model '{model}'.")
        };
    }

    private sealed class CanonicalModel : IParityModel
    {
        private readonly CanonicalWorld _world;
        private readonly float _dt;
        public string ConfigurationJson => _world.Settings.ToJson();
        internal CanonicalModel(ScenarioRecipe r, uint seed, KinematicSample[] initial, bool classic, bool naive, bool alignmentDisabled)
        {
            var settings = new CanonicalWorldSettings
            {
                Mode = classic ? CanonicalMode.ClassicReynolds : CanonicalMode.Enhanced,
                InitialCapacity = r.Count, WorldWidth = r.Width, WorldHeight = r.Height,
                FixedDeltaTime = r.DeltaTime, TargetSpeed = r.TargetSpeed, MaxForce = r.MaxForce,
                SenseRadius = r.SenseRadius, SeparationRadius = r.SeparationRadius,
                SeparationWeight = r.SeparationWeight, AlignmentWeight = r.AlignmentWeight, CohesionWeight = r.CohesionWeight,
                FieldOfView = classic ? 360 : 270, MaxNeighbors = Math.Max(4, r.Count), WanderStrength = 0, Seed = seed
            };
            ISpatialIndex index = naive ? new NaiveSpatialIndex(r.Width, r.Height) : new GridSpatialIndex(r.SenseRadius, r.Width, r.Height);
            _world = new CanonicalWorld(settings, index);
            _dt = r.DeltaTime;
            foreach (KinematicSample a in initial)
                if (!_world.TryAddBoid(new Vec2(a.X, a.Y), new Vec2(a.Vx, a.Vy)))
                    throw new InvalidOperationException("Canonical initialization rejected an agent.");
            if (alignmentDisabled) _world.SetPolicyEnabled(CanonicalPolicyIds.Alignment, false);
        }
        public void Step()
        {
            _world.Step(_dt);
            if (_world.Instrumentation.TruncatedCount != 0) throw new InvalidOperationException("A parity recipe truncated a canonical query.");
        }
        public void Capture(Span<KinematicSample> destination)
        {
            if (destination.Length != _world.Count) throw new InvalidOperationException("Parity population changed.");
            for (int i = 0; i < _world.Count; i++)
            {
                Boid b = _world.Boids[i];
                destination[i] = new(b.Position.X, b.Position.Y, b.Velocity.X, b.Velocity.Y);
            }
        }
    }

    private sealed class LegacyModel : IParityModel
    {
        private readonly World _world;
        public string ConfigurationJson => _world.Config.ToJson();
        internal LegacyModel(ScenarioRecipe r, uint seed, KinematicSample[] initial)
        {
            _world = new World(new SimConfig
            {
                InitialCapacity = r.Count, MaxCapacity = r.Count, WorldWidth = r.Width, WorldHeight = r.Height,
                FixedDeltaTime = r.DeltaTime, MaxSpeed = r.TargetSpeed, MaxForce = r.MaxForce,
                SenseRadius = r.SenseRadius, SeparationRadius = r.SeparationRadius, GridCellSize = r.SenseRadius,
                SeparationWeight = r.SeparationWeight, AlignmentWeight = r.AlignmentWeight, CohesionWeight = r.CohesionWeight,
                FieldOfView = 270, MaxNeighbors = Math.Max(4, r.Count), WanderStrength = 0,
                Seed = seed, BoundaryMode = BoundaryMode.Wrap, SpeedModel = SpeedModel.ConstantSpeed,
                BaseDrain = 0, AttackDamage = 0, AggressionMatrix = SimConfig.NeutralAggressionMatrix(1)
            });
            foreach (KinematicSample a in initial)
            {
                int i = _world.AddAgent(a.X, a.Y, 0);
                if (i < 0) throw new InvalidOperationException("Legacy initialization rejected an agent.");
                float speed = MathF.Sqrt(a.Vx * a.Vx + a.Vy * a.Vy);
                _world.Vx[i] = a.Vx / speed * r.TargetSpeed;
                _world.Vy[i] = a.Vy / speed * r.TargetSpeed;
            }
        }
        public void Step() => _world.Tick();
        public void Capture(Span<KinematicSample> destination)
        {
            if (destination.Length != _world.Count) throw new InvalidOperationException("Parity population changed.");
            for (int i = 0; i < _world.Count; i++)
            {
                if ((_world.State[i] & AgentState.Dead) != 0) throw new InvalidOperationException("Parity requires a fixed live population.");
                destination[i] = new(_world.X[i], _world.Y[i], _world.Vx[i], _world.Vy[i]);
            }
        }
    }

    private sealed class ReferenceModel : IParityModel
    {
        private readonly ReynoldsReference _reference;
        private readonly ScenarioRecipe _recipe;
        public string ConfigurationJson => System.Text.Json.JsonSerializer.Serialize(_recipe, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        internal ReferenceModel(ScenarioRecipe recipe, KinematicSample[] initial) { _recipe = recipe; _reference = new(recipe, initial); }
        public void Step() => _reference.Step();
        public void Capture(Span<KinematicSample> destination) => _reference.Agents.CopyTo(destination);
    }
}
