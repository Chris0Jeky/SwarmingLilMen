using SwarmSim.Core.Canonical;
using SwarmSim.Core.Spatial;

namespace SwarmSim.Benchmarks;

internal static class CanonicalSelectionExperiment
{
    internal readonly record struct Result(long SortedWrites, long HeapWrites, long GridQueryBytes, long NaiveQueryBytes);

    internal static Result MeasureDenseSelection(int count, int cap)
    {
        var boids = new Boid[count];
        Array.Fill(boids, new Boid(new Vec2(10, 10), new Vec2(1, 0)));
        var grid = new GridSpatialIndex(10, 100, 100);
        var naive = new NaiveSpatialIndex(100, 100);
        grid.Initialize(count); naive.Initialize(count);
        grid.Rebuild(boids); naive.Rebuild(boids);
        var sorted = new int[cap];
        var heap = new int[cap];
        var expected = new int[cap];
        var actual = new int[cap];
        long sortedWrites = 0, heapWrites = 0;
        for (int self = 0; self < count; self++)
        {
            int sortedCount = 0, heapCount = 0;
            bool sortedTruncated = false, heapTruncated = false;
            // All positions are in one cell. Rebuild prepends increasing indices, so this is
            // exactly the production query's hit order, including self exclusion.
            for (int candidate = count - 1; candidate >= 0; candidate--)
            {
                if (candidate == self) continue;
                sortedWrites += InsertSortedReference(sorted, ref sortedCount, candidate, ref sortedTruncated);
                heapWrites += UniformGrid.InsertLowestBounded(heap, ref heapCount, candidate, ref heapTruncated);
            }
            heapWrites += UniformGrid.SortNeighborHeap(heap.AsSpan(0, heapCount));
            var oracle = naive.QueryNeighbors(boids, self, 0, expected);
            var query = grid.QueryNeighbors(boids, self, 0, actual);
            CanonicalExperiment.Require(sortedCount == oracle.Count && heapCount == oracle.Count && query.Count == oracle.Count
                && sortedTruncated == oracle.IsTruncated && heapTruncated == oracle.IsTruncated && query.IsTruncated == oracle.IsTruncated
                && sorted.AsSpan(0, sortedCount).SequenceEqual(expected.AsSpan(0, oracle.Count))
                && heap.AsSpan(0, heapCount).SequenceEqual(expected.AsSpan(0, oracle.Count))
                && actual.AsSpan(0, query.Count).SequenceEqual(expected.AsSpan(0, oracle.Count)), "Selection replay differs from actual queries");
        }
        // Both actual query paths were warmed above; replay buffers and construction are excluded.
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int self = 0; self < count; self++) grid.QueryNeighbors(boids, self, 0, actual);
        long gridBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int self = 0; self < count; self++) naive.QueryNeighbors(boids, self, 0, expected);
        long naiveBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return new(sortedWrites, heapWrites, gridBytes, naiveBytes);
    }

    // Frozen pre-experiment selection from 2d7e962, with an array-write counter.
    // Reference only: never an alternative simulation path.
    private static int InsertSortedReference(Span<int> buffer, ref int written, int candidate, ref bool truncated)
    {
        int writes = 0;
        if (written < buffer.Length)
        {
            int insertAt = written;
            while (insertAt > 0 && buffer[insertAt - 1] > candidate)
            {
                buffer[insertAt] = buffer[insertAt - 1];
                writes++;
                insertAt--;
            }
            buffer[insertAt] = candidate;
            written++;
            return writes + 1;
        }
        truncated = true;
        if (buffer.IsEmpty || candidate >= buffer[written - 1]) return 0;
        int replaceAt = written - 1;
        while (replaceAt > 0 && buffer[replaceAt - 1] > candidate)
        {
            buffer[replaceAt] = buffer[replaceAt - 1];
            writes++;
            replaceAt--;
        }
        buffer[replaceAt] = candidate;
        return writes + 1;
    }
}
