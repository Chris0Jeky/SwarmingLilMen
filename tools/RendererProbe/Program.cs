using System.Net;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Raylib_cs;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Render;
using RenderProgram = SwarmSim.Render.Program;

if (args is ["--help"])
{
    Console.WriteLine("RendererProbe [new-output-directory]\nNative canonical inspection checks, PNG gallery and JSON report. Requires a graphics context (Linux: xvfb-run -a). Default: artifacts/renderer-probe/<UTC timestamp>. Existing output directories are refused.");
    return 0;
}
if (args.Length > 1) { Console.Error.WriteLine("Expected at most one output directory. See --help."); return 2; }
string output = Path.GetFullPath(args.Length == 1 ? args[0] : Path.Combine("artifacts", "renderer-probe", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")));
if (Directory.Exists(output) || File.Exists(output))
{
    Console.Error.WriteLine($"Output already exists; choose a new directory: {output}");
    return 2;
}
Directory.CreateDirectory(output);
var cases = new List<ProbeCase>();
var checks = new List<ProbeCheck>();
var elapsed = System.Diagnostics.Stopwatch.StartNew();
TextWriter console = Console.Out;
bool windowReady = false;
try
{
    Console.SetOut(TextWriter.Null);
    Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
    Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
    Raylib.InitWindow(1920, 1080, "Canonical renderer probe");
    windowReady = Raylib.IsWindowReady();
    if (!windowReady) throw new InvalidOperationException("Native graphics context unavailable.");

    CheckControls();
    Capture("all-rules", 270);
    Capture("separation-on", 270, closeNeighbor: true);
    Capture("separation-off", 270, KeyboardKey.S, closeNeighbor: true);
    Capture("alignment-off", 270, KeyboardKey.A);
    Capture("cohesion-off", 270, KeyboardKey.C);
    Capture("fov-90", 90);
    Capture("fov-360", 360);
    Capture("wrapped-seam", 360, seam: true);
    Capture("help", 270, help: true);
}
catch (Exception ex) { checks.Add(new("native execution", false, ex.Message)); }
finally
{
    Console.SetOut(console);
    if (windowReady) Raylib.CloseWindow();
}

bool passed = checks.All(c => c.Passed) && cases.Count == 9
    && cases.All(c => c.Checks.All(check => check.Passed) && c.ArtifactChecks.All(check => check.Passed));
int behaviorChecks = checks.Count + cases.Sum(c => c.Checks.Count);
int artifactChecks = cases.Sum(c => c.ArtifactChecks.Count);
var report = new
{
    Passed = passed,
    ElapsedSeconds = elapsed.Elapsed.TotalSeconds,
    Runtime = RuntimeInformation.FrameworkDescription,
    Platform = RuntimeInformation.OSDescription,
    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    Input = "Raylib automation events -> native input adapter -> production controls",
    Capture = "1920x1080 native render target; production overlay/help; explicit six-boid scenes including wrapped links",
    NotVerified = "OS keyboard/focus, full-window DPI, human acceptance, performance throughput",
    BehaviorCheckCount = behaviorChecks,
    ArtifactCheckCount = artifactChecks,
    Assemblies = new[] { typeof(RenderProgram).Assembly, typeof(CanonicalWorld).Assembly, Assembly.GetExecutingAssembly() }
        .Select(a => new { Name = a.GetName().Name, Version = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }),
    Checks = checks,
    Cases = cases
};
File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
string Encode(string text) => WebUtility.HtmlEncode(text);
string CheckList(IEnumerable<ProbeCheck> items) => string.Join("", items.Select(c => $"<li class='{(c.Passed ? "pass" : "fail")}'>{(c.Passed ? "PASS" : "FAIL")} {Encode(c.Name)}{(c.Detail is null ? "" : ": " + Encode(c.Detail))}</li>"));
string cards = string.Join("", cases.Select(c => $"<article><h2>{Encode(c.Name)}</h2><a href='{c.Name}.png'><img src='{c.Name}.png' alt='{Encode(c.Name)} canonical inspection capture'></a><ul>{CheckList(c.Checks)}</ul><h3>Artifact checks</h3><ul>{CheckList(c.ArtifactChecks)}</ul></article>"));
File.WriteAllText(Path.Combine(output, "index.html"), $$"""
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Canonical renderer probe — {{(passed ? "PASS" : "FAIL")}}</title>
<style>body{margin:2rem;background:#11151d;color:#e7ebf3;font:16px system-ui}a{color:#8dceff}h1{margin-bottom:.4rem}p{max-width:85ch}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,600px),1fr));gap:1.5rem}article{border:1px solid #465266;padding:1rem}img{width:100%;height:auto}li{margin:.3rem 0}.pass{color:#a4deb6}.fail{color:#ffb7ae}</style>
<h1>{{(passed ? "PASS" : "FAIL")}} · Canonical renderer probe</h1>
<p>Native event playback and production overlay rendering in {{elapsed.Elapsed.TotalSeconds:F2}} seconds. Open an image at full size to inspect it.</p>
<p>{{behaviorChecks}} behavior checks; {{artifactChecks}} separate export/dimension checks.</p>
<p>Not verified: OS keyboard/focus, full-window DPI, human acceptance or throughput. <a href="report.json">Machine-readable report and build hashes</a>.</p>
<ul>{{CheckList(checks)}}</ul><main>{{cards}}</main></html>
""");
foreach (var check in checks.Concat(cases.SelectMany(c => c.Checks.Concat(c.ArtifactChecks))))
    if (!check.Passed) Console.Error.WriteLine($"FAIL {check.Name}: {check.Detail}");
Console.WriteLine($"{(passed ? "PASS" : "FAIL")}: {cases.Count} captures, {behaviorChecks} behavior checks, {artifactChecks} artifact checks in {elapsed.Elapsed.TotalSeconds:F2}s\n{Path.Combine(output, "index.html")}");
return passed ? 0 : 1;

void CheckControls()
{
    ResetConfig();
    RenderProgram.ProcessCanonicalParameters(Tap(KeyboardKey.Eight));
    RenderProgram.ProcessCanonicalParameters(Tap(KeyboardKey.LeftShift, KeyboardKey.Up));
    checks.Add(new("native Shift + Up reaches FOV setting", RenderProgram.BuildCanonicalWorldSettings().FieldOfView == 271));
    RenderProgram.ProcessCanonicalParameters(Tap(KeyboardKey.Down));
    checks.Add(new("native Down adjusts FOV by 15", RenderProgram.BuildCanonicalWorldSettings().FieldOfView == 256));
    foreach (var (key, action) in new[] { (KeyboardKey.O, RendererControlId.Interactions), (KeyboardKey.Tab, RendererControlId.NextSubject),
        (KeyboardKey.H, RendererControlId.Help), (KeyboardKey.R, RendererControlId.Reset) })
        checks.Add(new($"native {key} reaches {action}", Tap(key).IsActive(action)));
}

void Capture(string name, int fov, KeyboardKey? toggle = null, bool closeNeighbor = false, bool help = false, bool seam = false)
{
    ResetConfig();
    RenderProgram.ProcessCanonicalParameters(Tap(KeyboardKey.Eight));
    for (int degrees = 270; degrees != fov; degrees += fov < 270 ? -15 : 15)
        RenderProgram.ProcessCanonicalParameters(Tap(fov < 270 ? KeyboardKey.Down : KeyboardKey.Up));
    CanonicalWorldSettings settings = RenderProgram.BuildCanonicalWorldSettings();
    var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, 1920, 1080));
    // The explicit unwrapped fixture also supplies an independent pixel oracle. In the seam
    // scene, two input positions lie left of zero and their actual boids are placed at +1920.
    Vector2 center = new(seam ? 80 : 920, 570);
    Vector2[] inputs = [center, center + new Vector2(closeNeighbor ? 30 : 130, 30),
        center + new Vector2(90, -100), center + new Vector2(-50, -130),
        center + new Vector2(-190, -20), center + new Vector2(-140, 130)];
    Vec2[] headings = [new(1, 0), new(0.9f, 0.15f), new(1, 0.2f), new(1, -0.1f), new(1, 0.1f), new(0.9f, 0.2f)];
    for (int i = 0; i < inputs.Length; i++)
    {
        Vector2 position = inputs[i];
        if (position.X < 0) position.X += 1920;
        if (!world.TryAddBoid(new Vec2(position.X, position.Y), headings[i]))
            throw new InvalidOperationException($"Could not add fixture boid {i}.");
    }
    if (toggle is { } key) CanonicalInspectionOverlay.ApplyRuleSwitches(Tap(key), world);
    world.Step(settings.FixedDeltaTime);
    world.TryGetMetrics(0, out var metrics);
    Vec2 force = world.Instrumentation.SteeringVectors[0];
    var neighbors = new int[128];
    var weights = new float[128];
    int visible = world.QueryLastTickVisibleNeighbors(0, neighbors, weights).Count;
    var result = new ProbeCase(name, fov, visible, metrics.SeparationMagnitude, metrics.AlignmentMagnitude, metrics.CohesionMagnitude, force.X, force.Y, [], []);
    cases.Add(result);
    void Check(string description, bool condition, string? detail = null) => result.Checks.Add(new($"{name}: {description}", condition, detail));
    void ArtifactCheck(string description, bool condition) => result.ArtifactChecks.Add(new($"{name}: {description}", condition));
    Check("native FOV control sets configuration", settings.FieldOfView == fov, $"expected {fov}, observed {settings.FieldOfView}");
    int[] expectedVisible = fov == 90 ? [1] : fov == 360 ? [1, 2, 3, 4, 5] : [1, 2, 3];
    Check("expected visible inputs", neighbors.AsSpan(0, visible).SequenceEqual(expectedVisible),
        $"expected [{string.Join(",", expectedVisible)}], observed [{string.Join(",", neighbors.Take(visible))}]");
    bool separation = closeNeighbor && toggle != KeyboardKey.S;
    bool alignment = !separation && toggle != KeyboardKey.A;
    bool cohesion = !separation && toggle != KeyboardKey.C;
    Check("separation applied state", (metrics.SeparationMagnitude > 0) == separation);
    Check("alignment applied state", (metrics.AlignmentMagnitude > 0) == alignment);
    Check("cohesion applied state", (metrics.CohesionMagnitude > 0) == cohesion);

    RenderTexture2D target = Raylib.LoadRenderTexture(1920, 1080);
    try
    {
        Raylib.BeginDrawing();
        Raylib.BeginTextureMode(target);
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawText("CANONICAL / INSPECTION", 24, 20, 28, Color.Lime);
        Raylib.DrawText($"Explicit state | seed 7 | FOV {fov} degrees", 24, 64, 20, Color.LightGray);
        CanonicalInspectionOverlay.DrawRuleSwitches(world, 24, 106);
        foreach (Boid boid in world.Boids) Raylib.DrawCircleV(new Vector2(boid.Position.X, boid.Position.Y), 3, Color.Lime);
        CanonicalInspectionOverlay.Draw(world, 0, neighbors, weights);
        Raylib.DrawText("O: Interactions | Tab: Next boid | 8: Field of view | H: Help", 24, 1040, 20, Color.LightGray);
        if (help && Tap(KeyboardKey.H).IsActive(RendererControlId.Help))
            RenderProgram.DrawHelpOverlay(RendererMode.Canonical, SpeedModel.ConstantSpeed);
        Raylib.EndTextureMode();
        Raylib.EndDrawing();
        Image image = Raylib.LoadImageFromTexture(target.Texture);
        try
        {
            Raylib.ImageFlipVertical(ref image);
            ArtifactCheck("PNG exported", Raylib.ExportImage(image, Path.Combine(output, name + ".png")));
            ArtifactCheck("capture is 1920x1080", image.Width == 1920 && image.Height == 1080);
            if (help)
                Check("help panel drawn", Near(image, 510, 40, Color.SkyBlue));
            else
            {
                Check("front FOV arc drawn", Near(image, (int)center.X + 240, 570, Color.Gold));
                Check("top FOV arc matches aperture", Near(image, (int)center.X, 330, Color.Gold) == (fov >= 180));
                // The seam scene's rear arc is outside the viewport, so it is not an aperture check.
                if (!seam) Check("rear FOV arc matches aperture", Near(image, (int)center.X - 240, 570, Color.Gold) == (fov == 360));
                Check("only the expected separation link segments are drawn", LinkPixelsMatch(image, new Color(255, 159, 67, 255),
                    ExpectedLinks(inputs, separation ? [1] : [], -3f)));
                Check("only the expected alignment link segments are drawn", LinkPixelsMatch(image, new Color(89, 200, 255, 255),
                    ExpectedLinks(inputs, alignment ? expectedVisible : [], 0f)));
                Check("only the expected cohesion link segments are drawn", LinkPixelsMatch(image, new Color(223, 164, 255, 255),
                    ExpectedLinks(inputs, cohesion ? expectedVisible : [], 3f)));
                Check("force pixels follow the resolved vector and stop at its tip", ForcePixelsMatch(image, center, force, settings.MaxForce));
            }
        }
        finally { Raylib.UnloadImage(image); }
    }
    finally { Raylib.UnloadRenderTexture(target); }
}

static void ResetConfig() => RenderProgram.ApplyBaseConfig(new SimConfig
{
    Seed = 7, WorldWidth = 1920, WorldHeight = 1080, MaxSpeed = 10, MaxForce = 20,
    SenseRadius = 240, SeparationRadius = 60, FieldOfView = 270, SeparationWeight = 1.5f,
    AlignmentWeight = 0.9f, CohesionWeight = 0.5f, WanderStrength = 0, WhiskerWeight = 0
});

static unsafe RendererInputSnapshot Tap(params KeyboardKey[] keys)
{
    // raylib 5.5 rcore.c AutomationEventType: INPUT_KEY_UP=1, INPUT_KEY_DOWN=2.
    foreach (KeyboardKey key in keys)
    {
        var press = new AutomationEvent { Type = 2 };
        press.Params[0] = (int)key;
        Raylib.PlayAutomationEvent(press);
    }
    RendererInputSnapshot input = RenderProgram.Controls.Capture(RendererMode.Canonical, RaylibRendererInput.Instance);
    foreach (KeyboardKey key in keys)
    {
        var release = new AutomationEvent { Type = 1 };
        release.Params[0] = (int)key;
        Raylib.PlayAutomationEvent(release);
    }
    return input;
}

static bool Near(Image image, int x, int y, Color color)
{
    for (int dy = -3; dy <= 3; dy++)
        for (int dx = -3; dx <= 3; dx++)
            if ((uint)(x + dx) < image.Width && (uint)(y + dy) < image.Height
                && Raylib.GetImageColor(image, x + dx, y + dy).Equals(color)) return true;
    return false;
}

static List<ProbeSegment> ExpectedLinks(Vector2[] inputs, int[] neighbors, float lane)
{
    var segments = new List<ProbeSegment>();
    foreach (int neighbor in neighbors)
    {
        Vector2 delta = inputs[neighbor] - inputs[0];
        Vector2 offset = Vector2.Normalize(new(-delta.Y, delta.X)) * lane;
        segments.Add(new(inputs[0] + offset, inputs[neighbor] + offset));
        if (inputs[neighbor].X < 0)
            segments.Add(new(inputs[neighbor] + offset + new Vector2(1920, 0), inputs[0] + offset + new Vector2(1920, 0)));
    }
    return segments;
}

static bool LinkPixelsMatch(Image image, Color color, List<ProbeSegment> segments)
{
    // Require a sample on every expected segment, including both clipped halves of a seam link.
    // Several interior samples allow for occlusion by the selected marker or force arrow.
    foreach (ProbeSegment segment in segments)
    {
        bool found = false;
        foreach (float fraction in new[] { 0.25f, 0.5f, 0.75f })
        {
            Vector2 sample = Vector2.Lerp(segment.Start, segment.End, fraction);
            if (sample.X >= 0 && sample.X < image.Width && Near(image, (int)MathF.Round(sample.X), (int)MathF.Round(sample.Y), color))
                found = true;
        }
        if (!found) return false;
    }

    // Reject extra links even when the same rule also has a legitimate link. Exclude text/legend
    // rows, but include the full viewport width so wraparound lines cannot cross the open middle.
    for (int y = 300; y < 900; y++)
        for (int x = 0; x < image.Width; x++)
            if (Raylib.GetImageColor(image, x, y).Equals(color)
                && !segments.Any(segment => DistanceSquared(new Vector2(x, y), segment) <= 6.25f)) return false;
    return true;
}

static float DistanceSquared(Vector2 point, ProbeSegment segment)
{
    Vector2 delta = segment.End - segment.Start;
    float fraction = Math.Clamp(Vector2.Dot(point - segment.Start, delta) / delta.LengthSquared(), 0f, 1f);
    return Vector2.DistanceSquared(point, segment.Start + delta * fraction);
}

static bool ForcePixelsMatch(Image image, Vector2 center, Vec2 force, float maxForce)
{
    Vector2 offset = new Vector2(force.X, force.Y) * (64f / maxForce);
    float length = MathF.Min(offset.Length(), 128f);
    Vector2 direction = length > 0 ? Vector2.Normalize(offset) : Vector2.Zero;
    Vector2 tip = center + direction * length;
    int pixels = 0;
    for (int y = 300; y < 900; y++)
        for (int x = 0; x < image.Width; x++)
        {
            if (!Raylib.GetImageColor(image, x, y).Equals(Color.White)) continue;
            pixels++;
            Vector2 delta = new Vector2(x, y) - center;
            float along = Vector2.Dot(delta, direction);
            float across = MathF.Abs(delta.X * direction.Y - delta.Y * direction.X);
            if (along < -2f || along > length + 2f || across > MathF.Min(4f, length * 0.2f) + 2f) return false;
        }
    return length < 0.1f ? pixels == 0 : pixels > 0 && Near(image, (int)MathF.Round(tip.X), (int)MathF.Round(tip.Y), Color.White);
}

internal readonly record struct ProbeSegment(Vector2 Start, Vector2 End);
internal sealed record ProbeCheck(string Name, bool Passed, string? Detail = null);
internal sealed record ProbeCase(string Name, int FieldOfView, int VisibleInputs, float Separation, float Alignment,
    float Cohesion, float ForceX, float ForceY, List<ProbeCheck> Checks, List<ProbeCheck> ArtifactChecks);
