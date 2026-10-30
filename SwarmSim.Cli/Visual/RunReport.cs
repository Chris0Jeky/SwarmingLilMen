using System.Globalization;
using System.Net;
using System.Text;

namespace SwarmSim.Cli.Visual;

/// <summary>Writes filmstrip.png and a self-contained report.html (no external URLs, no scripts) into a run directory.</summary>
internal static class RunReport
{
    internal const string BaseCss = """
        :root{color-scheme:light dark;--bg:#f6f7f9;--fg:#171b22;--muted:#5b6573;--card:#fff;--line:#d9dee5;--accent:#2a6df4}
        @media (prefers-color-scheme:dark){:root{--bg:#0f1318;--fg:#e6eaf0;--muted:#98a2b0;--card:#171c23;--line:#2a313b;--accent:#6ea0ff}}
        *{box-sizing:border-box}
        body{margin:0;padding:16px;background:var(--bg);color:var(--fg);font:15px/1.45 system-ui,-apple-system,"Segoe UI",sans-serif}
        main{max-width:1100px;margin:0 auto}
        h1{font-size:1.25rem;margin:0 0 4px}
        a{color:var(--accent)}
        .muted{color:var(--muted);font-size:.85rem}
        """;

    private const string ReportCss = """
        dl{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:8px;margin:12px 0}
        dl div{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:8px 10px;min-width:0}
        dt{font-size:.75rem;color:var(--muted)}
        dd{margin:0;font-weight:600;overflow-wrap:anywhere;font-variant-numeric:tabular-nums}
        .film{display:block;width:100%;height:auto;border:1px solid var(--line);border-radius:8px;background:#0a0c10}
        .cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:10px;margin-top:12px}
        .card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:8px 10px;min-width:0}
        .card h2{font-size:.85rem;margin:0 0 4px;overflow-wrap:anywhere}
        svg{display:block;width:100%;height:56px}
        polyline{fill:none;stroke:var(--accent);stroke-width:1.5;stroke-linecap:round;stroke-linejoin:round;vector-effect:non-scaling-stroke}
        .stats{display:flex;flex-wrap:wrap;gap:2px 10px;font-size:.75rem;color:var(--muted);font-variant-numeric:tabular-nums}
        """;

    private const double SparkWidth = 240d, SparkHeight = 56d, SparkPad = 3d;

    internal static void Write(string directory, FrameRecorder frames, RunManifest manifest)
    {
        Raster raster = Filmstrip.Render(frames.Ticks, frames.Frames, frames.WorldWidth, frames.WorldHeight);
        byte[] png = PngWriter.Encode(raster.Width, raster.Height, raster.Rgba);
        File.WriteAllBytes(Path.Combine(directory, "filmstrip.png"), png);
        string csv = File.ReadAllText(Path.Combine(directory, "metrics.csv"));
        File.WriteAllText(Path.Combine(directory, "report.html"), Html(manifest, png, csv), new UTF8Encoding(false));
    }

    internal static string Html(RunManifest manifest, byte[] png, string metricsCsv)
    {
        string[] lines = metricsCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] header = lines[0].Split(',');
        var ticks = new List<double>();
        var columns = new List<double>[header.Length - 1];
        for (int c = 0; c < columns.Length; c++) columns[c] = [];
        for (int row = 1; row < lines.Length; row++)
        {
            string[] cells = lines[row].Split(',');
            ticks.Add(double.Parse(cells[0], CultureInfo.InvariantCulture));
            for (int c = 0; c < columns.Length; c++)
                columns[c].Add(c + 1 < cells.Length && double.TryParse(cells[c + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                    && double.IsFinite(v) ? v : double.NaN); // empty cells are undefined values, never zero
        }

        string commit = manifest.GitCommit is null ? "unknown"
            : manifest.GitCommit[..Math.Min(12, manifest.GitCommit.Length)] + (manifest.DirtyWorktree == true ? " (dirty)" : "");
        var html = new StringBuilder();
        void Line(string text) => html.Append(text).Append('\n');
        Line("<!doctype html>");
        Line("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        Line("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        Line($"<title>Run report {Esc(manifest.Engine)} seed {manifest.Seed}</title>");
        Line("<style>" + BaseCss + "\n" + ReportCss + "</style></head><body><main>");
        Line($"<h1>Swarm run report</h1><p class=\"muted\">{Esc(manifest.RunId)}</p>");
        Line("<dl>");
        foreach ((string key, string value) in new[]
        {
            ("Engine", manifest.Engine), ("Seed", manifest.Seed.ToString(CultureInfo.InvariantCulture)),
            ("Ticks", manifest.Ticks.ToString(CultureInfo.InvariantCulture)), ("Agents", manifest.AgentCount.ToString(CultureInfo.InvariantCulture)),
            ("Git commit", commit), ("Final kinematic hash", manifest.FinalKinematicHash)
        })
            Line($"<div><dt>{Esc(key)}</dt><dd>{Esc(value)}</dd></div>");
        Line("</dl>");
        Line("<p class=\"muted\">Colour is heading angle: one colour means an aligned flock, a rainbow means disorder, grey dots are stationary. Tap the image for full size.</p>");
        Line($"<a href=\"filmstrip.png\"><img class=\"film\" alt=\"Filmstrip of the swarm at evenly spaced ticks\" src=\"data:image/png;base64,{Convert.ToBase64String(png)}\"></a>");
        Line("<section class=\"cards\">");
        for (int c = 0; c < columns.Length; c++)
            Line(Card(header[c + 1], ticks, columns[c]));
        Line("</section></main></body></html>");
        return html.ToString().ReplaceLineEndings("\n"); // raw-string CSS carries the source file's newlines
    }

    private static string Card(string name, List<double> ticks, List<double> values)
    {
        double first = double.NaN, last = double.NaN, min = double.PositiveInfinity, max = double.NegativeInfinity;
        foreach (double v in values)
        {
            if (double.IsNaN(v)) continue;
            if (double.IsNaN(first)) first = v;
            last = v;
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        var card = new StringBuilder();
        card.Append($"<div class=\"card\"><h2>{Esc(name)}</h2>");
        if (double.IsNaN(first))
            return card.Append("<p class=\"muted\">no defined values</p></div>").ToString();

        double maxTick = ticks[^1];
        double range = max - min;
        card.Append($"<svg viewBox=\"0 0 {Num(SparkWidth)} {Num(SparkHeight)}\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"{Esc(name)} over time\">");
        var segment = new StringBuilder();
        int run = 0;
        void Flush()
        {
            if (segment.Length == 0) return;
            if (run == 1) segment.Append(segment.ToString()); // lone point: zero-length round-capped line draws a dot
            card.Append($"<polyline points=\"{segment.ToString().TrimEnd()}\"/>");
            segment.Clear();
            run = 0;
        }
        for (int i = 0; i < values.Count; i++)
        {
            if (double.IsNaN(values[i])) { Flush(); continue; } // gap: never bridged, never zero
            double x = maxTick > 0 ? SparkPad + ticks[i] / maxTick * (SparkWidth - 2 * SparkPad) : SparkWidth / 2;
            double y = range > 0 ? SparkHeight - SparkPad - (values[i] - min) / range * (SparkHeight - 2 * SparkPad) : SparkHeight / 2;
            segment.Append(Num(x)).Append(',').Append(Num(y)).Append(' ');
            run++;
        }
        Flush();
        card.Append("</svg>");
        card.Append($"<div class=\"stats\"><span>first {Fmt(first)}</span><span>last {Fmt(last)}</span><span>min {Fmt(min)}</span><span>max {Fmt(max)}</span></div></div>");
        return card.ToString();
    }

    internal static string Esc(string text) => WebUtility.HtmlEncode(text);
    internal static string Fmt(double value) => double.IsNaN(value) ? "-" : value.ToString("G6", CultureInfo.InvariantCulture);
    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
