using System.Numerics;
using Raylib_cs;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Render;

[Flags]
internal enum ContributionLinks { None = 0, Separation = 1, Alignment = 2, Cohesion = 4 }

/// <summary>Inspection of the built-in boids rules, using one completed tick's inputs and forces.</summary>
internal static class CanonicalInspectionOverlay
{
    private static readonly Color SeparationColor = new(255, 159, 67, 255);
    private static readonly Color AlignmentColor = new(89, 200, 255, 255);
    private static readonly Color CohesionColor = new(223, 164, 255, 255);
    private static readonly (RendererControlId Control, string Policy, Color Color)[] Rules =
    [
        (RendererControlId.ToggleSeparation, CanonicalPolicyIds.Separation, SeparationColor),
        (RendererControlId.ToggleAlignment, CanonicalPolicyIds.Alignment, AlignmentColor),
        (RendererControlId.ToggleCohesion, CanonicalPolicyIds.Cohesion, CohesionColor)
    ];

    internal static void ApplyRuleSwitches(RendererInputSnapshot input, CanonicalWorld world)
    {
        if (input.Mode != RendererMode.Canonical) return;
        foreach (var rule in Rules)
            if (input.IsActive(rule.Control))
                world.SetPolicyEnabled(rule.Policy, !world.IsPolicyEnabled(rule.Policy));
    }

    internal static void CopyRuleSwitches(CanonicalWorld source, CanonicalWorld destination)
    {
        foreach (var rule in Rules)
            destination.SetPolicyEnabled(rule.Policy, source.IsPolicyEnabled(rule.Policy));
    }

    internal static void DrawRuleSwitches(CanonicalWorld world, int x, int y)
    {
        foreach (var rule in Rules)
        {
            bool enabled = world.IsPolicyEnabled(rule.Policy);
            string label = Program.Controls.ShortLabel(RendererMode.Canonical, rule.Control);
            Raylib.DrawText($"{label} [{(enabled ? "ON" : "OFF")}]", x, y, 18, enabled ? rule.Color : Color.Gray);
            x += 270;
        }
    }

    internal static Vector2 FovPoint(Boid subject, float radius, float degrees, float fraction)
    {
        float angle = MathF.Atan2(subject.Forward.Y, subject.Forward.X)
            + (fraction - 0.5f) * degrees * MathF.PI / 180f;
        return new(subject.Position.X + radius * MathF.Cos(angle), subject.Position.Y + radius * MathF.Sin(angle));
    }

    // Links identify weighted inputs to a nonzero applied rule, not additive per-neighbor forces.
    internal static ContributionLinks ClassifyLinks(Vec2 reverseDelta, float weight, float separationRadius,
        RuleInstrumentation.Metrics applied)
    {
        if (weight <= 0f) return ContributionLinks.None;
        ContributionLinks result = ContributionLinks.None;
        float distance = reverseDelta.Length;
        if (applied.SeparationMagnitude > 0f && distance > 1e-4f && distance < separationRadius)
            result |= ContributionLinks.Separation;
        if (applied.AlignmentMagnitude > 0f) result |= ContributionLinks.Alignment;
        if (applied.CohesionMagnitude > 0f) result |= ContributionLinks.Cohesion;
        return result;
    }

    internal static Vector2 SteeringOffset(Vec2 force, float maxForce)
    {
        // Scale direction and magnitude together; the displayed cap is visual only.
        var vector = new Vector2(force.X, force.Y) / maxForce * 64f;
        return vector.LengthSquared() > 128f * 128f ? Vector2.Normalize(vector) * 128f : vector;
    }

    /// <summary>Displayed link query for the selected boid, with its status line.</summary>
    internal readonly record struct InspectionQuery(SpatialQueryResult Result, string Status);

    // Recorded tick facts (what the simulation used) and displayed facts (what fits the overlay's
    // fixed buffers) are reported separately: a display cap below the simulation's cap can omit links.
    internal static string FormatStatus(ulong tick, bool hasCompletedTick, bool subjectRecorded, int recordedInputs,
        bool recordedTruncated, int displayedLinks, int displayCap, bool displayCapped)
    {
        if (!hasCompletedTick) return "Inspection: waiting for first completed tick";
        if (!subjectRecorded) return $"Tick {tick}: subject not recorded";
        return $"Tick {tick} recorded: {recordedInputs} inputs, {(recordedTruncated ? "simulation query truncated" : "no truncation")}"
            + $" | Displayed: {displayedLinks} links, cap {displayCap}{(displayCapped ? " reached, links omitted" : "")}"
            + " | Gray links: no applied rule";
    }

    internal static InspectionQuery Inspect(CanonicalWorld world, int subjectIndex, int[] neighbors, float[] weights)
    {
        ulong tick = world.TickCount;
        if (world.LastTickInputs.IsEmpty) return new(default, FormatStatus(tick, false, false, 0, false, 0, 0, false));
        if ((uint)subjectIndex >= (uint)world.LastTickInputs.Length) return new(default, FormatStatus(tick, true, false, 0, false, 0, 0, false));
        int capacity = Program.ComputeOverlayQueryCapacity(Math.Min(neighbors.Length, weights.Length), world.EffectiveMaxNeighbors);
        SpatialQueryResult query = world.QueryLastTickVisibleNeighbors(subjectIndex, neighbors.AsSpan(0, capacity), weights.AsSpan(0, capacity));
        return new(query, FormatStatus(tick, true, true, world.Instrumentation.NeighborCounts[subjectIndex],
            world.Instrumentation.QueryTruncated[subjectIndex], query.Count, capacity, query.IsTruncated));
    }

    /// <summary>Keeps the inspected boid across recreation (order is preserved), clamping if it no longer exists.</summary>
    internal static int KeepSubject(int subjectIndex, int boidCount) => Math.Clamp(subjectIndex, 0, Math.Max(0, boidCount - 1));

    internal static void Draw(CanonicalWorld world, int subjectIndex, int[] neighbors, float[] weights)
    {
        InspectionQuery inspection = Inspect(world, subjectIndex, neighbors, weights);
        if ((uint)subjectIndex >= (uint)world.LastTickInputs.Length)
        {
            Raylib.DrawText(inspection.Status, 10, Raylib.GetScreenHeight() - 100, 18, Color.Gold);
            return;
        }

        CanonicalWorldSettings settings = world.Settings;
        Boid subject = world.LastTickInputs[subjectIndex];
        var center = new Vector2(subject.Position.X, subject.Position.Y);
        var forward = new Vector2(subject.Forward.X, subject.Forward.Y);
        world.TryGetMetrics(subjectIndex, out var applied);
        SpatialQueryResult query = inspection.Result;

        for (int n = 0; n < query.Count; n++)
        {
            Boid neighbor = world.LastTickInputs[neighbors[n]];
            Vec2 delta = Vec2.MinimumImageDelta(subject.Position, neighbor.Position, settings.WorldWidth, settings.WorldHeight);
            Vec2 reverse = Vec2.MinimumImageDelta(neighbor.Position, subject.Position, settings.WorldWidth, settings.WorldHeight);
            ContributionLinks links = ClassifyLinks(reverse, weights[n], settings.SeparationRadius, applied);
            var position = new Vector2(neighbor.Position.X, neighbor.Position.Y);
            if (links == ContributionLinks.None) DrawLink(center, position, delta, 0f, Color.DarkGray);
            if ((links & ContributionLinks.Separation) != 0) DrawLink(center, position, delta, -3f, SeparationColor);
            if ((links & ContributionLinks.Alignment) != 0) DrawLink(center, position, delta, 0f, AlignmentColor);
            if ((links & ContributionLinks.Cohesion) != 0) DrawLink(center, position, delta, 3f, CohesionColor);
            Raylib.DrawCircleLines((int)position.X, (int)position.Y, 5f, Color.Gray);
        }

        const int segments = 64;
        Vector2 point = FovPoint(subject, settings.SenseRadius, settings.FieldOfView, 0f);
        if (settings.FieldOfView < 360f) Raylib.DrawLineEx(center, point, 1f, Color.Gold);
        for (int i = 1; i <= segments; i++)
        {
            Vector2 next = FovPoint(subject, settings.SenseRadius, settings.FieldOfView, (float)i / segments);
            Raylib.DrawLineEx(point, next, 2f, Color.Gold);
            point = next;
        }
        if (settings.FieldOfView < 360f) Raylib.DrawLineEx(center, point, 1f, Color.Gold);

        float lookAhead = settings.TargetSpeed * MathF.Max(0.05f, settings.WhiskerTimeHorizon);
        Vector2 whiskerEnd = center + forward * lookAhead;
        Raylib.DrawLineEx(center, whiskerEnd, 1f, Color.Gray);
        Raylib.DrawCircleLines((int)whiskerEnd.X, (int)whiskerEnd.Y, MathF.Max(0.1f, settings.SeparationRadius), Color.Gray);
        DrawArrow(center, center + forward * 28f, Color.LightGray, 1f);
        Vec2 force = world.Instrumentation.SteeringVectors[subjectIndex];
        // Marker first: the arrow must stay visible over it for small forces.
        Raylib.DrawCircleV(center, 6f, Color.Gold);
        DrawArrow(center, center + SteeringOffset(force, settings.MaxForce), Color.White, 3f);
        Raylib.DrawText($"#{subjectIndex}", (int)center.X + 10, (int)center.Y + 8, 18, Color.Gold);

        int panelX = Raylib.GetScreenWidth() - 610;
        Raylib.DrawRectangle(panelX, 48, 590, 250, new Color(16, 21, 30, 240));
        Raylib.DrawText($"BOID #{subjectIndex} / TICK {world.TickCount}", panelX + 18, 64, 22, Color.White);
        Raylib.DrawText("Links: inputs to applied rules (before movement)", panelX + 18, 98, 18, Color.LightGray);
        Raylib.DrawText($"Separation  {applied.SeparationMagnitude:F3}", panelX + 18, 126, 18, SeparationColor);
        Raylib.DrawText($"Alignment   {applied.AlignmentMagnitude:F3}", panelX + 18, 150, 18, AlignmentColor);
        Raylib.DrawText($"Cohesion    {applied.CohesionMagnitude:F3}", panelX + 18, 174, 18, CohesionColor);
        Raylib.DrawText($"White arrow: resolved force {force.Length:F3}", panelX + 18, 204, 18, Color.White);
        Raylib.DrawText("64px / MaxForce; capped at 128px", panelX + 18, 228, 16, Color.LightGray);
        Raylib.DrawText("Gold: FOV arc | Gray: heading / whisker", panelX + 18, 258, 18, Color.Gold);
        Raylib.DrawText(inspection.Status, 10, Raylib.GetScreenHeight() - 100, 18, Color.LightGray);
    }

    private static void DrawArrow(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 offset = end - start;
        if (offset.LengthSquared() < 0.01f) return;
        Vector2 direction = Vector2.Normalize(offset);
        Vector2 perpendicular = new(-direction.Y, direction.X);
        float head = MathF.Min(8f, offset.Length() * 0.4f);
        Raylib.DrawLineEx(start, end, width, color);
        Raylib.DrawLineEx(end, end - direction * head + perpendicular * head * 0.5f, width, color);
        Raylib.DrawLineEx(end, end - direction * head - perpendicular * head * 0.5f, width, color);
    }

    private static void DrawLink(Vector2 subject, Vector2 neighbor, Vec2 minimumImageDelta, float lane, Color color)
    {
        var delta = new Vector2(minimumImageDelta.X, minimumImageDelta.Y);
        Vector2 offset = delta.LengthSquared() > 0f ? Vector2.Normalize(new(-delta.Y, delta.X)) * lane : Vector2.Zero;
        Vector2 wrappedNeighbor = subject + delta;
        Raylib.DrawLineEx(subject + offset, wrappedNeighbor + offset, 1.5f, color);
        if (Vector2.DistanceSquared(wrappedNeighbor, neighbor) > 0.01f)
            Raylib.DrawLineEx(neighbor + offset, neighbor - delta + offset, 1.5f, color);
    }
}
