using System.Reflection;
using System.Runtime.InteropServices;

namespace SwarmSim.Cli;

internal static class RunProvenance
{
    internal static RuntimeIdentity Runtime() => new()
    {
        Framework = RuntimeInformation.FrameworkDescription,
        OperatingSystem = RuntimeInformation.OSDescription,
        Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        CliVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"
    };

    internal static (string? Commit, bool? Dirty) Git()
    {
        // Runtime CWD and its Git status cannot establish the identity or cleanliness of this
        // binary. Only an embedded build revision is evidence; build dirtiness stays unknown.
        return (ReadEmbeddedCommit(typeof(Program).Assembly), null);
    }

    internal static string? ReadEmbeddedCommit(Assembly assembly)
    {
        foreach (AssemblyMetadataAttribute metadata in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (metadata.Key == "SourceRevisionId" && IsCommit(metadata.Value))
                return metadata.Value;
        }

        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        int metadataStart = version?.IndexOf('+') ?? -1;
        if (metadataStart < 0)
            return null;

        // The SDK appends +<revision>, or .<revision> after existing SemVer build metadata.
        string information = version!;
        string candidate = information[(Math.Max(metadataStart, information.LastIndexOf('.')) + 1)..];
        return IsCommit(candidate) ? candidate : null;
    }

    private static bool IsCommit(string? value) => value is { Length: 40 or 64 } && value.All(Uri.IsHexDigit);
}
