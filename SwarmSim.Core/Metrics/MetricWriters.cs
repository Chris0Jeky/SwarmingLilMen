using System.Globalization;
using System.IO;

namespace SwarmSim.Core.Metrics;

/// <summary>Invariant CSV with manifest-selected column order and LF line endings. Does not own the writer.</summary>
public sealed class MetricCsvWriter
{
    private readonly TextWriter _output;
    private readonly IReadOnlyList<string> _channels;

    public MetricCsvWriter(TextWriter output, MetricPlan plan)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(plan);
        _output = output;
        _channels = plan.Channels;
        _output.Write("tick");
        for (int i = 0; i < _channels.Count; i++)
        {
            _output.Write(',');
            _output.Write(_channels[i]);
        }
        _output.Write('\n');
    }

    public void WriteRow(long tick, ReadOnlySpan<double> values)
    {
        MetricFormatting.ValidateRow(tick, values, _channels.Count);
        MetricFormatting.WriteTick(_output, tick);
        foreach (double value in values)
        {
            _output.Write(',');
            if (!double.IsNaN(value)) MetricFormatting.WriteNumber(_output, value);
        }
        _output.Write('\n');
    }
}

/// <summary>Invariant JSONL in the same requested order. Undefined values are null. Does not own the writer.</summary>
public sealed class MetricJsonlWriter
{
    private readonly TextWriter _output;
    private readonly IReadOnlyList<string> _channels;

    public MetricJsonlWriter(TextWriter output, MetricPlan plan)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(plan);
        _output = output;
        _channels = plan.Channels;
    }

    public void WriteRow(long tick, ReadOnlySpan<double> values)
    {
        MetricFormatting.ValidateRow(tick, values, _channels.Count);
        _output.Write("{\"tick\":");
        MetricFormatting.WriteTick(_output, tick);
        for (int i = 0; i < _channels.Count; i++)
        {
            _output.Write(",\"");
            _output.Write(_channels[i]); // Registry permits only ASCII letters/digits, '_' and '.'.
            _output.Write("\":");
            if (double.IsNaN(values[i])) _output.Write("null");
            else MetricFormatting.WriteNumber(_output, values[i]);
        }
        _output.Write("}\n");
    }
}

internal static class MetricFormatting
{
    internal static void ValidateRow(long tick, ReadOnlySpan<double> values, int count)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        if (values.Length != count) throw new ArgumentException("Row length does not match the selected metric schema.", nameof(values));
        foreach (double value in values)
            if (double.IsInfinity(value)) throw new ArgumentException("Metric rows may contain finite numbers or missing NaN values, but not infinity.", nameof(values));
    }

    internal static void WriteTick(TextWriter output, long tick)
    {
        Span<char> buffer = stackalloc char[32];
        tick.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture);
        output.Write(buffer.Slice(0, written));
    }

    internal static void WriteNumber(TextWriter output, double value)
    {
        Span<char> buffer = stackalloc char[64];
        value.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture);
        output.Write(buffer.Slice(0, written));
    }
}
