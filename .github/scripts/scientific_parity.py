"""Run one full parity matrix; never record or replace the locked calibration."""
import argparse
from collections import Counter
import hashlib
import json
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
        receipt["primaryRuns"] = len(rows)
        receipt["passed"] = True
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
