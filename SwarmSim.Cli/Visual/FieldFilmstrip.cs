using System.Globalization;

namespace SwarmSim.Cli.Visual;

/// <summary>
/// Fixed-scale sequential heatmaps of row-major scalar fields. Cell colors fill the panel
/// interior; grids finer than the raster use deterministic nearest-cell sampling. Tick labels
/// overlay the upper-left corner. Layout matches Filmstrip so its final-panel crop stays valid.
/// Rendering never changes captured values or derives a scale from individual frames.
/// </summary>
internal static class FieldFilmstrip
{
    // A fixed purple-blue-green-yellow sequence; linear RGB interpolation, rounded to bytes.
    private static readonly (byte R, byte G, byte B)[] Palette =
        [(68, 1, 84), (59, 82, 139), (33, 145, 140), (94, 201, 98), (253, 231, 37)];
    private static readonly (byte R, byte G, byte B) Missing = (150, 150, 150);

    // Same compact 3x5 decimal glyphs as the agent filmstrip, kept local to avoid changing it.
    private static readonly string[] Digits =
    [
        "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
        "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
    ];

    internal static Raster Render(IReadOnlyList<int> ticks, IReadOnlyList<float[]> frames,
        int columns, int rows, float colorMinimum, float colorMaximum)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        ArgumentNullException.ThrowIfNull(frames);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns), "A field needs positive dimensions.");
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), "A field needs positive dimensions.");
        if ((long)columns * rows > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(columns), "The field cell count must fit in an array.");
        if (!float.IsFinite(colorMinimum))
            throw new ArgumentOutOfRangeException(nameof(colorMinimum), "The display minimum must be finite.");
        if (!float.IsFinite(colorMaximum) || colorMaximum <= colorMinimum)
            throw new ArgumentOutOfRangeException(nameof(colorMaximum), "The display maximum must be finite and greater than the minimum.");
        if (ticks.Count != frames.Count || ticks.Count is 0 or > Filmstrip.PanelCount)
            throw new ArgumentException("A field filmstrip needs between 1 and 8 frames, one per tick.", nameof(frames));

        int cellCount = columns * rows;
        for (int index = 0; index < frames.Count; index++)
        {
            if (ticks[index] < 0)
                throw new ArgumentOutOfRangeException(nameof(ticks), "Frame ticks must be nonnegative.");
            if (frames[index] is null || frames[index].Length != cellCount)
                throw new ArgumentException("Every field frame must contain exactly columns * rows values.", nameof(frames));
        }

        int panelHeight = Filmstrip.PanelHeight(columns, rows);
        int width = Filmstrip.Columns * Filmstrip.PanelWidth + (Filmstrip.Columns + 1) * Filmstrip.Gap;
        int height = Filmstrip.Rows * panelHeight + (Filmstrip.Rows + 1) * Filmstrip.Gap;
        var rgba = new byte[checked(width * height * 4)];
        Fill(rgba, width, 0, 0, width, height, Filmstrip.Background);
        // Widen before subtraction: a valid range can span -float.MaxValue through +float.MaxValue.
        double range = (double)colorMaximum - colorMinimum;
        for (int index = 0; index < frames.Count; index++)
        {
            int left = Filmstrip.Gap + index % Filmstrip.Columns * (Filmstrip.PanelWidth + Filmstrip.Gap);
            int top = Filmstrip.Gap + index / Filmstrip.Columns * (panelHeight + Filmstrip.Gap);
            DrawPanel(rgba, width, left, top, panelHeight, frames[index], columns, rows,
                colorMinimum, range, ticks[index]);
        }
        return new Raster(width, height, rgba);
    }

    private static void DrawPanel(byte[] rgba, int stride, int left, int top, int panelHeight,
        ReadOnlySpan<float> frame, int columns, int rows, double minimum, double range, int tick)
    {
        int interiorWidth = Filmstrip.PanelWidth - 2;
        int interiorHeight = panelHeight - 2;
        for (int y = 0; y < interiorHeight; y++)
        {
            // Sample the cell containing each pixel center, with wide products for large grids.
            int row = (int)((2L * y + 1) * rows / (2L * interiorHeight));
            for (int x = 0; x < interiorWidth; x++)
            {
                int column = (int)((2L * x + 1) * columns / (2L * interiorWidth));
                SetPixel(rgba, stride, left + 1 + x, top + 1 + y,
                    Color(frame[row * columns + column], minimum, range));
            }
        }
        Fill(rgba, stride, left, top, Filmstrip.PanelWidth, 1, Filmstrip.Border);
        Fill(rgba, stride, left, top + panelHeight - 1, Filmstrip.PanelWidth, 1, Filmstrip.Border);
        Fill(rgba, stride, left, top, 1, panelHeight, Filmstrip.Border);
        Fill(rgba, stride, left + Filmstrip.PanelWidth - 1, top, 1, panelHeight, Filmstrip.Border);
        DrawNumber(rgba, stride, left + 5, top + 5, tick);
    }

    private static (byte R, byte G, byte B) Color(float value, double minimum, double range)
    {
        if (!float.IsFinite(value)) return Missing;
        double scaled = Math.Clamp(((double)value - minimum) / range, 0d, 1d) * (Palette.Length - 1);
        int segment = Math.Min((int)scaled, Palette.Length - 2);
        double fraction = scaled - segment;
        (byte R, byte G, byte B) low = Palette[segment], high = Palette[segment + 1];
        return ((byte)Math.Round(low.R + (high.R - low.R) * fraction),
            (byte)Math.Round(low.G + (high.G - low.G) * fraction),
            (byte)Math.Round(low.B + (high.B - low.B) * fraction));
    }

    private static void SetPixel(byte[] rgba, int stride, int x, int y, (byte R, byte G, byte B) color)
    {
        int offset = (y * stride + x) * 4;
        rgba[offset] = color.R;
        rgba[offset + 1] = color.G;
        rgba[offset + 2] = color.B;
        rgba[offset + 3] = 255;
    }

    private static void Fill(byte[] rgba, int stride, int left, int top, int width, int height,
        (byte R, byte G, byte B) color)
    {
        for (int y = top; y < top + height; y++)
            for (int x = left; x < left + width; x++)
                SetPixel(rgba, stride, x, y, color);
    }

    private static void DrawNumber(byte[] rgba, int stride, int left, int top, int tick)
    {
        const int scale = 2;
        string text = tick.ToString(CultureInfo.InvariantCulture);
        int labelWidth = text.Length * 4 * scale - scale;
        Fill(rgba, stride, left - 2, top - 2, labelWidth + 4, 5 * scale + 4, Filmstrip.PanelBackground);
        for (int index = 0; index < text.Length; index++)
        {
            string glyph = Digits[text[index] - '0'];
            for (int row = 0; row < 5; row++)
                for (int column = 0; column < 3; column++)
                    if (glyph[row * 3 + column] == '1')
                        Fill(rgba, stride, left + index * 4 * scale + column * scale,
                            top + row * scale, scale, scale, Filmstrip.Text);
        }
    }
}
