using SwarmSim.Core.Metrics;

namespace SwarmSim.Parity;

public sealed class ReynoldsReference
{
    private readonly ScenarioRecipe _recipe;
    private KinematicSample[] _current;
    private KinematicSample[] _next;

    public ReynoldsReference(ScenarioRecipe recipe, ReadOnlySpan<KinematicSample> initial)
    {
        recipe.Validate();
        if (initial.Length != recipe.Count) throw new ArgumentException("Initial population does not match the recipe.");
        _recipe = recipe;
        _current = initial.ToArray();
        _next = new KinematicSample[initial.Length];
        for (int i = 0; i < _current.Length; i++)
        {
            KinematicSample agent = _current[i];
            if (!float.IsFinite(agent.X) || !float.IsFinite(agent.Y) || !float.IsFinite(agent.Vx) || !float.IsFinite(agent.Vy))
                throw new ArgumentException("Initial state must be finite.");
            F2 velocity = new F2(agent.Vx, agent.Vy).AtLength(recipe.TargetSpeed);
            _current[i] = new(Wrap(agent.X, recipe.Width), Wrap(agent.Y, recipe.Height), velocity.X, velocity.Y);
        }
    }

    public ReadOnlySpan<KinematicSample> Agents => _current;

    public void Step()
    {
        for (int i = 0; i < _current.Length; i++)
        {
            KinematicSample self = _current[i];
            F2 separation = default, velocitySum = default, displacementSum = default;
            int separated = 0, sensed = 0;
            for (int j = 0; j < _current.Length; j++)
            {
                if (j == i) continue;
                KinematicSample other = _current[j];
                F2 delta = new(Periodic(other.X - self.X, _recipe.Width), Periodic(other.Y - self.Y, _recipe.Height));
                float squared = delta.Squared;
                // The demo excludes coincident neighbors. Inclusive outer radius is a declared adaptation.
                if (squared == 0f || squared > _recipe.SenseRadius * _recipe.SenseRadius) continue;
                float distance = MathF.Sqrt(squared);
                if (distance < _recipe.SeparationRadius && _recipe.SeparationRadius > 0f)
                {
                    float influence = MathF.Max(0f, 1f - distance / _recipe.SeparationRadius) / distance;
                    separation += (delta / -distance) * influence;
                    separated++;
                }
                velocitySum += new F2(other.Vx, other.Vy);
                displacementSum += delta;
                sensed++;
            }
            F2 velocity = new(self.Vx, self.Vy), force = default;
            // Keep the demo's count-vs-zero-vector distinction: a symmetric separation or
            // alignment sum can produce braking; canonical has its own near-zero guards.
            if (separated > 0)
                force += (separation.AtLength(_recipe.TargetSpeed * _recipe.SeparationWeight) - velocity).Limited(_recipe.MaxForce);
            if (sensed > 0)
            {
                force += ((velocitySum / sensed).AtLength(_recipe.TargetSpeed * _recipe.AlignmentWeight) - velocity).Limited(_recipe.MaxForce);
                F2 center = displacementSum / sensed;
                if (center.Squared > 0)
                    force += (center.AtLength(_recipe.TargetSpeed * _recipe.CohesionWeight) - velocity).Limited(_recipe.MaxForce);
            }
            velocity = (velocity + force * _recipe.DeltaTime).AtLength(_recipe.TargetSpeed);
            _next[i] = new(Wrap(self.X + velocity.X * _recipe.DeltaTime, _recipe.Width),
                Wrap(self.Y + velocity.Y * _recipe.DeltaTime, _recipe.Height), velocity.X, velocity.Y);
        }
        (_current, _next) = (_next, _current);
    }

    private static float Periodic(float delta, float extent)
    {
        float result = delta % extent;
        if (result > extent * 0.5f) result -= extent;
        if (result < -extent * 0.5f) result += extent;
        return result;
    }

    private static float Wrap(float coordinate, float extent)
    {
        float result = coordinate % extent;
        if (result < 0) result += extent;
        return result >= extent ? 0 : result;
    }

    private readonly record struct F2(float X, float Y)
    {
        public float Squared => X * X + Y * Y;
        public F2 AtLength(float length) => Squared == 0 ? default : this / MathF.Sqrt(Squared) * length;
        public F2 Limited(float maximum) => Squared <= maximum * maximum ? this : AtLength(maximum);
        public static F2 operator +(F2 a, F2 b) => new(a.X + b.X, a.Y + b.Y);
        public static F2 operator -(F2 a, F2 b) => new(a.X - b.X, a.Y - b.Y);
        public static F2 operator *(F2 a, float scale) => new(a.X * scale, a.Y * scale);
        public static F2 operator /(F2 a, float scale) => new(a.X / scale, a.Y / scale);
    }
}
