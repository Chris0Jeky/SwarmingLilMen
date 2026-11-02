"""Collect auditable CompositionCost evidence without qualifying performance."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from collections import Counter


def file_identity(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return {"sha256": digest.hexdigest(), "bytes": path.stat().st_size}


def source_manifest(root, output):
    root, output = root.resolve(), output.resolve()
    files = {}
    for directory, children, names in os.walk(root):
        directory = Path(directory)
        children[:] = sorted(
            name for name in children
            if name not in {"bin", "obj", "__pycache__", ".git"}
            and not (directory == root and name in {"artifacts", "BenchmarkDotNet.Artifacts"})
            and (directory / name).resolve() != output
        )
        for name in children:
            require(not (directory / name).is_symlink(), f"Source directory symlink is unsupported: {directory / name}")
        for name in sorted(names):
            path = directory / name
            if name == ".git":
                continue
            files[path.relative_to(root).as_posix()] = file_identity(path)
    encoded = json.dumps(files, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return {"files": files, "sha256": hashlib.sha256(encoded).hexdigest()}


def prepare_output(output):
    output.mkdir(parents=True, exist_ok=False)


def validate_results(report, console, agents, smoke, invocations):
    """Validate BDN 0.15.6 evidence, including bytes absent from its JSON exporter."""
    cases, errors = [], []
    if not isinstance(report, dict):
        return [], ["Full JSON report is not an object."]
    headers = list(re.finditer(r"^// Benchmark: (.+)$", console, re.MULTILINE))
    sections = {}
    for index, header in enumerate(headers):
        end = headers[index + 1].start() if index + 1 < len(headers) else len(console)
        sections.setdefault(header.group(1).strip(), []).append(console[header.end():end])
    benchmarks = report.get("Benchmarks", [])
    if not isinstance(benchmarks, list) or not benchmarks:
        return [], ["No benchmark results were exported."]
    host = report.get("HostEnvironmentInfo")
    if (not isinstance(host, dict)
            or any(not isinstance(host.get(key), str) or not host[key].strip()
                   for key in ("BenchmarkDotNetVersion", "RuntimeVersion", "OsVersion"))
            or host["BenchmarkDotNetVersion"] != "0.15.6"):
        errors.append("BDN 0.15.6, runtime or OS metadata is missing or incompatible.")
    populations = []
    for benchmark in benchmarks:
        try:
            parameter = re.fullmatch(r"AgentCount=(\d+)", benchmark["Parameters"])
            require(parameter is not None, "Unexpected benchmark parameters.")
            population = int(parameter.group(1))
            populations.append(population)
            cases.append(validate_case(benchmark, sections, population, smoke, invocations))
        except (KeyError, TypeError, ValueError) as error:
            errors.append(f"Incomplete benchmark: {error}")
    if Counter(populations) != Counter(agents):
        errors.append(f"Expected each population exactly once: {list(agents)}; found {populations}.")
    if Counter(header.group(1).strip() for header in headers) != Counter(
        benchmark.get("DisplayInfo") for benchmark in benchmarks if isinstance(benchmark, dict)
    ):
        errors.append("Raw log cases do not match exported cases exactly once.")
    return cases, errors


def require(condition, message):
    if not condition:
        raise ValueError(message)


def job_settings(smoke, invocations):
    return {
        "Runtime": ".NET 8.0", "InvocationCount": str(invocations),
        "IterationCount": "1" if smoke else "5", "LaunchCount": "1",
        "UnrollFactor": "1", "WarmupCount": "0" if smoke else "3",
        "RunStrategy": "ColdStart" if smoke else "Throughput",
    }


def validate_case(benchmark, sections, population, smoke, invocations):
    require(benchmark["Type"] == "CompositionCostBenchmarks" and benchmark["Method"] == "Step",
            "Unexpected benchmark workload.")
    display = benchmark["DisplayInfo"]
    full_name = f"CompositionCostBenchmarks.Step(AgentCount: {population})"
    require(benchmark["FullName"] == full_name and display.endswith(f"[AgentCount={population}]"),
            "Population differs between parameter, display and full-name records.")
    for key, value in job_settings(smoke, invocations).items():
        require(re.search(rf"\b{key}={re.escape(value)}(?=,|\))", display), f"Wrong or missing job setting {key}={value}.")
    require(isinstance(benchmark["Statistics"], dict) and benchmark["Statistics"], "Benchmark statistics are missing.")
    require(len(sections.get(display, [])) == 1, "Missing or duplicate raw log case.")
    section = sections[display][0]

    def one(pattern, description):
        matches = re.findall(pattern, section, re.MULTILINE)
        require(len(matches) == 1, f"Expected one {description}; found {len(matches)}.")
        return matches[0]

    one(r"^// BenchmarkDotNet v0\.15\.6\s*$", "BDN 0.15.6 version record")
    runtime = one(r"^// Runtime=(.+)$", "child runtime record").strip()
    require(re.match(r"\.NET 8\.0(?:[. ,]|$)", runtime), "Child runtime differs from requested .NET 8.0.")
    gc = one(r"^// GC=(.+)$", "child GC configuration").strip()
    execution = one(r"^// Execute: (.+)$", "child execution record").strip()
    executed_name = re.search(r' --benchmarkName (?:"([^"]+)"|(\S+)) --job ', execution)
    require(executed_name is not None and (executed_name.group(1) or executed_name.group(2)) == full_name,
            "Executed benchmark name differs from exported case.")
    directory = re.search(r" --benchmarkId \d+ in (.+)$", execution)
    require(directory is not None, "Cannot identify the executed child directory.")
    child_exit = one(r"^// Benchmark Process \d+ has exited with code (-?\d+)\.\s*$", "child exit record")
    require(child_exit == "0", f"Benchmark child exited with code {child_exit}.")

    iterations = 1 if smoke else 5
    measurements = [row for row in benchmark["Measurements"]
                    if row["IterationMode"] == "Workload" and row["IterationStage"] == "Actual"]
    require(len(measurements) == iterations, "Missing or duplicate Workload/Actual measurements.")
    require({(row["LaunchIndex"], row["IterationIndex"]) for row in measurements}
            == {(1, index) for index in range(1, iterations + 1)}, "Unexpected measurement indices.")
    raw_measurements = re.findall(r"^WorkloadActual\s+(\d+):\s+(\d+) op,\s+([\d.,eE+-]+) ns,", section, re.MULTILINE)
    require(len(raw_measurements) == iterations, "Missing or duplicate raw WorkloadActual records.")
    raw_by_index = {int(index): (int(operations), float(nanoseconds.replace(",", "")))
                    for index, operations, nanoseconds in raw_measurements}
    require(len(raw_by_index) == iterations, "Duplicate raw measurement indices.")
    for row in measurements:
        require(type(row["Operations"]) is int and row["Operations"] == invocations,
                "Workload operation count differs from the requested job.")
        nanoseconds = row["Nanoseconds"]
        require(isinstance(nanoseconds, (int, float)) and not isinstance(nanoseconds, bool)
                and math.isfinite(nanoseconds) and nanoseconds >= 0, "Invalid measurement duration.")
        raw_operations, raw_nanoseconds = raw_by_index.get(row["IterationIndex"], (None, math.nan))
        require(raw_operations == row["Operations"] and abs(raw_nanoseconds - nanoseconds) <= 0.011,
                "Raw and JSON workload measurements disagree.")

    raw_line = one(r"^// GC:\s*(.*)$", "raw allocation record").strip()
    raw = re.fullmatch(r"(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)", raw_line)
    require(raw is not None, "Raw allocated bytes or operation count is unavailable.")
    gen0, gen1, gen2, allocated, operations = map(int, raw.groups())
    require(max(gen0, gen1, gen2) <= 2**31 - 1 and allocated <= 2**63 - 1
            and 0 < operations <= 2**63 - 1, "Raw allocation counts exceed BDN's integer ranges.")
    memory = benchmark["Memory"]
    # This Step has one operation per invocation, including BDN's separate allocation interval.
    require(type(memory["TotalOperations"]) is int and memory["TotalOperations"] == operations == invocations,
            "Allocation operation count differs from the requested job or raw record.")
    collections = (memory["Gen0Collections"], memory["Gen1Collections"], memory["Gen2Collections"])
    require(all(type(value) is int for value in collections) and collections == (gen0, gen1, gen2),
            "Raw and JSON GC collections disagree.")
    per_operation = memory["BytesAllocatedPerOperation"]
    require(type(per_operation) is int and 0 <= per_operation <= 2**63 - 1,
            "JSON allocation data is not a nonnegative Int64.")
    # BDN 0.15.6 applies no allocation-quantum exclusion to this forced .NET 8 job.
    # Reproduce its double division and MidpointRounding.ToEven, including inputs above 2**53.
    require(per_operation == round(float(allocated) / float(operations)),
            "Raw and JSON allocation data disagree with BDN's .NET 8 rounding.")
    # Keep the raw total: multiplying the rounded value by operations cannot recover it.
    return {
        "population": population, "display": display, "runtime": runtime, "gc": gc,
        "measurement_count": len(measurements), "memory": memory,
        "raw_gc": {"gen0": gen0, "gen1": gen1, "gen2": gen2, "allocated_bytes": allocated, "operations": operations},
        "execution": execution, "binary_directory": directory.group(1).strip(),
    }


def benchmark_command(dotnet, project, output, agents, smoke, invocations):
    return [
        dotnet, "run", "--project", str(project), "-c", "Release", "--no-build", "--",
        "--filter", *(f"*AgentCount: {count})" for count in agents),
        "--runtimes", "net8.0", "--cli", dotnet,
        "--warmupCount", "0" if smoke else "3", "--iterationCount", "1" if smoke else "5",
        "--launchCount", "1", "--invocationCount", str(invocations), "--unrollFactor", "1",
        "--strategy", "ColdStart" if smoke else "Throughput",
        "--exporters", "fulljson", "csvmeasurements", "--keepFiles", "--stopOnFirstError",
        "--artifacts", str(output / "bdn"),
        *(["--job", "Dry"] if smoke else []),
    ]


def run_logged(command, root, log):
    with log.open("wb") as destination, subprocess.Popen(
        command, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
    ) as process:
        try:
            for chunk in iter(lambda: process.stdout.read1(65536), b""):
                destination.write(chunk)
                destination.flush()
                sys.stdout.write(chunk.decode("utf-8", errors="replace"))
                sys.stdout.flush()
            return process.wait()
        except BaseException:
            process.terminate()
            raise


def binary_manifest(cases, root):
    builds = {}
    for case in cases:
        directory = Path(case["binary_directory"]).resolve()
        require(directory.is_relative_to((root / "tools" / "CompositionCost" / "bin").resolve()),
                f"Unexpected child binary directory: {directory}")
        executable = re.search(r'(?:"([^"/\\]+\.dll)"|([^\s"/\\]+\.dll)) --anonymousPipes\b', case["execution"])
        require(executable is not None, "Cannot identify the executed generated DLL.")
        generated = executable.group(1) or executable.group(2)
        for name in (generated, Path(generated).stem + ".deps.json", Path(generated).stem + ".runtimeconfig.json",
                     "SwarmSim.Core.dll", "SwarmSim.Benchmarks.dll", "CompositionCost.dll"):
            require((directory / name).is_file(), f"Missing executed build file: {directory / name}")
        if str(directory) not in builds:
            builds[str(directory)] = {
                "directory": str(directory), "generated_dll": generated,
                "files": {path.relative_to(directory).as_posix(): file_identity(path)
                          for path in sorted(directory.rglob("*")) if path.is_file()},
            }
    require(builds, "No executed build could be identified.")
    return list(builds.values())


def artifact_manifest(output):
    return {path.relative_to(output).as_posix(): file_identity(path)
            for path in sorted(output.rglob("*")) if path.is_file() and path != output / "receipt.json"}


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8")


def collect_evidence(root, options):
    root, output = root.resolve(), options.out.resolve()
    prepare_output(output)
    receipt = {
        "schema_version": 1, "purpose": "smoke" if options.smoke else "measurement",
        "evidence_complete": False, "performance_verdict": "not_evaluated",
        "started_utc": datetime.now(timezone.utc).isoformat(), "working_directory": str(root),
        "driver_arguments": options.arguments, "requested_agents": options.agents,
        "requested_job": job_settings(options.smoke, options.invocations),
        "commands": [], "errors": ["Collection has not finished."],
        "runtime_environment_overrides": {key: os.environ[key] for key in (
            "DOTNET_ROOT", "DOTNET_ROLL_FORWARD", "DOTNET_MULTILEVEL_LOOKUP", "DOTNET_PROCESSOR_COUNT",
            "DOTNET_gcServer", "DOTNET_gcConcurrent", "DOTNET_GCHeapCount", "DOTNET_GCHeapHardLimit",
            "DOTNET_GCHeapHardLimitPercent", "DOTNET_TieredCompilation", "DOTNET_TieredPGO", "DOTNET_ReadyToRun",
            "COMPlus_gcServer", "COMPlus_gcConcurrent", "COMPlus_GCHeapCount", "COMPlus_GCHeapHardLimit",
            "COMPlus_GCHeapHardLimitPercent", "COMPlus_TieredCompilation", "COMPlus_TieredPGO", "COMPlus_ReadyToRun",
        ) if key in os.environ},
    }
    write_json(output / "receipt.json", receipt)
    errors, before, validated = [], None, False
    try:
        before = source_manifest(root, output)
        write_json(output / "source-before.json", before)
        receipt["source_before"] = before["sha256"]
        project = root / "tools" / "CompositionCost" / "CompositionCost.csproj"
        commands = [
            ([options.dotnet, "--info"], "dotnet-info.log"),
            ([options.dotnet, "--version"], "dotnet-version.log"),
            ([options.dotnet, "build", str(project), "-c", "Release"], "build.log"),
            (benchmark_command(options.dotnet, project, output, options.agents, options.smoke, options.invocations), "console.log"),
        ]
        for command, log in commands:
            record = {"arguments": command, "working_directory": str(root), "log": log, "exit_code": None}
            receipt["commands"].append(record)
            record["exit_code"] = run_logged(command, root, output / log)
            require(record["exit_code"] == 0, f"Command for {log} exited with code {record['exit_code']}.")
            if log == "dotnet-version.log":
                version = (output / log).read_text(encoding="utf-8-sig").strip()
                require(re.fullmatch(r"\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?", version),
                        "Selected SDK version is missing or malformed.")
                receipt["selected_sdk_version"] = version
        reports = list((output / "bdn" / "results").glob("*-report-full.json"))
        require(len(reports) == 1, f"Expected one full JSON report; found {len(reports)}.")
        report = json.loads(reports[0].read_text(encoding="utf-8-sig"))
        cases, result_errors = validate_results(report, (output / "console.log").read_text(encoding="utf-8-sig"),
                                               options.agents, options.smoke, options.invocations)
        errors.extend(result_errors)
        receipt["cases"] = cases
        receipt["host_environment"] = report.get("HostEnvironmentInfo")
        receipt["built_binaries"] = binary_manifest(cases, root)
        validated = True
    except (Exception, KeyboardInterrupt) as error:
        errors.append(f"{type(error).__name__}: {error}")
    finally:
        try:
            after = source_manifest(root, output)
            write_json(output / "source-after.json", after)
            receipt["source_after"] = after["sha256"]
            if before is not None and before["sha256"] != after["sha256"]:
                receipt["changed_source_files"] = sorted(
                    name for name in before["files"].keys() | after["files"].keys()
                    if before["files"].get(name) != after["files"].get(name)
                )
                errors.append("Source content changed during collection.")
            receipt["artifacts"] = artifact_manifest(output)
        except Exception as error:
            errors.append(f"Could not finish evidence hashes: {error}")
        if not validated and not errors:
            errors.append("Collection did not finish validation.")
        receipt["finished_utc"] = datetime.now(timezone.utc).isoformat()
        receipt["errors"] = errors
        receipt["evidence_complete"] = validated and not errors
        write_json(output / "receipt.json", receipt)
    print(f"Evidence {'complete' if not errors else 'incomplete'}: {output / 'receipt.json'}")
    print("Performance qualification was not evaluated." if not options.smoke else "Smoke only: timing and allocation are not performance qualification.")
    for error in errors:
        print(f"  {error}")
    return 1 if errors else 0


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True, help="New evidence directory; an existing path is rejected.")
    parser.add_argument("--agents", type=int, nargs="+", choices=(1000, 10000, 50000), required=True)
    parser.add_argument("--dotnet", default="dotnet", help="SDK executable used for both builds and the benchmark child.")
    parser.add_argument("--invocations", type=int, help="Fixed invocations per measurement iteration (default: 64).")
    parser.add_argument("--smoke", action="store_true", help="One 1k Dry invocation to test artifact capture only.")
    arguments = list(sys.argv[1:] if arguments is None else arguments)
    options = parser.parse_args(arguments)
    if len(options.agents) != len(set(options.agents)):
        parser.error("Each population must be requested once.")
    if options.smoke and (options.agents != [1000] or options.invocations is not None):
        parser.error("--smoke requires --agents 1000 and does not accept --invocations.")
    options.invocations = 1 if options.smoke else (64 if options.invocations is None else options.invocations)
    if options.invocations < 1:
        parser.error("--invocations must be positive.")
    executable = shutil.which(options.dotnet)
    if executable is None:
        parser.error(f"SDK executable not found: {options.dotnet}")
    options.dotnet, options.arguments = str(Path(executable).resolve()), arguments
    try:
        return collect_evidence(Path(__file__).resolve().parents[2], options)
    except FileExistsError:
        print(f"Output already exists and was not changed: {options.out}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
