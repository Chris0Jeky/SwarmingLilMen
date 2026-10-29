using System.Text;

namespace SwarmSim.Cli;

internal static class SweepRunner
{
    internal static string Run(string sweepFile, string outputDirectory) =>
        Run(SweepPlan.Load(sweepFile), outputDirectory);

    internal static string Run(SweepPlan plan, string outputDirectory,
        Func<RunRequest, string, string>? executeCell = null)
    {
        Func<RunRequest, string, string> runCell = executeCell ?? ExperimentRunner.Run;
        string parent = Path.GetFullPath(outputDirectory);
        string sweepId = "sweep-" + Guid.NewGuid().ToString("N");
        string staging = Path.Combine(parent, "." + sweepId + ".tmp");
        string destination = Path.Combine(parent, sweepId);
        // The entire plan has already passed resolution, construction and initial metrics.
        Directory.CreateDirectory(parent);
        if (Directory.Exists(staging) || File.Exists(staging) || Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("Generated sweep directory already exists; no files were changed.");
        Directory.CreateDirectory(staging);
        try
        {
            string work = Path.Combine(staging, ".cell-runs");
            var results = new SweepCellSummary[plan.Cells.Count];
            foreach (SweepCell cell in plan.Cells)
            {
                string completed = runCell(cell.Request, work);
                string cellPath = Path.Combine(staging, cell.Id);
                // The normal run ID remains inside the manifest. Only its containing directory
                // receives the stable planned cell ID; manifest replay never relies on its path.
                Directory.Move(completed, cellPath);
                results[cell.Index] = SweepSummary.Read(cell, plan.Metrics, Path.Combine(cellPath, "metrics.csv"));
            }
            Directory.Delete(work); // Empty: every completed run was moved to its cell directory.
            using (var output = new StreamWriter(new FileStream(Path.Combine(staging, "summary.csv"),
                       FileMode.CreateNew, FileAccess.Write, FileShare.None), new UTF8Encoding(false)))
                SweepSummary.Write(output, plan, results);
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            throw;
        }
    }
}
