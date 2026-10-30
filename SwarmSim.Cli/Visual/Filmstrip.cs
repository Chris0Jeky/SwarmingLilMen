using SwarmSim.Core.Metrics;

namespace SwarmSim.Cli.Visual;

/// <summary>Copies only the frames a filmstrip needs (preselected ticks); null in the runner when --visual is off.</summary>
internal sealed class FrameRecorder
{
    private readonly KinematicSample[][] _frames;
    private int _next;

    internal FrameRecorder(int ticks, float worldWidth, float worldHeight)
    {
        Ticks = Filmstrip.SelectTicks(ticks);
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        _frames = new KinematicSample[Ticks.Length][];
    }

    internal int[] Ticks { get; }
    internal float WorldWidth { get; }
    internal float WorldHeight { get; }
    internal KinematicSample[][] Frames => _frames;

    /// <summary>Ticks arrive in increasing order; a tick that was not preselected costs one comparison.</summary>
    internal void Capture(int tick, ReadOnlySpan<KinematicSample> agents)
    {
        if (_next < Ticks.Length && Ticks[_next] == tick)
            _frames[_next++] = agents.ToArray();
    }
}

internal readonly record struct Raster(int Width, int Height, byte[] Rgba);

/// <summary>
/// Dependency-free software renderer: 8 panels (4x2) of agents as short heading lines coloured by
/// heading angle on a hue wheel. It sees only kinematic samples, so it works for any engine.
/// </summary>
internal static class Filmstrip
{
    internal const int PanelWidth = 480;
    internal const int Gap = 8;
    internal const int Columns = 4;
    internal const int Rows = 2;
    internal const int PanelCount = Columns * Rows;
    private const int HueSteps = 360;
    private const float StationarySpeedSquared = 1e-12f;

    internal static readonly (byte R, byte G, byte B) Background = (10, 12, 16);
    internal static readonly (byte R, byte G, byte B) PanelBackground = (20, 24, 31);
    internal static readonly (byte R, byte G, byte B) Border = (68, 77, 90);
    internal static readonly (byte R, byte G, byte B) Text = (214, 220, 228);
    internal static readonly (byte R, byte G, byte B) Stationary = (150, 150, 150);

    // 3x5 glyphs, row-major, '1' = lit.
    private static readonly string[] Digits =
    [
        "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
        "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
    ];

    /// <summary>Evenly spaced ticks including 0 and the final tick; duplicates removed when ticks &lt; 7.</summary>
    internal static int[] SelectTicks(int ticks)
    {
        var chosen = new List<int>(PanelCount);
        for (int k = 0; k < PanelCount; k++)
        {
            int tick = (int)((long)ticks * k / (PanelCount - 1));
            if (chosen.Count == 0 || chosen[^1] != tick) chosen.Add(tick);
        }
        return chosen.ToArray();
    }

    internal static int PanelHeight(float worldWidth, float worldHeight)
    {
        double ratio = IsUsable(worldWidth) && IsUsable(worldHeight) ? (double)worldHeight / worldWidth : 1d;
        return Math.Clamp((int)Math.Round(PanelWidth * ratio), 48, 1440);
    }

    internal static Raster Render(IReadOnlyList<int> ticks, IReadOnlyList<KinematicSample[]> frames, float worldWidth, float worldHeight)
    {
        if (ticks.Count != frames.Count || ticks.Count is 0 or > PanelCount)
            throw new ArgumentException("A filmstrip needs between 1 and 8 frames, one per tick.");
        int panelHeight = PanelHeight(worldWidth, worldHeight);
        int width = Columns * PanelWidth + (Columns + 1) * Gap;
        int height = Rows * panelHeight + (Rows + 1) * Gap;
        var rgba = new byte[width * height * 4];
        Fill(rgba, width, 0, 0, width, height, Background);
        for (int index = 0; index < ticks.Count; index++)
        {
            int column = index % Columns, row = index / Columns;
            DrawPanel(rgba, width, Gap + column * (PanelWidth + Gap), Gap + row * (panelHeight + Gap),
                PanelWidth, panelHeight, frames[index], worldWidth, worldHeight, ticks[index]);
        }
        return new Raster(width, height, rgba);
    }

    internal static void DrawPanel(byte[] rgba, int stride, int left, int top, int panelWidth, int panelHeight,
        ReadOnlySpan<KinematicSample> agents, float worldWidth, float worldHeight, int tick)
    {
        Fill(rgba, stride, left, top, panelWidth, panelHeight, PanelBackground);
        double sx = (panelWidth - 1) / (IsUsable(worldWidth) ? worldWidth : 1d);
        double sy = (panelHeight - 1) / (IsUsable(worldHeight) ? worldHeight : 1d);
        // Line length follows the panel, not world units: other foundations use a world of size 10.
        double half = Math.Max(2d, panelWidth * 0.0125) / 2d;
        var clip = new Clip(left + 1, top + 1, left + panelWidth - 2, top + panelHeight - 2);
        foreach (KinematicSample agent in agents)
        {
            if (!float.IsFinite(agent.X) || !float.IsFinite(agent.Y)) continue;
            double cx = left + Math.Clamp(agent.X * sx, 0d, panelWidth - 1);
            double cy = top + Math.Clamp(agent.Y * sy, 0d, panelHeight - 1);
            float speedSquared = agent.Vx * agent.Vx + agent.Vy * agent.Vy;
            if (!(speedSquared > StationarySpeedSquared))
            {
                int x = (int)cx, y = (int)cy;
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                        Plot(rgba, stride, clip, x + dx, y + dy, Stationary);
                continue;
            }
            // Heading is the velocity direction in image space (y down); sx/sy keep it true for any world aspect.
            double heading = Math.Atan2(agent.Vy, agent.Vx);
            double ux = Math.Cos(heading), uy = Math.Sin(heading);
            Line(rgba, stride, clip, cx - ux * half, cy - uy * half, cx + ux * half, cy + uy * half, HeadingColor(heading));
        }
        Outline(rgba, stride, left, top, panelWidth, panelHeight, Border);
        DrawNumber(rgba, stride, left + 5, top + 5, tick);
    }

    /// <summary>Hue wheel by heading angle: east is red, quantised to 1 degree so an ordered flock is one exact colour.</summary>
    internal static (byte R, byte G, byte B) HeadingColor(double headingRadians)
    {
        int step = (int)Math.Round(headingRadians / (2d * Math.PI) * HueSteps);
        step = ((step % HueSteps) + HueSteps) % HueSteps;
        return Hsv(step / (double)HueSteps, 0.85d, 1d);
    }

    private static (byte R, byte G, byte B) Hsv(double hue, double saturation, double value)
    {
        double scaled = hue * 6d;
        int sector = (int)scaled;
        double f = scaled - sector;
        double p = value * (1d - saturation), q = value * (1d - saturation * f), t = value * (1d - saturation * (1d - f));
        (double r, double g, double b) = sector switch
        {
            0 => (value, t, p), 1 => (q, value, p), 2 => (p, value, t),
            3 => (p, q, value), 4 => (t, p, value), _ => (value, p, q)
        };
        return ((byte)Math.Round(r * 255d), (byte)Math.Round(g * 255d), (byte)Math.Round(b * 255d));
    }

    private static bool IsUsable(float extent) => float.IsFinite(extent) && extent > 0f;

    private readonly record struct Clip(int MinX, int MinY, int MaxX, int MaxY);

    private static void Plot(byte[] rgba, int stride, Clip clip, int x, int y, (byte R, byte G, byte B) color)
    {
        if (x < clip.MinX || x > clip.MaxX || y < clip.MinY || y > clip.MaxY) return;
        int offset = (y * stride + x) * 4;
        rgba[offset] = color.R; rgba[offset + 1] = color.G; rgba[offset + 2] = color.B; rgba[offset + 3] = 255;
    }

    /// <summary>Bresenham, two pixels wide (the brush steps across the minor axis) so lines stay visible.</summary>
    private static void Line(byte[] rgba, int stride, Clip clip, double x0, double y0, double x1, double y1, (byte R, byte G, byte B) color)
    {
        int ax = (int)Math.Round(x0), ay = (int)Math.Round(y0), bx = (int)Math.Round(x1), by = (int)Math.Round(y1);
        int dx = Math.Abs(bx - ax), dy = -Math.Abs(by - ay);
        int stepX = ax < bx ? 1 : -1, stepY = ay < by ? 1 : -1;
        bool shallow = dx >= -dy;
        int error = dx + dy;
        while (true)
        {
            Plot(rgba, stride, clip, ax, ay, color);
            if (shallow) Plot(rgba, stride, clip, ax, ay + 1, color); else Plot(rgba, stride, clip, ax + 1, ay, color);
            if (ax == bx && ay == by) break;
            int doubled = 2 * error;
            if (doubled >= dy) { error += dy; ax += stepX; }
            if (doubled <= dx) { error += dx; ay += stepY; }
        }
    }

    private static void Fill(byte[] rgba, int stride, int left, int top, int width, int height, (byte R, byte G, byte B) color)
    {
        for (int y = top; y < top + height; y++)
            for (int x = left; x < left + width; x++)
            {
                int offset = (y * stride + x) * 4;
                rgba[offset] = color.R; rgba[offset + 1] = color.G; rgba[offset + 2] = color.B; rgba[offset + 3] = 255;
            }
    }

    private static void Outline(byte[] rgba, int stride, int left, int top, int width, int height, (byte R, byte G, byte B) color)
    {
        var all = new Clip(left, top, left + width - 1, top + height - 1);
        for (int x = left; x < left + width; x++) { Plot(rgba, stride, all, x, top, color); Plot(rgba, stride, all, x, top + height - 1, color); }
        for (int y = top; y < top + height; y++) { Plot(rgba, stride, all, left, y, color); Plot(rgba, stride, all, left + width - 1, y, color); }
    }

    private static void DrawNumber(byte[] rgba, int stride, int left, int top, int number)
    {
        const int scale = 2;
        string text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int width = text.Length * 4 * scale - scale; // 3 columns + 1 spacing per glyph, no trailing space
        Fill(rgba, stride, left - 2, top - 2, width + 4, 5 * scale + 4, PanelBackground);
        var all = new Clip(0, 0, stride - 1, int.MaxValue / 8);
        for (int i = 0; i < text.Length; i++)
        {
            string glyph = Digits[text[i] - '0'];
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 3; col++)
                    if (glyph[row * 3 + col] == '1')
                        for (int py = 0; py < scale; py++)
                            for (int px = 0; px < scale; px++)
                                Plot(rgba, stride, all, left + i * 4 * scale + col * scale + px, top + row * scale + py, Text);
        }
    }
}
