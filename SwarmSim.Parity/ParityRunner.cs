using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Parity;

public sealed record ParityRunResult([property: JsonRequired] string Scenario, [property: JsonRequired] uint Seed,
    [property: JsonRequired] string Model, [property: JsonRequired] string RequestedInitialHash,
    [property: JsonRequired] string InitialHash, [property: JsonRequired] string FinalHash,
    [property: JsonRequired] string StateTraceHash, [property: JsonRequired] string CsvHash,
    [property: JsonRequired] double?[] WindowMeans, [property: JsonRequired] int[] WindowFiniteCounts);

public static class ParityRunner
{
    public static IReadOnlyList<string> Models { get; } = Array.AsReadOnly(new[] { "enhanced", "classic", "legacy", "reference" });
    public static IReadOnlyList<uint> Seeds { get; } = Array.AsReadOnly(new uint[] { 7, 19, 42, 73, 101 });
    public static IReadOnlyList<string> Channels { get; } = Array.AsReadOnly(new[] {
        "population", "meanSpeed", "polarization", "nearestNeighborMean", "nearestNeighborP50",
        "headingCircularVariance", "turnRateMean", "turnRateP50", "turnRateP95", "flockRadius",
        "neighborCountMean", "neighborCountP50", "neighborCountP95", "collisionPairs", "minSpeed", "maxSpeed" });

    public static (string Requested, string Actual) InitialIdentities(ScenarioRecipe recipe, uint seed, string model)
    {
        KinematicSample[] requested = recipe.Generate(seed);
        IParityModel simulation = ParityModels.Create(model, recipe, seed, requested, naive: false, alignmentDisabled: false);
        var initial = new KinematicSample[recipe.Count];
        simulation.Capture(initial);
        return (StateHash(requested), StateHash(initial));
    }

    public static ParityRunResult Run(ScenarioRecipe recipe, uint seed, string model,
        string? outputDirectory = null, bool naiveIndex = false, bool alignmentDisabled = false)
    {
        recipe.Validate();
        KinematicSample[] requested = recipe.Generate(seed);
        IParityModel simulation = ParityModels.Create(model, recipe, seed, requested, naiveIndex, alignmentDisabled);
        var state = new KinematicSample[recipe.Count];
        simulation.Capture(state);
        KinematicSample[] initial = (KinematicSample[])state.Clone();
        string requestedHash = StateHash(requested), initialHash = StateHash(state);
        var geometry = new ParityGeometryProbe(recipe.Count, recipe.SenseRadius, recipe.DeltaTime);
        var registry = new MetricRegistry();
        registry.Register(new MotionMetricProbe(includeCentroids: false));
        registry.Register(new ExactNearestNeighborProbe(recipe.Count));
        registry.Register(geometry);
        MetricPlan plan = registry.CreatePlan(Channels.ToArray());
        double[] row = new double[plan.ChannelCount], sums = new double[plan.ChannelCount];
        int[] finite = new int[plan.ChannelCount];
        int windowStart = recipe.Ticks * 4 / 5;
        using var trace = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendState(trace, state);
        using var bytes = new MemoryStream();
        using var writer = new StreamWriter(bytes, new UTF8Encoding(false), 16_384, leaveOpen: true);
        var csv = new MetricCsvWriter(writer, plan);
        for (int tick = 1; tick <= recipe.Ticks; tick++)
        {
            geometry.SetPrevious(state);
            simulation.Step();
            simulation.Capture(state);
            var frame = new MetricFrame(state, MetricDomain.Toroidal(recipe.Width, recipe.Height));
            plan.Sample(frame, row);
            if (model is "classic" or "reference" &&
                (row[14] < recipe.TargetSpeed * 0.9999 || row[15] > recipe.TargetSpeed * 1.0001))
                throw new InvalidOperationException("A constant-speed comparison model violated its speed bound.");
            csv.WriteRow(tick, row);
            AppendState(trace, state);
            if (tick > windowStart)
                for (int channel = 0; channel < row.Length; channel++)
                    if (double.IsFinite(row[channel])) { sums[channel] += row[channel]; finite[channel]++; }
        }
        writer.Flush();
        byte[] contents = bytes.ToArray();
        var means = new double?[row.Length];
        for (int channel = 0; channel < row.Length; channel++)
            means[channel] = finite[channel] == 0 ? null : sums[channel] / finite[channel];
        var result = new ParityRunResult(recipe.Id, seed, model, requestedHash, initialHash, StateHash(state),
            Convert.ToHexString(trace.GetHashAndReset()), Convert.ToHexString(SHA256.HashData(contents)), means, finite);
        if (outputDirectory is not null)
        {
            if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
                throw new IOException("Parity output must be a new directory.");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllBytes(Path.Combine(outputDirectory, "metrics.csv"), contents);
            WriteInitial(Path.Combine(outputDirectory, "requested-initial.csv"), requested);
            WriteInitial(Path.Combine(outputDirectory, "initial.csv"), initial);
            File.WriteAllText(Path.Combine(outputDirectory, "configuration.json"), simulation.ConfigurationJson + "\n");
        }
        return result;
    }

    private static void WriteInitial(string path, KinematicSample[] state)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        writer.WriteLine("id,x,y,vx,vy");
        for (int i = 0; i < state.Length; i++)
        {
            KinematicSample a = state[i];
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{i},{a.X:R},{a.Y:R},{a.Vx:R},{a.Vy:R}"));
        }
    }

    private static string StateHash(KinematicSample[] state)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendState(hash, state);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendState(IncrementalHash hash, KinematicSample[] state)
    {
        Span<byte> encoded = stackalloc byte[16];
        foreach (KinematicSample a in state)
        {
            BinaryPrimitives.WriteSingleLittleEndian(encoded, a.X); BinaryPrimitives.WriteSingleLittleEndian(encoded[4..], a.Y);
            BinaryPrimitives.WriteSingleLittleEndian(encoded[8..], a.Vx); BinaryPrimitives.WriteSingleLittleEndian(encoded[12..], a.Vy);
            hash.AppendData(encoded);
        }
    }
}
