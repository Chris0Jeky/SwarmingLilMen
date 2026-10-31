using System.Globalization;
using System.Text;

namespace SwarmSim.Cli.Visual;

/// <summary>Gallery-root index.html: one card per entry, failures first, linking each run's report.html by relative path.</summary>
internal static class GalleryReport
{
    private const string Css = """
        :root{--pass:#1a7f37;--fail:#c4261d}
        @media (prefers-color-scheme:dark){:root{--pass:#56d364;--fail:#ff7b72}}
        .banner{font-weight:600;margin:8px 0}
        .banner.pass{color:var(--pass)}
        .banner.fail{color:var(--fail)}
        .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(min(100%,480px),1fr));gap:12px;margin-top:12px}
        .gc{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:10px;min-width:0}
        .gc.fail{border-color:var(--fail)}
        .gc h2{font-size:1rem;margin:8px 0 2px;overflow-wrap:anywhere}
        .gc img{display:block;width:100%;height:auto;border-radius:6px;background:#0a0c10}
        .gc ul{list-style:none;margin:6px 0 0;padding:0;font-size:.85rem;font-variant-numeric:tabular-nums}
        .gc li{overflow-wrap:anywhere;margin:2px 0}
        .badge{display:inline-block;min-width:3.2em;text-align:center;font-size:.7rem;font-weight:700;padding:1px 6px;border-radius:4px;border:1px solid currentColor}
        .badge.pass{color:var(--pass)}
        .badge.fail{color:var(--fail)}
        """;

    internal static void Write(string galleryDirectory, IReadOnlyList<EntryResult> results)
    {
        int failed = results.Count(result => !result.Passed);
        // ponytail: OrderBy is stable, so failures keep recipe order among themselves, then the passes do.
        EntryResult[] ordered = results.OrderBy(result => result.Passed).ToArray();
        var html = new StringBuilder();
        void Line(string text) => html.Append(text).Append('\n');
        Line("<!doctype html>");
        Line("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        Line("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        Line("<title>Behaviour gallery</title>");
        Line("<style>" + RunReport.BaseCss + "\n" + Css + "</style></head><body><main>");
        Line("<h1>Behaviour gallery</h1>");
        Line(failed == 0
            ? $"<p class=\"banner pass\">PASS: all {results.Count} entries meet their bands.</p>"
            : $"<p class=\"banner fail\">FAIL: {failed} of {results.Count} entries are outside their bands (shown first).</p>");
        Line("<p class=\"muted\">Each band bounds the mean of one metric over the final quarter of ticks. They are coarse behavioural sanity checks on one seed each, not determinism or science gates. Open a picture for the full run report.</p>");
        Line("<section class=\"grid\">");
        foreach (EntryResult result in ordered)
        {
            string state = result.Passed ? "pass" : "fail";
            string name = RunReport.Esc(result.Entry.Name);
            string folder = RunReport.Esc(result.Entry.Directory);
            RunRequest request = result.Entry.Request;
            Line($"<article class=\"gc {state}\"><a href=\"{folder}/report.html\"><img loading=\"lazy\" alt=\"{name} filmstrip\" src=\"{folder}/filmstrip.png\"></a>");
            Line($"<h2><a href=\"{folder}/report.html\">{name}</a></h2>");
            Line(string.Create(CultureInfo.InvariantCulture,
                $"<p class=\"muted\">{RunReport.Esc(request.Engine)} - seed {request.Seed} - {request.AgentCount} agents - {request.Ticks} ticks</p>"));
            Line("<ul>");
            foreach (ExpectationResult expectation in result.Expectations)
                Line($"<li><span class=\"badge {(expectation.Passed ? "pass" : "fail")}\">{(expectation.Passed ? "PASS" : "FAIL")}</span> {RunReport.Esc(expectation.Text)}</li>");
            Line("</ul></article>");
        }
        Line("</section></main></body></html>");
        File.WriteAllText(Path.Combine(galleryDirectory, "index.html"), html.ToString().ReplaceLineEndings("\n"), new UTF8Encoding(false));
    }
}
