using System;
using SwarmSim.Core.Canonical;
using Xunit;

namespace SwarmSim.Tests;

public sealed class CanonicalWorldCtorTests
{
    [Fact]
    public void Ctor_NullSettings_Throws()
    {
        var probe = new CanonicalWorldSettings();
        var validIndex = new GridSpatialIndex(probe.SenseRadius, probe.WorldWidth, probe.WorldHeight);

        Assert.Throws<ArgumentNullException>(() => new CanonicalWorld(null!, validIndex));
    }

    [Fact]
    public void Ctor_NullIndex_Throws()
    {
        var settings = new CanonicalWorldSettings();

        Assert.Throws<ArgumentNullException>(() => new CanonicalWorld(settings, null!));
    }
}
