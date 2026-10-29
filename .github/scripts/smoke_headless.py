"""Exercise built, display-free CLI artifacts and replay on each CI platform."""

import csv
import hashlib
import io
import json
import math
import os
from pathlib import Path
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
CLI = ROOT / "SwarmSim.Cli/bin/Release/net8.0/SwarmSim.Cli.dll"
DOTNET = os.environ.get("DOTNET_EXE", "dotnet")
TICKS = 600
AGENTS = 400


def invoke(arguments, output):
    environment = os.environ.copy()
    environment.pop("DISPLAY", None)
    environment.pop("WAYLAND_DISPLAY", None)
    completed = subprocess.run(
        [DOTNET, str(CLI), *arguments, "--out", str(output)],
        cwd=ROOT,
        env=environment,
        check=True,
        capture_output=True,
        text=True,
        timeout=120,
    )
    assert not completed.stderr, completed.stderr
    directories = list(output.iterdir())
    assert len(directories) == 1 and directories[0].is_dir(), directories
    run = directories[0]
    # Windows can spell the same temp directory with a short-name alias. Check the
    # reported directory's filesystem identity, not its spelling.
    assert Path(completed.stdout.strip()).samefile(run), (completed.stdout, str(run))
    assert sorted(path.name for path in run.iterdir()) == [
        "log.txt", "manifest.json", "metrics.csv"
    ]
    return run


def main():
    dependencies = json.loads(CLI.with_suffix(".deps.json").read_text(encoding="utf-8"))
    names = list(dependencies["libraries"])
    assert any(name.startswith("SwarmSim.Core/") for name in names), names
    assert not any("raylib" in name.lower() or "SwarmSim.Render" in name for name in names), names
    print("CLI dependencies:", ", ".join(names), flush=True)
    expected_commit = None
    if os.environ.get("GITHUB_ACTIONS") == "true":
        expected_commit = subprocess.run(
            ["git", "rev-parse", "HEAD"], cwd=ROOT, check=True,
            capture_output=True, text=True, timeout=10,
        ).stdout.strip()

    with tempfile.TemporaryDirectory(prefix="swarm-headless-ci-") as temporary:
        parent = Path(temporary)
        for engine in ("canonical", "legacy"):
            config = "canonical-balanced.json" if engine == "canonical" else "balanced.json"
            arguments = [
                "--engine", engine, "--config", str(ROOT / "configs" / config),
                "--seed", "7", "--ticks", str(TICKS), "--agents", str(AGENTS),
            ]
            original = invoke(arguments, parent / (engine + "-original"))
            repeated = invoke(arguments, parent / (engine + "-repeat"))
            replay = invoke(["--manifest", str(original / "manifest.json")], parent / (engine + "-replay"))
            metrics = (original / "metrics.csv").read_bytes()
            assert metrics == (repeated / "metrics.csv").read_bytes()
            assert metrics == (replay / "metrics.csv").read_bytes()
            assert not metrics.startswith(b"\xef\xbb\xbf") and b"\r" not in metrics
            rows = list(csv.reader(io.StringIO(metrics.decode("utf-8"))))
            assert rows[0] == ["tick", "population", "meanSpeed", "polarization"]
            assert len(rows) == TICKS + 2, len(rows)
            for tick, row in enumerate(rows[1:]):
                assert len(row) == 4 and int(row[0]) == tick and int(row[1]) == AGENTS, row
                assert math.isfinite(float(row[2])) and float(row[2]) >= 0, row
                assert 0 <= float(row[3]) <= 1, row

            manifest = json.loads((original / "manifest.json").read_text(encoding="utf-8"))
            assert manifest["engine"] == engine and manifest["ticks"] == TICKS
            assert manifest["metricsSha256"] == hashlib.sha256(metrics).hexdigest().upper()
            assert manifest["dirtyWorktree"] is None
            if expected_commit is not None:
                assert manifest["gitCommit"] == expected_commit, manifest["gitCommit"]
            for other in (repeated, replay):
                other_manifest = json.loads((other / "manifest.json").read_text(encoding="utf-8"))
                assert other_manifest["finalKinematicHash"] == manifest["finalKinematicHash"]
            print(
                f"HEADLESS_SMOKE engine={engine} ticks={TICKS} agents={AGENTS} "
                f"csvLines={len(rows)} state={manifest['finalKinematicHash']} "
                f"csv={manifest['metricsSha256']} repeat=identical replay=identical "
                f"gitCommit={manifest['gitCommit']} dirtyWorktree={manifest['dirtyWorktree']}",
                flush=True,
            )


if __name__ == "__main__":
    main()
