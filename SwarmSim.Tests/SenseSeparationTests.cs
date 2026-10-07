using SwarmSim.Core;

namespace SwarmSim.Tests;

/// <summary>
/// Regression coverage for the legacy SenseSystem separation semantics documented
/// in source (SenseSystem.cs "SEPARATION WEIGHTING" and BehaviorSystem.cs):
/// linear (not inverse) radial falloff, blend-weight renormalization, near-zero skip.
/// All tests drive the public path via World.Tick with a seeded Rng and fixed timestep.
/// </summary>
public class SenseSeparationTests
{
    private static SimConfig SeparationOnlyConfig(
        float separationWeight = 1.0f,
        float separationRadius = 20f)
    {
        return new SimConfig
        {
            InitialCapacity = 10,
            SenseRadius = 50f,
            SeparationRadius = separationRadius,
            FieldOfView = 360f, // Omnidirectional: only distance guards filter neighbors
            SeparationWeight = separationWeight,
            AlignmentWeight = 0f, // Disabled for isolation
            CohesionWeight = 0f,  // Disabled for isolation
            CollisionAvoidanceRadius = 0f, // Disabled: effective floor is 0.01, far below test distances
            MaxSpeed = 100f,
            MaxForce = 1000f, // Large enough that separation steering is never clamped
            Friction = 1.0f, // No friction to see pure steering
            FixedDeltaTime = 1f / 120f // Pinned fixed timestep
        };
    }

    [Fact]
    public void Separation_LinearFalloff_NotInverse()
    {
        // Two neighbors at d and 2d inside separationRadius must contribute in the
        // ratio (r - d) / (r - 2d): bounded linear falloff, no inverse-distance term.
        const float separationRadius = 20f;
        const float d = 5f;
        var config = SeparationOnlyConfig(separationWeight: 1.0f, separationRadius: separationRadius);
        var world = new World(config, seed: 42);

        int focal = world.AddAgent(x: 100f, y: 100f, group: 0);
        world.AddAgent(x: 100f + d, y: 100f, group: 0); // Distance d to the east
        world.AddAgent(x: 100f, y: 100f + 2f * d, group: 0); // Distance 2d to the north

        world.Tick();

        float vx = world.Vx[focal];
        float vy = world.Vy[focal];

        // Pushed west (away from eastern neighbor) and south (away from northern neighbor).
        Assert.True(vx < 0f, $"Focal should move west, but vx={vx}");
        Assert.True(vy < 0f, $"Focal should move south, but vy={vy}");

        // Steering preserves the separation-vector direction (clamping only rescales),
        // so vy/vx must equal the falloff ratio (r - 2d) / (r - d) = 10/15 = 2/3.
        // Pure inverse (1/d) would give 1/2 and inverse-square 1/4, both well outside tolerance.
        float expectedRatio = (separationRadius - 2f * d) / (separationRadius - d);
        float ratio = vy / vx;
        Assert.InRange(ratio, expectedRatio * 0.95f, expectedRatio * 1.05f);
    }

    [Fact]
    public void Separation_BlendWeight_Renormalized_DoublingChangesNothing()
    {
        // Doubling every neighbor falloff contribution scales the accumulated separation
        // vector by 2, but BehaviorSystem renormalizes it before steering, so the
        // post-BehaviorSystem steering direction and magnitude must be unchanged.
        // Duplicating each neighbor position doubles each contribution exactly.
        var config = SeparationOnlyConfig(separationWeight: 1.0f);

        var single = new World(config, seed: 42);
        int singleFocal = single.AddAgent(x: 100f, y: 100f, group: 0);
        single.AddAgent(x: 105f, y: 100f, group: 0);
        single.AddAgent(x: 100f, y: 110f, group: 0);
        single.Tick();

        var doubled = new World(config, seed: 42);
        int doubledFocal = doubled.AddAgent(x: 100f, y: 100f, group: 0);
        doubled.AddAgent(x: 105f, y: 100f, group: 0);
        doubled.AddAgent(x: 105f, y: 100f, group: 0);
        doubled.AddAgent(x: 100f, y: 110f, group: 0);
        doubled.AddAgent(x: 100f, y: 110f, group: 0);
        doubled.Tick();

        // Non-vacuity: the focal agent must actually steer in both worlds.
        Assert.NotEqual(0f, single.Fx[singleFocal]);
        Assert.NotEqual(0f, single.Fy[singleFocal]);

        // Steering forces (post-BehaviorSystem) and integrated velocities match.
        const float tolerance = 1e-4f;
        Assert.InRange(doubled.Fx[doubledFocal], single.Fx[singleFocal] - tolerance, single.Fx[singleFocal] + tolerance);
        Assert.InRange(doubled.Fy[doubledFocal], single.Fy[singleFocal] - tolerance, single.Fy[singleFocal] + tolerance);
        Assert.InRange(doubled.Vx[doubledFocal], single.Vx[singleFocal] - tolerance, single.Vx[singleFocal] + tolerance);
        Assert.InRange(doubled.Vy[doubledFocal], single.Vy[singleFocal] - tolerance, single.Vy[singleFocal] + tolerance);
    }

    [Fact]
    public void Separation_NearZeroSkip_ContributesNothing()
    {
        // A neighbor nearer than 0.01 units (distSq < 0.0001f guard) is skipped entirely,
        // so the focal aggregate must equal the single-agent case: exactly no steering.
        // The neighbor is well inside SenseRadius with 360-degree FOV, so the distance
        // guard is the only possible filter.
        var config = SeparationOnlyConfig(separationWeight: 2.0f);
        const float closeOffset = 0.005f; // Inside the 0.01-unit skip radius

        var solo = new World(config, seed: 42);
        int soloIdx = solo.AddAgent(x: 100f, y: 100f, group: 0);
        solo.Tick();

        var close = new World(config, seed: 42);
        int focalIdx = close.AddAgent(x: 100f, y: 100f, group: 0);
        int nearIdx = close.AddAgent(x: 100f + closeOffset, y: 100f, group: 0);
        close.Tick();

        // Focal state matches the single-agent case exactly.
        Assert.Equal(solo.Vx[soloIdx], close.Vx[focalIdx]);
        Assert.Equal(solo.Vy[soloIdx], close.Vy[focalIdx]);
        Assert.Equal(solo.Fx[soloIdx], close.Fx[focalIdx]);
        Assert.Equal(solo.Fy[soloIdx], close.Fy[focalIdx]);
        Assert.Equal(solo.X[soloIdx], close.X[focalIdx]);
        Assert.Equal(solo.Y[soloIdx], close.Y[focalIdx]);

        // Exactly nothing: no steering force and no motion for either agent.
        Assert.Equal(0f, close.Fx[focalIdx]);
        Assert.Equal(0f, close.Fy[focalIdx]);
        Assert.Equal(0f, close.Vx[focalIdx]);
        Assert.Equal(0f, close.Vy[focalIdx]);
        Assert.Equal(0f, close.Vx[nearIdx]);
        Assert.Equal(0f, close.Vy[nearIdx]);
    }
}
