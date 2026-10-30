namespace SwarmSim.Tests;

public sealed class AllocationMeasurementTests
{
    /// <summary>
    /// A background GC's initial suspend clears every thread's allocation context, so
    /// GC.GetAllocatedBytesForCurrentThread jumps by the unused quantum (up to about 8 KB) on a
    /// thread that allocated nothing, while parallel test classes supply the pressure. Exact
    /// zero-allocation assertions are only meaningful with concurrent GC off. See issue #191.
    /// </summary>
    [Fact]
    public void ZeroAllocationAssertionsRunWithoutBackgroundCollection() =>
        Assert.Equal(false, GC.GetConfigurationVariables()["Concurrent"]);
}
