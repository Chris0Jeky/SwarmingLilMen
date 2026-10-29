using SwarmSim.Core.Metrics;
using SwarmSim.Core.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwarmSim.Parity;

public sealed record ScenarioRecipe
{
    public int Version { get; init; } = 1;
    public string Id { get; init; } = "S1_TWO_BOID_HEAD_ON";
    public string Layout { get; init; } = "head-on";
    public int Count { get; init; } = 2;
    public int Ticks { get; init; } = 240;
    public float Width { get; init; } = 256;
    public float Height { get; init; } = 256;
    public float DeltaTime { get; init; } = 1f / 60f;
    public float TargetSpeed { get; init; } = 30;
    public float MaxForce { get; init; } = 18;
    public float SenseRadius { get; init; } = 32;
    public float SeparationRadius { get; init; } = 8;
    public float SeparationWeight { get; init; } = 1.5f;
    public float AlignmentWeight { get; init; } = 1;
    public float CohesionWeight { get; init; } = 1;
    public float LayoutRadius { get; init; } = 10;
    public float LayoutSpacing { get; init; } = 4;
    public float LayoutOffset { get; init; } = 48;
    public static ScenarioRecipe FromJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("A recipe must be an object.");
        var expected = typeof(ScenarioRecipe).GetProperties().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
            if (!expected.Contains(property.Name) || !seen.Add(property.Name))
                throw new JsonException($"Unknown or duplicate recipe property '{property.Name}'.");
        if (!seen.SetEquals(expected)) throw new JsonException("Every recipe field must be explicit.");
        var recipe = JsonSerializer.Deserialize<ScenarioRecipe>(json, new JsonSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new JsonException("A recipe cannot be null.");
        try { recipe.Validate(); }
        catch (ArgumentException error) { throw new JsonException(error.Message, error); }
        return recipe;
    }

    public void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(Id) || Id.Any(x => !char.IsAsciiLetterOrDigit(x) && x != '_'))
            throw new ArgumentException("Recipe version/identity is invalid.");
        if (Count is < 1 or > 512 || Ticks is < 1 or > 1800)
            throw new ArgumentException("Recipes are capped at 512 agents and 1800 ticks.");
        foreach (float value in new[] { Width, Height, DeltaTime, TargetSpeed, MaxForce, SenseRadius })
            if (!float.IsFinite(value) || value <= 0) throw new ArgumentException("Physical scales must be finite and positive.");
        foreach (float value in new[] { SeparationRadius, SeparationWeight, AlignmentWeight, CohesionWeight,
            LayoutRadius, LayoutSpacing, LayoutOffset })
            if (!float.IsFinite(value) || value < 0) throw new ArgumentException("Weights and layout scales must be finite and nonnegative.");
        if (SeparationRadius > SenseRadius) throw new ArgumentException("Separation radius cannot exceed the sensing radius.");
        if (Layout is not ("head-on" or "ring" or "crossing" or "uniform" or "clump"))
            throw new ArgumentException("Unknown initial layout.");
        if (Layout == "head-on" && Count != 2 || Layout == "crossing" && (Count < 2 || Count % 2 != 0))
            throw new ArgumentException("Head-on requires two agents; crossing requires an even count of at least two.");
    }

    public KinematicSample[] Generate(uint seed)
    {
        Validate();
        var result = new KinematicSample[Count];
        var streams = new RandomStreams(seed);
        string consumer = "parity.initial.v1." + Id;
        float phase = streams.CreateStream(consumer).NextFloat() * (2f * MathF.PI);
        for (int i = 0; i < Count; i++)
        {
            Pcg32Rng random = streams.CreateStream(consumer, (ulong)i);
            float x = Width * 0.5f, y = Height * 0.5f, angle;
            switch (Layout)
            {
                case "head-on":
                    result[i] = new(x + (i == 0 ? -LayoutRadius : LayoutRadius), y,
                        i == 0 ? TargetSpeed : -TargetSpeed, 0f);
                    continue;
                case "ring":
                    float radial = phase + i * (2f * MathF.PI / Count);
                    x += LayoutRadius * MathF.Cos(radial); y += LayoutRadius * MathF.Sin(radial);
                    angle = radial + MathF.PI * 0.5f;
                    break;
                case "crossing":
                    int half = Count / 2;
                    float slot = (i % half - (half - 1) * 0.5f) * LayoutSpacing;
                    if (i < half) { x -= LayoutOffset; y += slot; angle = 0f; }
                    else { x += slot; y -= LayoutOffset; angle = MathF.PI * 0.5f; }
                    break;
                case "uniform":
                    x = random.NextFloat() * Width; y = random.NextFloat() * Height;
                    angle = random.NextFloat() * (2f * MathF.PI);
                    break;
                default: // uniform area in the declared disk
                    float radius = MathF.Sqrt(random.NextFloat()) * LayoutRadius;
                    float bearing = random.NextFloat() * (2f * MathF.PI);
                    x += radius * MathF.Cos(bearing); y += radius * MathF.Sin(bearing);
                    angle = random.NextFloat() * (2f * MathF.PI);
                    break;
            }
            result[i] = new(Wrap(x, Width), Wrap(y, Height),
                MathF.Cos(angle) * TargetSpeed, MathF.Sin(angle) * TargetSpeed);
        }
        _ = new MetricFrame(result, MetricDomain.Toroidal(Width, Height));
        return result;
    }

    private static float Wrap(float x, float extent)
    {
        float value = x % extent;
        if (value < 0) value += extent;
        return value >= extent ? 0 : value;
    }
}
