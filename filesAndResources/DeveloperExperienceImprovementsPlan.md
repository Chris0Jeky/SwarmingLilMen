# Developer Experience Improvements - Implementation Plan

**Created**: 2025-11-12
**Status**: Planning
**Priority**: High (Complete before Phase 3)

## Problem Statement

The current SwarmingLilMen project lacks discoverability and ease of use:

- **No command-line interface**: Can't configure simulation without editing code
- **Hidden controls**: User must read source code to find keyboard shortcuts
- **No help system**: No `--help` flag or in-app help overlay
- **Scattered documentation**: Mix of up-to-date and outdated docs
- **No configuration examples**: Hard to understand parameter effects without experimentation

**Goal**: Make the project immediately usable and self-documenting for any developer.

---

## Implementation Phases

### Phase 1: Runtime Help & Discoverability (PRIORITY 1)

#### 1.1 In-App Help Overlay (H Key)
**Location**: `SwarmSim.Render/Program.cs`

**Design**:
```
┌─────────────────────────────────────────────────────────┐
│ SwarmingLilMen - Interactive Swarm Simulation          │
│ Press H to toggle this help                            │
├─────────────────────────────────────────────────────────┤
│ SPAWNING                                                │
│  Left Click    Spawn 50 agents at cursor (group 0)     │
│  Right Click   Spawn 50 agents at cursor (group 1)     │
│  SPACE         Spawn 100 random agents                  │
│                                                          │
│ VISUALIZATION (Toggles)                                 │
│  V             Velocity vectors                         │
│  S             Sense radius circles                     │
│  N             Neighbor connections                     │
│  F12           Debug overlay (snapshot info)            │
│                                                          │
│ PARAMETERS                                              │
│  1-7           Select parameter to adjust               │
│  ↑/↓ or +/-    Increase/decrease selected parameter    │
│  SHIFT + ↑/↓   Fine adjustment (smaller steps)          │
│                                                          │
│ PRESETS                                                 │
│  F1-F5         Load preset configurations               │
│  P             Print current configuration to console   │
│                                                          │
│ WORLD CONTROLS                                          │
│  R             Reset world to initial state             │
│  X             Shake - add random velocity to all       │
│  C             Export CSV snapshot of current state     │
│  ESC           Quit                                     │
└─────────────────────────────────────────────────────────┘
```

**Implementation**:
- Add `_showHelpOverlay` flag
- Toggle with H key
- Render semi-transparent dark panel with white text
- Use multi-column layout for readability
- Show current values of toggle states (V: ON, S: OFF, etc.)

#### 1.2 Startup Banner (Console)
**Location**: `SwarmSim.Render/Program.cs` (Main method, before window creation)

**Design**:
```
╔══════════════════════════════════════════════════════════════╗
║  SwarmingLilMen - High-Performance Swarm Simulation v1.0    ║
╠══════════════════════════════════════════════════════════════╣
║  Starting with 1000 agents across 4 groups                  ║
║  Fixed timestep: 1/60s | Interpolation: Enabled             ║
║                                                               ║
║  Press H for help | Press F12 for debug overlay              ║
╚══════════════════════════════════════════════════════════════╝
```

**Implementation**:
- Print to console before `Raylib.InitWindow()`
- Show current configuration summary
- Mention key help commands

---

### Phase 2: Command-Line Arguments (PRIORITY 2)

#### 2.1 Argument Parser
**Location**: New file `SwarmSim.Render/CommandLineArgs.cs`

**Design**:
```csharp
public sealed class CommandLineArgs
{
    public string? PresetName { get; set; }
    public string? ConfigFile { get; set; }
    public int? AgentCount { get; set; }
    public bool BenchmarkMode { get; set; }
    public bool ShowVersion { get; set; }
    public bool ListPresets { get; set; }
    public bool ShowHelp { get; set; }

    public static CommandLineArgs Parse(string[] args)
    {
        // Simple manual parsing (avoid external dependencies)
        var result = new CommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--help" or "-h":
                    result.ShowHelp = true;
                    break;
                case "--version" or "-v":
                    result.ShowVersion = true;
                    break;
                case "--preset" or "-p":
                    if (i + 1 < args.Length)
                        result.PresetName = args[++i];
                    break;
                case "--config" or "-c":
                    if (i + 1 < args.Length)
                        result.ConfigFile = args[++i];
                    break;
                case "--agent-count" or "-n":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int count))
                        result.AgentCount = count;
                    break;
                case "--benchmark" or "-b":
                    result.BenchmarkMode = true;
                    break;
                case "--list-presets" or "-l":
                    result.ListPresets = true;
                    break;
            }
        }
        return result;
    }
}
```

#### 2.2 Help Text
**Location**: `SwarmSim.Render/CommandLineArgs.cs`

**Design**:
```
Usage: SwarmSim.Render [OPTIONS]

Options:
  -h, --help              Show this help message and exit
  -v, --version           Show version information
  -p, --preset NAME       Load a preset configuration
                          (peaceful, warbands, evolution, dense, sparse)
  -c, --config FILE       Load configuration from JSON file
  -n, --agent-count N     Override initial agent count (default: 1000)
  -b, --benchmark         Run in headless benchmark mode (no window)
  -l, --list-presets      List all available presets

Examples:
  SwarmSim.Render                           # Default: 1000 agents, balanced config
  SwarmSim.Render --preset peaceful         # Run peaceful flocking preset
  SwarmSim.Render --config my_config.json   # Load custom configuration
  SwarmSim.Render -n 5000 -p warbands       # 5000 agents with warbands preset

Interactive Controls:
  Press H in the application for interactive help overlay
  See CONTROLS.md for complete reference
```

#### 2.3 Preset Loader
**Location**: `SwarmSim.Render/Program.cs`

**Implementation**:
```csharp
private static SimConfig LoadPresetByName(string name)
{
    return name.ToLowerInvariant() switch
    {
        "peaceful" => Presets[0].CreateConfig(),
        "warbands" => Presets[1].CreateConfig(),
        "evolution" => Presets[2].CreateConfig(),
        "dense" => Presets[3].CreateConfig(),
        "sparse" => Presets[4].CreateConfig(),
        _ => throw new ArgumentException($"Unknown preset: {name}")
    };
}
```

#### 2.4 JSON Config Loader
**Location**: `SwarmSim.Core/SimConfig.cs`

**Implementation**:
```csharp
public static SimConfig LoadFromJson(string filePath)
{
    string json = File.ReadAllText(filePath);
    var config = System.Text.Json.JsonSerializer.Deserialize<SimConfig>(json);
    if (config == null)
        throw new InvalidDataException($"Failed to deserialize config from {filePath}");

    config.Validate(); // Ensure loaded config is valid
    return config;
}

public void SaveToJson(string filePath)
{
    var options = new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true
    };
    string json = System.Text.Json.JsonSerializer.Serialize(this, options);
    File.WriteAllText(filePath, json);
}
```

---

### Phase 3: Documentation Consolidation (PRIORITY 3)

#### 3.1 Create CONTROLS.md
**Location**: `CONTROLS.md` (root directory)

**Content Outline**:
```markdown
# SwarmingLilMen - Controls Reference

## Quick Start
Press H in the application for interactive help

## Spawning Agents
...

## Visualization Toggles
...

## Parameter Adjustment
...

## Presets
...

## World Controls
...

## Configuration Files
...
```

#### 3.2 Update README.md
**Location**: `README.md` (root directory)

**Changes**:
- Update feature list with current capabilities
- Add "Quick Start" section with command-line examples
- Link to CONTROLS.md for detailed reference
- Update roadmap to reflect completed phases
- Add screenshots of F12 debug overlay

#### 3.3 Update QUICKSTART.md
**Location**: `QUICKSTART.md` (root directory)

**Changes**:
- Add command-line examples for each use case
- Show how to run different presets
- Demonstrate parameter tweaking
- Link to configuration JSON examples

#### 3.4 Archive Completed Plans
**Location**: `filesAndResources/archive/`

**Actions**:
- Move `DecouplingPlan.md` → `archive/DecouplingPlan_COMPLETED.md`
- Move `MakingBoidsBetter.md` → `archive/MakingBoidsBetter_COMPLETED.md`
- Add header noting completion date and relevant commits
- Update any links to these documents

#### 3.5 Update CLAUDE.md
**Location**: `CLAUDE.md`

**Additions**:
- Document snapshot architecture and versioning
- Document interpolation approach
- Document debugging practices (F12 overlay, console logging)
- Update build commands to include new CLI options

---

### Phase 4: Configuration Examples (PRIORITY 4)

#### 4.1 Create configs/ Directory
**Location**: `configs/` (root directory)

**Files to Create**:
- `peaceful_flocking.json` - Peaceful boids with high cohesion
- `warbands.json` - Multiple groups with aggression (when combat is implemented)
- `rapid_evolution.json` - High mutation rates
- `dense_swarm.json` - Large agent count, tight packing
- `sparse_exploration.json` - Wide world, low density
- `debug_minimal.json` - 10 agents for testing
- `performance_50k.json` - Performance testing configuration

**Example File Structure**:
```json
{
  "WorldWidth": 1920,
  "WorldHeight": 1080,
  "BoundaryMode": "Wrap",
  "FixedDeltaTime": 0.016666667,

  "_comment_agents": "Agent behavior parameters",
  "MaxSpeed": 10.0,
  "MaxForce": 5.0,
  "SpeedModel": "ConstantSpeed",

  "_comment_boids": "Boids flocking weights",
  "SeparationWeight": 5.0,
  "AlignmentWeight": 2.5,
  "CohesionWeight": 0.5,
  "SenseRadius": 100.0,
  "SeparationRadius": 30.0,
  "FieldOfView": 270.0,

  "_comment": "See CONFIGURATION.md for parameter descriptions"
}
```

#### 4.2 Create CONFIGURATION.md
**Location**: `CONFIGURATION.md` (root directory)

**Content Outline**:
```markdown
# Configuration Guide

## Parameter Reference

### World Parameters
- **WorldWidth** / **WorldHeight**: Simulation bounds in pixels
  - Range: 800-4096
  - Effect: Larger worlds allow more spread, smaller worlds increase density

### Agent Behavior
- **MaxSpeed**: Maximum velocity magnitude
  - Range: 1-50
  - Effect: Higher values = faster agents, more dynamic flocking
...

## Configuration Cookbook

### Recipe: Tight Schooling
For fish-like tight schooling behavior:
- High cohesion weight (2.0+)
- Moderate alignment (1.5-2.0)
- Strong separation (5.0+)
- Small sense radius (60-80)

### Recipe: Loose Flocking
For bird-like loose flocking:
...

## Troubleshooting

### Agents Don't Move
Check: MaxForce > 0, weights not all zero

### Agents Explode/Collide
Check: Separation weight sufficient, MaxSpeed reasonable
```

---

## Implementation Order

**Session 1: Immediate Wins**
1. ✅ Fix build errors (duplicate methods)
2. ✅ Convert performance tests to warnings
3. ✅ Add DX TODO to PROJECT_STATUS.md
4. ⏳ Create this plan document

**Session 2: In-App Help**
1. Add H key help overlay to Program.cs
2. Add startup banner to console
3. Update UI to show toggle states
4. Test help overlay rendering

**Session 3: Command-Line Interface**
1. Create CommandLineArgs.cs with parser
2. Add --help, --version, --list-presets handlers
3. Implement preset loader
4. Implement JSON config loader/saver
5. Update Main() to process args before window creation

**Session 4: Documentation**
1. Create CONTROLS.md
2. Update README.md with current features
3. Update QUICKSTART.md with CLI examples
4. Archive completed plan documents
5. Update CLAUDE.md with architecture notes

**Session 5: Configuration Examples**
1. Create configs/ directory
2. Generate all example JSON files
3. Create CONFIGURATION.md guide
4. Add cookbook recipes
5. Test loading each example config

---

## Testing Strategy

### Manual Testing Checklist
- [ ] H key shows/hides help overlay
- [ ] All help text is readable and accurate
- [ ] --help shows correct usage information
- [ ] --version displays version
- [ ] --list-presets shows all presets
- [ ] --preset <name> loads correct configuration
- [ ] --config <file> loads JSON correctly
- [ ] --agent-count <n> overrides agent count
- [ ] Invalid arguments show clear error messages
- [ ] All example configs load without errors

### Automated Tests
- [ ] Test CommandLineArgs.Parse() with various inputs
- [ ] Test SimConfig.LoadFromJson() with valid/invalid files
- [ ] Test preset name resolution
- [ ] Test configuration validation

---

## Success Criteria

A developer (or future Claude) should be able to:

✅ **Discover capabilities** - Run `--help` and see all options
✅ **Learn controls** - Press H in app to see all keyboard shortcuts
✅ **Run presets** - Use `--preset peaceful` without reading docs
✅ **Load custom configs** - Use `--config my_config.json` easily
✅ **Understand parameters** - Read CONFIGURATION.md to learn what each does
✅ **Find examples** - Browse configs/ directory for templates

**No source code reading required for basic usage!**

---

## Non-Goals (Deferred)

- GUI configuration editor (future nice-to-have)
- Hot-reloading configurations (future enhancement)
- Built-in tutorial mode (out of scope)
- Web-based configuration tool (not needed)

---

## Notes for Implementation

- Keep command-line parsing simple (no external dependencies)
- Use JSON serialization built into .NET (System.Text.Json)
- Ensure all help text fits on 1920x1080 screen
- Make help overlay semi-transparent to see simulation underneath
- Use consistent terminology across all docs (agent vs entity, preset vs configuration)
- Test with real users to validate discoverability

---

**End of Plan**
