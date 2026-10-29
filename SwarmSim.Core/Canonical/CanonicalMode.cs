using System.Text.Json.Serialization;

namespace SwarmSim.Core.Canonical;

/// <summary>Public, replayable built-in compositions. Missing mode means Enhanced permanently.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CanonicalMode>))]
public enum CanonicalMode
{
    Enhanced = 0,
    ClassicReynolds = 1
}
