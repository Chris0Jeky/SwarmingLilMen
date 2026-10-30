using System.Globalization;
using System.Text;

namespace SwarmSim.Cli.Visual;

/// <summary>Sweep-root index.html: one row per cell, linking each cell's report.html by relative path.</summary>
internal static class SweepReport
{
    private const string Css = """
        .scroll{overflow-x:auto;border:1px solid var(--line);border-radius:8px;background:var(--card)}
        table{border-collapse:collapse;width:100%;font-size:.85rem;font-variant-numeric:tabular-nums}
        th,td{padding:6px 10px;border-bottom:1px solid var(--line);text-align:left;white-space:nowrap}
        th{color:var(--muted);font-weight:600;font-size:.75rem}
        td.thumb{padding:4px 10px}
        img{display:block;width:240px;height:auto;border-radius:4px;background:#0a0c10}
        """;

    internal static void Write(string sweepDirectory, SweepPlan plan, IReadOnlyList<SweepCellSummary> results)
    {
        var html = new StringBuilder();
        void Line(string text) => html.Append(text).Append('\n');
        Line("<!doctype html>");
        Line("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        Line("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        Line("<title>Sweep report</title>");
        Line("<style>" + RunReport.BaseCss + "\n" + Css + "</style></head><body><main>");
        Line($"<h1>Sweep report</h1><p class=\"muted\">{plan.Cells.Count} cells. Terminal metric values are from each cell's last tick. Open a thumbnail for the full cell report.</p>");
        Line("<div class=\"scroll\"><table><thead><tr><th>cell</th><th>picture</th><th>seed</th>");
        foreach (string parameter in plan.Parameters) Line($"<th>{RunReport.Esc(parameter)}</th>");
        foreach (string metric in plan.Metrics) Line($"<th>{RunReport.Esc(metric)} (terminal)</th>");
        Line("</tr></thead><tbody>");
        foreach (SweepCell cell in plan.Cells)
        {
            SweepCellSummary result = results[cell.Index];
            string link = cell.Id + "/report.html"; // ponytail: thumbnail is the CSS-scaled filmstrip; a real thumbnail file if sweeps get big
            Line($"<tr><td><a href=\"{link}\">{cell.Id}</a></td>"
                + $"<td class=\"thumb\"><a href=\"{link}\"><img loading=\"lazy\" alt=\"{cell.Id} filmstrip\" src=\"{cell.Id}/filmstrip.png\"></a></td>"
                + $"<td>{cell.Request.Seed.ToString(CultureInfo.InvariantCulture)}</td>"
                + string.Concat(cell.ParameterValues.Select(value => $"<td>{RunReport.Esc(value)}</td>"))
                + string.Concat(result.Terminal.Select(value => $"<td>{RunReport.Fmt(value)}</td>")) + "</tr>");
        }
        Line("</tbody></table></div></main></body></html>");
        File.WriteAllText(Path.Combine(sweepDirectory, "index.html"), html.ToString().ReplaceLineEndings("\n"), new UTF8Encoding(false));
    }
}
