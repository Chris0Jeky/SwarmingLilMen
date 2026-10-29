namespace SwarmSim.Core.Metrics;

/// <summary>Explicit channel registration. Names are case-sensitive; no engine-specific names live here.</summary>
public sealed class MetricRegistry
{
    private readonly Dictionary<string, Registration> _channels = new(StringComparer.Ordinal);
    private readonly HashSet<IMetricProbe> _probes = new(ReferenceEqualityComparer.Instance);
    private readonly record struct Registration(IMetricProbe Probe, int ChannelIndex, int OutputCount);

    public void Register(IMetricProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        if (_probes.Contains(probe)) throw new ArgumentException("This probe is already registered.", nameof(probe));
        ArgumentNullException.ThrowIfNull(probe.Channels);
        string[] names = probe.Channels.ToArray();
        if (names.Length == 0) throw new ArgumentException("A probe must declare at least one channel.", nameof(probe));
        var local = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (string.IsNullOrEmpty(name) || name == "tick" ||
                name.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_' && character != '.'))
                throw new ArgumentException($"Invalid metric channel name '{name}'.", nameof(probe));
            if (!local.Add(name) || _channels.ContainsKey(name))
                throw new ArgumentException($"Metric channel '{name}' is already registered.", nameof(probe));
        }
        // Publish only after the entire registration has passed validation.
        for (int index = 0; index < names.Length; index++)
            _channels.Add(names[index], new(probe, index, names.Length));
        _probes.Add(probe);
    }

    /// <summary>Compiles requested columns in their supplied order and allocates scratch once.</summary>
    public MetricPlan CreatePlan(params string[] requestedChannels)
    {
        ArgumentNullException.ThrowIfNull(requestedChannels);
        string[] names = (string[])requestedChannels.Clone();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var used = new Dictionary<IMetricProbe, int>(ReferenceEqualityComparer.Instance);
        var probes = new List<IMetricProbe>();
        var outputs = new List<double[]>();
        var routes = new (int Probe, int Channel)[names.Length];
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index];
            if (name is null || !seen.Add(name))
                throw new ArgumentException("Requested metric names must be non-null and unique.", nameof(requestedChannels));
            if (!_channels.TryGetValue(name, out Registration registration))
                throw new ArgumentException($"Unknown metric channel '{name}'.", nameof(requestedChannels));
            if (!used.TryGetValue(registration.Probe, out int probeIndex))
            {
                probeIndex = probes.Count;
                used.Add(registration.Probe, probeIndex);
                probes.Add(registration.Probe);
                outputs.Add(new double[registration.OutputCount]);
            }
            routes[index] = (probeIndex, registration.ChannelIndex);
        }
        return new(names, probes.ToArray(), outputs.ToArray(), routes);
    }
}

/// <summary>A reusable requested schema. Each needed probe is sampled once, regardless of selected columns.</summary>
public sealed class MetricPlan
{
    private readonly IMetricProbe[] _probes;
    private readonly double[][] _outputs;
    private readonly (int Probe, int Channel)[] _routes;

    internal MetricPlan(string[] names, IMetricProbe[] probes, double[][] outputs, (int Probe, int Channel)[] routes)
    {
        Channels = Array.AsReadOnly(names);
        _probes = probes;
        _outputs = outputs;
        _routes = routes;
        IsDeterministic = probes.All(probe => probe.IsDeterministic);
    }

    public IReadOnlyList<string> Channels { get; }
    public int ChannelCount => _routes.Length;
    public bool IsDeterministic { get; }

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        MetricMath.RequireOutput(output, ChannelCount);
        for (int index = 0; index < _probes.Length; index++)
        {
            _outputs[index].AsSpan().Fill(double.NaN);
            _probes[index].Sample(frame, _outputs[index]);
        }
        for (int index = 0; index < _routes.Length; index++)
        {
            (int probe, int channel) = _routes[index];
            double value = _outputs[probe][channel];
            if (double.IsInfinity(value))
                throw new InvalidOperationException($"Metric '{Channels[index]}' produced infinity.");
            output[index] = value;
        }
    }
}
