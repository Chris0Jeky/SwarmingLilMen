using System.Globalization;

namespace SwarmSim.Cli;

internal sealed record SweepCellSummary(int Index, double[] Terminal, double[] Mean, long[] Samples);

internal static class SweepSummary
{
    internal static SweepCellSummary Read(SweepCell cell, IReadOnlyList<string> metrics, string path)
    {
        using var input = new StreamReader(path);
        if (input.ReadLine() != "tick," + string.Join(',', metrics))
            throw new InvalidDataException($"Cell {cell.Id} has an unexpected metric schema.");
        var terminal = new double[metrics.Count];
        var sums = new double[metrics.Count];
        var counts = new long[metrics.Count];
        long expectedTick = 0;
        string? line;
        while ((line = input.ReadLine()) is not null)
        {
            string[] columns = line.Split(',');
            if (columns.Length != metrics.Count + 1 ||
                !long.TryParse(columns[0], NumberStyles.None, CultureInfo.InvariantCulture, out long tick) || tick != expectedTick)
                throw new InvalidDataException($"Cell {cell.Id} has an invalid metric row at tick {expectedTick}.");
            for (int channel = 0; channel < metrics.Count; channel++)
            {
                if (columns[channel + 1].Length == 0)
                {
                    terminal[channel] = double.NaN;
                    continue;
                }
                if (!double.TryParse(columns[channel + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                    throw new InvalidDataException($"Cell {cell.Id} has an invalid value for {metrics[channel]}.");
                terminal[channel] = value;
                sums[channel] += value;
                if (!double.IsFinite(sums[channel]))
                    throw new InvalidDataException($"Cell {cell.Id} overflows the summary sum for {metrics[channel]}.");
                counts[channel]++;
            }
            expectedTick++;
        }
        if (expectedTick != (long)cell.Request.Ticks + 1)
            throw new InvalidDataException($"Cell {cell.Id} does not contain all requested metric ticks.");
        for (int channel = 0; channel < metrics.Count; channel++)
            sums[channel] = counts[channel] == 0 ? double.NaN : sums[channel] / counts[channel];
        return new SweepCellSummary(cell.Index, terminal, sums, counts);
    }

    internal static void Write(TextWriter output, SweepPlan plan, IEnumerable<SweepCellSummary> results)
    {
        SweepCellSummary[] ordered = results.OrderBy(result => result.Index).ToArray();
        if (ordered.Length != plan.Cells.Count)
            throw new InvalidDataException("Sweep summary requires exactly one result for every planned cell.");
        output.Write("cellId,engine,seed,ticks,agents");
        foreach (string parameter in plan.Parameters) output.Write(',' + parameter);
        foreach (string metric in plan.Metrics)
            output.Write($",{metric}.terminal,{metric}.mean,{metric}.samples");
        output.Write('\n');
        for (int index = 0; index < ordered.Length; index++)
        {
            SweepCell cell = plan.Cells[index];
            SweepCellSummary result = ordered[index];
            if (result.Index != index || result.Terminal.Length != plan.Metrics.Count ||
                result.Mean.Length != plan.Metrics.Count || result.Samples.Length != plan.Metrics.Count)
                throw new InvalidDataException("Sweep summary result IDs or channel counts do not match the plan.");
            output.Write(FormattableString.Invariant($"{cell.Id},{cell.Request.Engine},{cell.Request.Seed},{cell.Request.Ticks},{cell.Request.AgentCount}"));
            foreach (string value in cell.ParameterValues) output.Write(',' + value);
            for (int channel = 0; channel < plan.Metrics.Count; channel++)
            {
                output.Write(',');
                WriteDefined(output, result.Terminal[channel]);
                output.Write(',');
                WriteDefined(output, result.Mean[channel]);
                output.Write(',');
                output.Write(result.Samples[channel].ToString(CultureInfo.InvariantCulture));
            }
            output.Write('\n');
        }
    }

    private static void WriteDefined(TextWriter output, double value)
    {
        if (double.IsInfinity(value)) throw new InvalidDataException("A sweep summary cannot contain infinity.");
        if (!double.IsNaN(value)) output.Write(value.ToString("R", CultureInfo.InvariantCulture));
    }
}
