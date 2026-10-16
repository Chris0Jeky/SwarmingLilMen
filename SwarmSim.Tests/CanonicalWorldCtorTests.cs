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

    [Fact]
    public void Ctor_MismatchedIndexWidth_Throws()
    {
        var settings = new CanonicalWorldSettings();
        ISpatialIndex grid = new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth + 1f, settings.WorldHeight);
        ISpatialIndex naive = new NaiveSpatialIndex(settings.WorldWidth + 1f, settings.WorldHeight);

        var gridEx = Assert.Throws<ArgumentException>(() => new CanonicalWorld(settings, grid));
        var naiveEx = Assert.Throws<ArgumentException>(() => new CanonicalWorld(settings, naive));

        Assert.Equal("spatialIndex", gridEx.ParamName);
        Assert.Equal("spatialIndex", naiveEx.ParamName);
    }

    [Fact]
    public void Ctor_MismatchedIndexHeight_Throws()
    {
        var settings = new CanonicalWorldSettings();
        ISpatialIndex grid = new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight + 1f);
        ISpatialIndex naive = new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight + 1f);

        var gridEx = Assert.Throws<ArgumentException>(() => new CanonicalWorld(settings, grid));
        var naiveEx = Assert.Throws<ArgumentException>(() => new CanonicalWorld(settings, naive));

        Assert.Equal("spatialIndex", gridEx.ParamName);
        Assert.Equal("spatialIndex", naiveEx.ParamName);
    }

    [Fact]
    public void Ctor_MatchingIndexExtents_Constructs()
    {
        var settings = new CanonicalWorldSettings { WorldWidth = 320f, WorldHeight = 240f };

        var gridWorld = new CanonicalWorld(
            settings,
            new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        var naiveWorld = new CanonicalWorld(
            settings,
            new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight));

        Assert.Equal(0, gridWorld.Count);
        Assert.Equal(0, naiveWorld.Count);
        Assert.Equal(settings.WorldWidth, gridWorld.Settings.WorldWidth);
        Assert.Equal(settings.WorldHeight, naiveWorld.Settings.WorldHeight);
    }
}
