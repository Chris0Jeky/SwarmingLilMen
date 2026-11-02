"""Run one full parity matrix; never record or replace the locked calibration."""
import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import subprocess
import sys
import time


def validate_result(mode, code, console, actual_rows, expected_rows, before, after):
    if before != after:
        raise ValueError("The locked baseline changed during verification.")
    expected_code = 1 if mode == "alignment-off" else 0
    if code != expected_code:
        raise ValueError(f"{mode} returned {code}; expected {expected_code}.")
    if mode == "alignment-off" and " polarization:" not in console:
        raise ValueError("Alignment-off did not establish a polarization rejection.")
    def identities(rows):
        return Counter((row["Scenario"], row["Seed"], row["Model"]) for row in rows)
    if identities(actual_rows) != identities(expected_rows):
        raise ValueError("The result does not contain the complete locked scenario/seed/model matrix.")


def validate_timing(mode, timing):
    counts = ("PrimaryRuns", "RepeatRuns", "NaiveIndexRuns")
    durations = ("PrimarySeconds", "RepeatSeconds", "NaiveIndexSeconds")
    required = ("Version", "MatrixScope", "MatrixSeconds") + counts + durations
    if not isinstance(timing, dict) or any(key not in timing for key in required):
        raise ValueError("The matrix timing record is missing required fields.")
    if type(timing["Version"]) is not int or timing["Version"] != 1:
        raise ValueError("Unsupported matrix timing record version.")
    if not isinstance(timing["MatrixScope"], str) or not timing["MatrixScope"].strip():
        raise ValueError("The matrix timing record must define its measured scope.")
    expected = {"verify": (100, 100, 50), "alignment-off": (100, 0, 0)}.get(mode)
    if expected is None:
        raise ValueError("Unknown scientific verification mode.")
    for key, count in zip(counts, expected):
        if type(timing[key]) is not int or timing[key] != count:
            raise ValueError(f"{key} must record exactly {count} completed runs.")
    for key in durations + ("MatrixSeconds",):
        value = timing[key]
        try:
            finite = math.isfinite(value)
        except (TypeError, OverflowError):
            finite = False
        if type(value) not in (int, float) or not finite or value < 0:
            raise ValueError(f"{key} must be a finite nonnegative duration.")
    for count, key in zip(expected, durations):
        if count == 0 and timing[key] != 0:
            raise ValueError(f"{key} records time for a phase that did not run.")
    # The disjoint Run calls are inside the matrix clock. Allow only rounding noise.
    tolerance = max(1e-6, timing["MatrixSeconds"] * 1e-9)
    if sum(timing[key] for key in durations) > timing["MatrixSeconds"] + tolerance:
        raise ValueError("Phase durations exceed the containing matrix interval.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("verify", "alignment-off"), required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--baseline", type=Path, default=Path("baselines/parity-v1.json"))
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    baseline_bytes = args.baseline.read_bytes()
    before = hashlib.sha256(baseline_bytes).hexdigest()
    expected_rows = json.loads(baseline_bytes)["Calibration"]
    args.out.mkdir(parents=True, exist_ok=False)
    run_directory = args.out / "matrix"
    command = [args.dotnet, "run", "--project", "SwarmSim.Parity", "-c", "Release", "--no-build",
               "--", "verify", "--baseline", str(args.baseline), "--out", str(run_directory)]
    if args.mode == "alignment-off":
        command.append("--alignment-off")
    receipt = {"mode": args.mode, "baselineSha256": before, "command": command, "passed": False}
    start = time.monotonic()
    try:
        receipt["sourceSha"] = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], text=True).strip()
        lines = []
        with (args.out / "console.log").open("w", encoding="utf-8", newline="\n") as log:
            with subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                  text=True, encoding="utf-8", errors="replace") as process:
                for line in process.stdout:
                    print(line, end="", flush=True)
                    log.write(line)
                    log.flush()
                    lines.append(line)
                code = process.wait()
        receipt["exitCode"] = code
        after = hashlib.sha256(args.baseline.read_bytes()).hexdigest()
        rows = json.loads((run_directory / "summary.json").read_text(encoding="utf-8"))
        validate_result(args.mode, code, "".join(lines), rows, expected_rows, before, after)
        timing = json.loads((run_directory / "timing.json").read_text(encoding="utf-8"))
        validate_timing(args.mode, timing)
        receipt["timing"] = timing
        receipt["primaryRuns"] = len(rows)
        receipt["passed"] = True
        print("Scientific timing: " + json.dumps(timing, sort_keys=True))
        print(f"Scientific {args.mode} gate passed for {len(rows)} primary runs.")
        return 0
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        receipt["error"] = str(error)
        print(f"Scientific parity failed: {error}", file=sys.stderr)
        return 1
    finally:
        receipt["elapsedSeconds"] = time.monotonic() - start
        (args.out / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    sys.exit(main())
