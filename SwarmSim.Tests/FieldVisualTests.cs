using SwarmSim.Cli.Visual;

namespace SwarmSim.Tests;

public sealed class FieldVisualTests
{
    private static readonly (byte R, byte G, byte B) Low = (68, 1, 84);
    private static readonly (byte R, byte G, byte B) Quarter = (59, 82, 139);
    private static readonly (byte R, byte G, byte B) Middle = (33, 145, 140);
    private static readonly (byte R, byte G, byte B) High = (253, 231, 37);

    [Fact]
    public void RowMajorCellsFillAreasWithKnownSequentialColors()
    {
        Raster image = FieldFilmstrip.Render([0], [new[] { 0f, 0.25f, 0.5f, 1f }], 2, 2, 0f, 1f);

        Assert.Equal(Low, CellCenter(image, 0, 0, 0, 2, 2));
        Assert.Equal(Quarter, CellCenter(image, 0, 1, 0, 2, 2));
        Assert.Equal(Middle, CellCenter(image, 0, 0, 1, 2, 2));
        Assert.Equal(High, CellCenter(image, 0, 1, 1, 2, 2));
        // A cell is a filled region, including pixels well away from its center.
        Assert.Equal(Low, Pixel(image, Filmstrip.Gap + 1, Filmstrip.Gap + 40));
        Assert.Equal(Low, Pixel(image, Filmstrip.Gap + 238, Filmstrip.Gap + 238));
        Assert.Equal(High, Pixel(image, Filmstrip.Gap + 478, Filmstrip.Gap + 478));
    }

    [Fact]
    public void EveryFrameUsesTheSameScaleAndOnlyDisplayValuesAreClamped()
    {
        float[][] frames = [new[] { 0f, 0.5f, 1f }, new[] { -100f, 0.5f, 100f }];
        int[][] before = frames.Select(frame => frame.Select(BitConverter.SingleToInt32Bits).ToArray()).ToArray();

        Raster image = FieldFilmstrip.Render([0, 20], frames, 3, 1, 0f, 1f);

        for (int frame = 0; frame < frames.Length; frame++)
        {
            Assert.Equal(Low, CellCenter(image, frame, 0, 0, 3, 1));
            Assert.Equal(Middle, CellCenter(image, frame, 1, 0, 3, 1));
            Assert.Equal(High, CellCenter(image, frame, 2, 0, 3, 1));
            Assert.Equal(before[frame], frames[frame].Select(BitConverter.SingleToInt32Bits));
        }
    }

    [Fact]
    public void NonfiniteValuesAreNeutralGrayAndTheirBitsRemainUntouched()
    {
        float[] values = [BitConverter.Int32BitsToSingle(unchecked((int)0x7FC01234)),
            float.PositiveInfinity, float.NegativeInfinity, -0f];
        int[] before = values.Select(BitConverter.SingleToInt32Bits).ToArray();

        Raster image = FieldFilmstrip.Render([7], [values], 4, 1, 0f, 1f);

        for (int column = 0; column < 3; column++)
            Assert.Equal(((byte)150, (byte)150, (byte)150), CellCenter(image, 0, column, 0, 4, 1));
        Assert.Equal(Low, CellCenter(image, 0, 3, 0, 4, 1));
        Assert.Equal(before, values.Select(BitConverter.SingleToInt32Bits));
    }

    [Fact]
    public void FiniteColorRangesWorkAcrossFloatOverflowAndSubnormalScales()
    {
        Raster wide = FieldFilmstrip.Render([0], [new[] { -float.MaxValue, 0f, float.MaxValue }],
            3, 1, -float.MaxValue, float.MaxValue);
        Assert.Equal(Low, CellCenter(wide, 0, 0, 0, 3, 1));
        Assert.Equal(Middle, CellCenter(wide, 0, 1, 0, 3, 1));
        Assert.Equal(High, CellCenter(wide, 0, 2, 0, 3, 1));

        Raster tiny = FieldFilmstrip.Render([0], [new[] { 0f, float.Epsilon }], 2, 1, 0f, float.Epsilon);
        Assert.Equal(Low, CellCenter(tiny, 0, 0, 0, 2, 1));
        Assert.Equal(High, CellCenter(tiny, 0, 1, 0, 2, 1));
    }

    [Fact]
    public void PaletteInterpolationHasDeterministicByteRounding()
    {
        Raster image = FieldFilmstrip.Render([0], [new[] { 0.125f }], 1, 1, 0f, 1f);

        Assert.Equal(((byte)64, (byte)42, (byte)112), CellCenter(image, 0, 0, 0, 1, 1));
        Assert.Equal(image.Rgba, FieldFilmstrip.Render([0], [new[] { 0.125f }], 1, 1, 0f, 1f).Rgba);
    }

    [Fact]
    public void LayoutAndFinalCropMatchTheExistingFilmstripIncludingTickLabels()
    {
        int[] ticks = [0, 1, 2, 3, int.MaxValue];
        float[][] frames = [new[] { 0f, 0f }, new[] { 0.25f, 0.25f }, new[] { 0.5f, 0.5f },
            new[] { 0.75f, 0.75f }, new[] { 1f, 1f }];

        Raster image = FieldFilmstrip.Render(ticks, frames, 2, 1, 0f, 1f);
        Raster final = Filmstrip.CropFinalPanel(image, frames.Length);

        int panelHeight = Filmstrip.PanelHeight(2, 1);
        Assert.Equal(Filmstrip.Columns * Filmstrip.PanelWidth + (Filmstrip.Columns + 1) * Filmstrip.Gap, image.Width);
        Assert.Equal(Filmstrip.Rows * panelHeight + (Filmstrip.Rows + 1) * Filmstrip.Gap, image.Height);
        Assert.Equal(Filmstrip.PanelWidth, final.Width);
        Assert.Equal(panelHeight, final.Height);
        Assert.Equal(High, Pixel(final, final.Width / 2, final.Height / 2));
        Assert.Equal(Filmstrip.Border, Pixel(final, 0, 0));
        Assert.Equal(Filmstrip.Text, Pixel(final, 5, 5)); // First row of the leading '2'.
        Assert.Equal(Filmstrip.Background, Pixel(image, 0, 0));
        Assert.Equal(Filmstrip.Background, Pixel(image,
            Filmstrip.Gap + Filmstrip.PanelWidth + Filmstrip.Gap + 100,
            Filmstrip.Gap + panelHeight + Filmstrip.Gap + 100)); // Empty sixth panel.
    }

    [Fact]
    public void AGridFinerThanThePanelIsSampledDeterministicallyWithoutGaps()
    {
        float[] values = Enumerable.Range(0, 1000).Select(index => index < 500 ? 0f : 1f).ToArray();

        Raster image = FieldFilmstrip.Render([0], [values], 1000, 1, 0f, 1f);

        Assert.Equal(48, Filmstrip.PanelHeight(1000, 1));
        Assert.Equal(Low, Pixel(image, Filmstrip.Gap + 1, Filmstrip.Gap + 30));
        Assert.Equal(High, Pixel(image, Filmstrip.Gap + Filmstrip.PanelWidth - 2, Filmstrip.Gap + 30));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(int.MaxValue, 2)]
    [InlineData(65536, 65536)]
    public void InvalidOrOverflowingDimensionsAreRejectedBeforeRendering(int columns, int rows)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FieldFilmstrip.Render([0], [new[] { 0f }], columns, rows, 0f, 1f));
    }

    [Theory]
    [InlineData(float.NaN, 1f)]
    [InlineData(float.NegativeInfinity, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(0f, float.NaN)]
    [InlineData(0f, float.PositiveInfinity)]
    [InlineData(0f, float.NegativeInfinity)]
    [InlineData(1f, 1f)]
    [InlineData(2f, 1f)]
    public void TheSharedDisplayScaleMustBeFiniteAndIncreasing(float minimum, float maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FieldFilmstrip.Render([0], [new[] { 0f }], 1, 1, minimum, maximum));
    }

    [Fact]
    public void NullMismatchedMissingAndOversizedFramesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => FieldFilmstrip.Render(null!, [new[] { 0f }], 1, 1, 0f, 1f));
        Assert.Throws<ArgumentNullException>(() => FieldFilmstrip.Render([0], null!, 1, 1, 0f, 1f));
        Assert.Throws<ArgumentException>(() => FieldFilmstrip.Render([], [], 1, 1, 0f, 1f));
        Assert.Throws<ArgumentException>(() => FieldFilmstrip.Render([0, 1], [new[] { 0f }], 1, 1, 0f, 1f));
        Assert.Throws<ArgumentException>(() => FieldFilmstrip.Render([0], [null!], 1, 1, 0f, 1f));
        Assert.Throws<ArgumentException>(() => FieldFilmstrip.Render([0], [Array.Empty<float>()], 1, 1, 0f, 1f));
        Assert.Throws<ArgumentException>(() => FieldFilmstrip.Render([0], [new[] { 0f, 1f }], 1, 1, 0f, 1f));
        Assert.Throws<ArgumentException>(() => FieldFilmstrip.Render([0], [new[] { 0f }], 2, 1, 0f, 1f));
        Assert.Throws<ArgumentException>(() => FieldFilmstrip.Render(
            Enumerable.Range(0, Filmstrip.PanelCount + 1).ToArray(),
            Enumerable.Range(0, Filmstrip.PanelCount + 1).Select(_ => new[] { 0f }).ToArray(), 1, 1, 0f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => FieldFilmstrip.Render([-1], [new[] { 0f }], 1, 1, 0f, 1f));
    }

    private static (byte R, byte G, byte B) CellCenter(Raster image, int frame, int column, int row, int columns, int rows)
    {
        int panelHeight = Filmstrip.PanelHeight(columns, rows);
        int left = Filmstrip.Gap + frame % Filmstrip.Columns * (Filmstrip.PanelWidth + Filmstrip.Gap);
        int top = Filmstrip.Gap + frame / Filmstrip.Columns * (panelHeight + Filmstrip.Gap);
        int x = left + 1 + (int)((2L * column + 1) * (Filmstrip.PanelWidth - 2) / (2L * columns));
        int y = top + 1 + (int)((2L * row + 1) * (panelHeight - 2) / (2L * rows));
        return Pixel(image, x, y);
    }

    private static (byte R, byte G, byte B) Pixel(Raster image, int x, int y)
    {
        int offset = (y * image.Width + x) * 4;
        Assert.Equal((byte)255, image.Rgba[offset + 3]);
        return (image.Rgba[offset], image.Rgba[offset + 1], image.Rgba[offset + 2]);
    }
}
