using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SwarmSim.Cli;

internal static class ExperimentRunner
{
    internal static string Run(RunOptions options)
    {
        RunRequest request = RunRequest.Resolve(options);
        // Serialize and construct before touching output; this also catches non-serializable
        // reserved values and index/capacity limits that scalar validation cannot establish.
        using JsonDocument resolved = JsonDocument.Parse(request.ConfigJson);
        var simulation = new PreparedSimulation(request);
        string parent = Path.GetFullPath(options.OutputDirectory);
        (string? commit, bool? dirty) = RunProvenance.Git();
        RuntimeIdentity runtime = RunProvenance.Runtime();
        DateTimeOffset createdUtc = DateTimeOffset.UtcNow;
        string runId = "run-" + Guid.NewGuid().ToString("N");
        string staging = Path.Combine(parent, "." + runId + ".tmp");
        string destination = Path.Combine(parent, runId);

        Directory.CreateDirectory(parent);
        if (Directory.Exists(staging) || File.Exists(staging) || Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("Generated run directory already exists; no files were changed.");
        Directory.CreateDirectory(staging);
        try
        {
            var elapsed = Stopwatch.StartNew();
            string metricsPath = Path.Combine(staging, "metrics.csv");
            using (StreamWriter metrics = CreateText(metricsPath))
            {
                metrics.WriteLine("tick,population,meanSpeed,polarization");
                simulation.InitialMetrics.Write(metrics);
                // Count upward from zero so int.MaxValue ticks does not overflow after the last step.
                for (int index = 0; index < request.Ticks; index++)
                {
                    simulation.Step();
                    simulation.Measure(index + 1).Write(metrics);
                }
            }
            elapsed.Stop();
            string stateHash = simulation.KinematicHash();
            string metricsHash;
            using (FileStream metricsFile = File.OpenRead(metricsPath))
                metricsHash = Convert.ToHexString(SHA256.HashData(metricsFile));
            if (request.Replay is not null &&
                (!string.Equals(request.Replay.FinalKinematicHash, stateHash, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(request.Replay.MetricsSha256, metricsHash, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Replay hash verification failed; no completed run was published. Check binary/runtime and recorded configuration.");

            var manifest = new RunManifest
            {
                SchemaVersion = 1,
                RunId = runId,
                CreatedUtc = createdUtc,
                GitCommit = commit,
                DirtyWorktree = dirty,
                Engine = request.Engine,
                Seed = request.Seed,
                Ticks = request.Ticks,
                AgentCount = request.AgentCount,
                FixedDeltaTime = request.FixedDeltaTime,
                ResolvedConfig = resolved.RootElement.Clone(),
                RequestedMetrics = RunManifest.SupportedMetrics.ToArray(),
                SpawnModel = RunManifest.SupportedSpawnModel,
                KinematicHashVersion = 1,
                FinalKinematicHash = stateHash,
                MetricsSha256 = metricsHash,
                Runtime = runtime
            };
            using (StreamWriter manifestFile = CreateText(Path.Combine(staging, "manifest.json")))
                manifestFile.WriteLine(JsonSerializer.Serialize(manifest, RunManifest.JsonOptions));
            using (StreamWriter log = CreateText(Path.Combine(staging, "log.txt")))
            {
                log.WriteLine($"Run: {runId}");
                log.WriteLine($"Engine: {request.Engine}");
                log.WriteLine(FormattableString.Invariant($"Seed: {request.Seed}; ticks: {request.Ticks}; agents: {request.AgentCount}; fixedDeltaTime: {request.FixedDeltaTime:R}"));
                log.WriteLine($"FinalKinematicHash v1: {stateHash}");
                log.WriteLine($"MetricsSha256: {metricsHash}");
                log.WriteLine($"ElapsedRunMilliseconds (includes metrics I/O): {elapsed.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)}");
                if (request.Replay is not null)
                    log.WriteLine($"Verified replay of {request.Replay.RunId}");
            }

            Directory.Move(staging, destination); // Same-parent publish; fails if destination exists.
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    private static StreamWriter CreateText(string path) => new(
        new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None),
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { NewLine = "\n" };
}
