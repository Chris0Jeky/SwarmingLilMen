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
    Capture("help", 270, help: true);
}
catch (Exception ex) { checks.Add(new("native execution", false, ex.Message)); }
finally
{
    Console.SetOut(console);
    if (windowReady) Raylib.CloseWindow();
}

bool passed = checks.All(c => c.Passed) && cases.Count == 8 && cases.All(c => c.Checks.All(check => check.Passed));
var report = new
{
    Passed = passed,
    ElapsedSeconds = elapsed.Elapsed.TotalSeconds,
    Runtime = RuntimeInformation.FrameworkDescription,
    Platform = RuntimeInformation.OSDescription,
    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    Input = "Raylib automation events -> native input adapter -> production controls",
    Capture = "1920x1080 native render target; production overlay/help; explicit six-boid scene",
    NotVerified = "OS keyboard/focus, full-window DPI, human acceptance, performance throughput",
    Assemblies = new[] { typeof(RenderProgram).Assembly, typeof(CanonicalWorld).Assembly, Assembly.GetExecutingAssembly() }
        .Select(a => new { Name = a.GetName().Name, Version = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }),
    Checks = checks,
    Cases = cases
};
File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
string Encode(string text) => WebUtility.HtmlEncode(text);
string CheckList(IEnumerable<ProbeCheck> items) => string.Join("", items.Select(c => $"<li class='{(c.Passed ? "pass" : "fail")}'>{(c.Passed ? "PASS" : "FAIL")} {Encode(c.Name)}{(c.Detail is null ? "" : ": " + Encode(c.Detail))}</li>"));
string cards = string.Join("", cases.Select(c => $"<article><h2>{Encode(c.Name)}</h2><a href='{c.Name}.png'><img src='{c.Name}.png' alt='{Encode(c.Name)} canonical inspection capture'></a><ul>{CheckList(c.Checks)}</ul></article>"));
File.WriteAllText(Path.Combine(output, "index.html"), $$"""
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Canonical renderer probe — {{(passed ? "PASS" : "FAIL")}}</title>
<style>body{margin:2rem;background:#11151d;color:#e7ebf3;font:16px system-ui}a{color:#8dceff}h1{margin-bottom:.4rem}p{max-width:85ch}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,600px),1fr));gap:1.5rem}article{border:1px solid #465266;padding:1rem}img{width:100%;height:auto}li{margin:.3rem 0}.pass{color:#a4deb6}.fail{color:#ffb7ae}</style>
<h1>{{(passed ? "PASS" : "FAIL")}} · Canonical renderer probe</h1>
<p>Native event playback and production overlay rendering in {{elapsed.Elapsed.TotalSeconds:F2}} seconds. Open an image at full size to inspect it.</p>
<p>Not verified: OS keyboard/focus, full-window DPI, human acceptance or throughput. <a href="report.json">Machine-readable report and build hashes</a>.</p>
<ul>{{CheckList(checks)}}</ul><main>{{cards}}</main></html>
""");
foreach (var check in checks.Concat(cases.SelectMany(c => c.Checks)))
    if (!check.Passed) Console.Error.WriteLine($"FAIL {check.Name}: {check.Detail}");
Console.WriteLine($"{(passed ? "PASS" : "FAIL")}: {cases.Count} captures, {checks.Count + cases.Sum(c => c.Checks.Count)} checks in {elapsed.Elapsed.TotalSeconds:F2}s\n{Path.Combine(output, "index.html")}");
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

void Capture(string name, int fov, KeyboardKey? toggle = null, bool closeNeighbor = false, bool help = false)
{
    ResetConfig();
    RenderProgram.ProcessCanonicalParameters(Tap(KeyboardKey.Eight));
    for (int degrees = 270; degrees != fov; degrees += fov < 270 ? -15 : 15)
        RenderProgram.ProcessCanonicalParameters(Tap(fov < 270 ? KeyboardKey.Down : KeyboardKey.Up));
    CanonicalWorldSettings settings = RenderProgram.BuildCanonicalWorldSettings();
    var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, 1920, 1080));
    world.TryAddBoid(new Vec2(920, 570), new Vec2(1, 0));
    world.TryAddBoid(new Vec2(closeNeighbor ? 950 : 1050, 600), new Vec2(0.9f, 0.15f));
    world.TryAddBoid(new Vec2(1010, 470), new Vec2(1, 0.2f));
    world.TryAddBoid(new Vec2(870, 440), new Vec2(1, -0.1f));
    world.TryAddBoid(new Vec2(730, 550), new Vec2(1, 0.1f));
    world.TryAddBoid(new Vec2(780, 700), new Vec2(0.9f, 0.2f));
    if (toggle is { } key) CanonicalInspectionOverlay.ApplyRuleSwitches(Tap(key), world);
    world.Step(settings.FixedDeltaTime);
    world.TryGetMetrics(0, out var metrics);
    Vec2 force = world.Instrumentation.SteeringVectors[0];
    var neighbors = new int[128];
    var weights = new float[128];
    int visible = world.QueryLastTickVisibleNeighbors(0, neighbors, weights).Count;
    var result = new ProbeCase(name, fov, visible, metrics.SeparationMagnitude, metrics.AlignmentMagnitude, metrics.CohesionMagnitude, force.X, force.Y, []);
    cases.Add(result);
    void Check(string description, bool condition, string? detail = null) => result.Checks.Add(new($"{name}: {description}", condition, detail));
    Check("native FOV control sets configuration", settings.FieldOfView == fov, $"expected {fov}, observed {settings.FieldOfView}");
    int expectedVisible = fov == 90 ? 1 : fov == 360 ? 5 : 3;
    Check("expected visible inputs", visible == expectedVisible, $"expected {expectedVisible}, observed {visible}");
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
            Check("PNG exported", Raylib.ExportImage(image, Path.Combine(output, name + ".png")));
            Check("capture is 1920x1080", image.Width == 1920 && image.Height == 1080);
            if (help)
                Check("help panel drawn", Near(image, 510, 40, Color.SkyBlue));
            else
            {
                Check("front FOV arc drawn", Near(image, 1160, 570, Color.Gold));
                Check("top FOV arc matches aperture", Near(image, 920, 330, Color.Gold) == (fov >= 180));
                Check("rear FOV arc matches aperture", Near(image, 680, 570, Color.Gold) == (fov == 360));
                Check("separation links match applied state", HasColor(image, new Color(255, 159, 67, 255)) == separation);
                Check("alignment links match applied state", HasColor(image, new Color(89, 200, 255, 255)) == alignment);
                Check("cohesion links match applied state", HasColor(image, new Color(223, 164, 255, 255)) == cohesion);
                Check("force tip drawn at resolved vector", Near(image, (int)MathF.Round(920 + force.X / 20 * 64), (int)MathF.Round(570 + force.Y / 20 * 64), Color.White));
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
            if (Raylib.GetImageColor(image, x + dx, y + dy).Equals(color)) return true;
    return false;
}

static bool HasColor(Image image, Color color)
{
    // Scene only: exclude the HUD/legend, which use the same colors even for disabled rules.
    for (int y = 360; y < 820; y++)
        for (int x = 650; x < 1200; x++)
            if (Raylib.GetImageColor(image, x, y).Equals(color)) return true;
    return false;
}

internal sealed record ProbeCheck(string Name, bool Passed, string? Detail = null);
internal sealed record ProbeCase(string Name, int FieldOfView, int VisibleInputs, float Separation, float Alignment,
    float Cohesion, float ForceX, float ForceY, List<ProbeCheck> Checks);
