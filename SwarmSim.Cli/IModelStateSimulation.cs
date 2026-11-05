using SwarmSim.Core.Diagnostics;

namespace SwarmSim.Cli;

/// <summary>Optional engine capability; unsupported engines must not claim a model-state hash.</summary>
internal interface IModelStateSimulation
{
    ModelStateHash StateHash();
}
