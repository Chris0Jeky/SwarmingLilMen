"""Hand-authored fixtures test evidence completeness, not benchmark performance."""

import copy
import contextlib
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch

import collect


DISPLAY = (
    "CompositionCostBenchmarks.Step: Job-fixture(Runtime=.NET 8.0, InvocationCount=64, "
    "IterationCount=5, LaunchCount=1, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [AgentCount=1000]"
)
REPORT = {
    "Title": "Synthetic contract fixture; not a measured result",
    "HostEnvironmentInfo": {"BenchmarkDotNetVersion": "0.15.6", "RuntimeVersion": ".NET 8.0.33",
                            "DotNetCliVersion": "8.0.427", "OsVersion": "Synthetic OS"},
    "Benchmarks": [{
        "DisplayInfo": DISPLAY, "Namespace": None, "Type": "CompositionCostBenchmarks",
        "Method": "Step", "MethodTitle": "Existing canonical Step, telemetry capture off",
        "Parameters": "AgentCount=1000", "FullName": "CompositionCostBenchmarks.Step(AgentCount: 1000)",
        "HardwareIntrinsics": "fixture", "Statistics": {"Mean": 15625},
        "Memory": {"Gen0Collections": 0, "Gen1Collections": 0, "Gen2Collections": 0,
                   "TotalOperations": 64, "BytesAllocatedPerOperation": 5.25},
        "Measurements": [
            {"IterationMode": "Workload", "IterationStage": "Actual", "LaunchIndex": 1,
             "IterationIndex": index, "Operations": 64, "Nanoseconds": 1000000}
            for index in range(1, 6)
        ],
        "Metrics": [],
    }],
}
CONSOLE = f"""// Benchmark: {DISPLAY}
// Execute: /fixture/dotnet generated.dll --anonymousPipes 1 2 --benchmarkName "CompositionCostBenchmarks.Step(AgentCount: 1000)" --job fixture --benchmarkId 0 in /fixture in progress/child
// BenchmarkDotNet v0.15.6
// Runtime=.NET 8.0.33 (64bit RyuJIT)
// GC=Concurrent Workstation
WorkloadActual   1: 64 op, 1000000.00 ns, 15625.00 ns/op
WorkloadActual   2: 64 op, 1000000.00 ns, 15625.00 ns/op
WorkloadActual   3: 64 op, 1000000.00 ns, 15625.00 ns/op
WorkloadActual   4: 64 op, 1000000.00 ns, 15625.00 ns/op
WorkloadActual   5: 64 op, 1000000.00 ns, 15625.00 ns/op
// GC:  0 0 0 336 64
// Benchmark Process 123 has exited with code 0.
"""


class CompletenessTests(unittest.TestCase):
    def validate(self, report=None, console=CONSOLE, agents=(1000,)):
        return collect.validate_results(REPORT if report is None else report, console, agents, False, 64)

    def test_complete_nonzero_allocation_is_evidence_not_a_performance_failure(self):
        cases, errors = self.validate()
        self.assertEqual([], errors)
        self.assertEqual(1, len(cases))
        self.assertEqual({"gen0": 0, "gen1": 0, "gen2": 0, "allocated_bytes": 336, "operations": 64}, cases[0]["raw_gc"])
        self.assertEqual("/fixture in progress/child", cases[0]["binary_directory"])
        self.assertEqual("Concurrent Workstation", cases[0]["gc"])

    def test_raw_bytes_are_not_reconstructed_from_corrected_per_operation_value(self):
        report = copy.deepcopy(REPORT)
        report["Benchmarks"][0]["Memory"]["BytesAllocatedPerOperation"] = 0
        cases, errors = self.validate(report)
        self.assertEqual([], errors)
        self.assertEqual(336, cases[0]["raw_gc"]["allocated_bytes"])

    def test_empty_selection_is_incomplete(self):
        report = copy.deepcopy(REPORT)
        report["Benchmarks"] = []
        self.assertTrue(self.validate(report, "")[1])

    def test_missing_population_is_incomplete(self):
        self.assertTrue(self.validate(agents=(1000, 10000))[1])

    def test_duplicate_population_or_extra_job_is_incomplete(self):
        report = copy.deepcopy(REPORT)
        report["Benchmarks"] *= 2
        self.assertTrue(self.validate(report, CONSOLE * 2)[1])

    def test_extra_population_is_incomplete(self):
        self.assertTrue(self.validate(agents=(10000,))[1])

    def test_wrong_job_characteristics_are_incomplete(self):
        for old, new in (("WarmupCount=3", "WarmupCount=4"), ("Runtime=.NET 8.0", "Runtime=.NET 9.0"),
                         ("InvocationCount=64", "InvocationCount=65"), ("LaunchCount=1", "LaunchCount=2"),
                         ("RunStrategy=Throughput", "RunStrategy=ColdStart")):
            with self.subTest(new=new):
                report = copy.deepcopy(REPORT)
                report["Benchmarks"][0]["DisplayInfo"] = DISPLAY.replace(old, new)
                self.assertTrue(self.validate(report, CONSOLE.replace(old, new))[1])

    def test_failed_child_with_partial_measurements_is_incomplete(self):
        self.assertTrue(self.validate(console=CONSOLE.replace("exited with code 0.", "exited with code 3."))[1])

    def test_failed_report_is_incomplete_even_when_log_exists(self):
        report = copy.deepcopy(REPORT)
        report["Benchmarks"][0]["Statistics"] = None
        self.assertTrue(self.validate(report)[1])

    def test_measurements_must_be_complete_actual_workload_with_unique_indices(self):
        for change in ("missing", "duplicate", "overhead", "wrong_operations", "nonfinite"):
            with self.subTest(change=change):
                report = copy.deepcopy(REPORT)
                rows = report["Benchmarks"][0]["Measurements"]
                if change == "missing":
                    rows.pop()
                elif change == "duplicate":
                    rows[1] = rows[0].copy()
                elif change == "overhead":
                    rows[0]["IterationMode"] = "Overhead"
                elif change == "wrong_operations":
                    rows[0]["Operations"] = 1
                else:
                    rows[0]["Nanoseconds"] = float("nan")
                self.assertTrue(self.validate(report)[1])

    def test_raw_measurements_are_required_and_match_the_json(self):
        for console in (CONSOLE.replace("WorkloadActual   5:", "WorkloadWarmup   5:"),
                        CONSOLE.replace("1000000.00 ns", "2000000.00 ns")):
            self.assertTrue(self.validate(console=console)[1])

    def test_raw_gc_bytes_and_operations_are_required_without_rounding(self):
        for replacement in ("", "// GC:  0 0 0 ? 64", "// GC:  0 0 0 336 0", "// GC:  0 0 0 336 63"):
            with self.subTest(replacement=replacement):
                self.assertTrue(self.validate(console=CONSOLE.replace("// GC:  0 0 0 336 64", replacement))[1])

    def test_duplicate_raw_gc_records_are_incomplete(self):
        self.assertTrue(self.validate(console=CONSOLE.replace("// GC:  0 0 0 336 64", "// GC:  0 0 0 336 64\n// GC:  0 0 0 336 64"))[1])

    def test_runtime_gc_and_actual_execution_location_are_required(self):
        for prefix in ("// Runtime=", "// GC=", "// Execute:", "// BenchmarkDotNet"):
            with self.subTest(prefix=prefix):
                console = "\n".join(line for line in CONSOLE.splitlines() if not line.startswith(prefix))
                self.assertTrue(self.validate(console=console)[1])

    def test_log_case_must_correspond_to_report_case(self):
        self.assertTrue(self.validate(console=CONSOLE.replace("Job-fixture", "Job-different", 1))[1])

    def test_population_identity_must_agree_in_all_case_records(self):
        for field in ("display", "full_name", "execution"):
            with self.subTest(field=field):
                report, console = copy.deepcopy(REPORT), CONSOLE
                if field == "display":
                    report["Benchmarks"][0]["DisplayInfo"] = DISPLAY.replace("[AgentCount=1000]", "[AgentCount=10000]")
                    console = console.replace("[AgentCount=1000]", "[AgentCount=10000]")
                elif field == "full_name":
                    report["Benchmarks"][0]["FullName"] = "CompositionCostBenchmarks.Step(AgentCount: 10000)"
                else:
                    console = console.replace("--benchmarkName \"CompositionCostBenchmarks.Step(AgentCount: 1000)\"", "--benchmarkName \"CompositionCostBenchmarks.Step(AgentCount: 10000)\"")
                self.assertTrue(self.validate(report, console)[1])

    def test_runtime_os_and_bdn_metadata_are_required(self):
        report = copy.deepcopy(REPORT)
        report["HostEnvironmentInfo"] = {"unrelated": "not metadata"}
        self.assertTrue(self.validate(report)[1])

    def test_optional_bdn_sdk_field_does_not_discard_other_evidence(self):
        report = copy.deepcopy(REPORT)
        report["HostEnvironmentInfo"]["DotNetCliVersion"] = None
        self.assertEqual([], self.validate(report)[1])

    def test_child_runtime_must_match_requested_runtime(self):
        self.assertTrue(self.validate(console=CONSOLE.replace("// Runtime=.NET 8.0.33", "// Runtime=.NET 9.0.1"))[1])


class SourceIdentityTests(unittest.TestCase):
    def test_content_identity_works_without_git_and_detects_new_or_changed_source(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "SwarmSim.Core" / "Kernel.cs"
            source.parent.mkdir()
            source.write_bytes(b"before\n")
            first = collect.source_manifest(root, root / "evidence")
            self.assertEqual(
                hashlib.sha256(b"before\n").hexdigest(),
                first["files"].get("SwarmSim.Core/Kernel.cs", {}).get("sha256"),
            )
            source.write_bytes(b"after\n")
            second = collect.source_manifest(root, root / "evidence")
            self.assertNotEqual(first["sha256"], second["sha256"])
            (root / "new.props").write_text("new input", encoding="utf-8")
            self.assertNotEqual(second["sha256"], collect.source_manifest(root, root / "evidence")["sha256"])

    def test_generated_outputs_do_not_change_source_identity(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "Program.cs").write_text("source", encoding="utf-8")
            first = collect.source_manifest(root, root / "custom evidence")
            for directory in ("bin", "obj", "artifacts", "BenchmarkDotNet.Artifacts", "__pycache__", ".git", "custom evidence"):
                path = root / directory
                path.mkdir()
                (path / "generated").write_text("changes", encoding="utf-8")
            self.assertEqual(first, collect.source_manifest(root, root / "custom evidence"))

    def test_source_directory_symlink_cannot_be_silently_omitted(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target = root / "target"
            target.mkdir()
            try:
                (root / "linked source").symlink_to(target, target_is_directory=True)
            except OSError:
                self.skipTest("Directory symlinks are unavailable on this runner.")
            with self.assertRaises(ValueError):
                collect.source_manifest(root, root / "evidence")


class FreshOutputTests(unittest.TestCase):
    def test_existing_empty_directory_is_rejected_without_writing(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "existing"
            output.mkdir()
            with self.assertRaises(FileExistsError):
                collect.prepare_output(output)
            self.assertEqual([], list(output.iterdir()))

    def test_existing_result_is_preserved(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "existing"
            output.mkdir()
            sentinel = output / "receipt.json"
            sentinel.write_bytes(b"previous evidence")
            with self.assertRaises(FileExistsError):
                collect.prepare_output(output)
            self.assertEqual(b"previous evidence", sentinel.read_bytes())

    def test_new_directory_is_created(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "new" / "evidence"
            collect.prepare_output(output)
            self.assertTrue(output.is_dir())


class CaptureTests(unittest.TestCase):
    def test_command_selects_only_requested_population_and_fixed_measurement_job(self):
        command = collect.benchmark_command("/sdk/dotnet", Path("project.csproj"), Path("evidence"), [1000], False, 1024)
        expected = [
            "/sdk/dotnet", "run", "--project", "project.csproj", "-c", "Release", "--no-build", "--",
            "--filter", "*AgentCount: 1000)", "--runtimes", "net8.0", "--cli", "/sdk/dotnet",
            "--warmupCount", "3", "--iterationCount", "5", "--launchCount", "1", "--invocationCount", "1024", "--unrollFactor", "1",
            "--strategy", "Throughput",
            "--exporters", "fulljson", "csvmeasurements", "--keepFiles", "--stopOnFirstError", "--artifacts", str(Path("evidence") / "bdn"),
        ]
        self.assertEqual(expected, command)

    def test_smoke_command_has_one_dry_invocation_and_no_measurement_defaults(self):
        command = collect.benchmark_command("dotnet", Path("project.csproj"), Path("evidence"), [1000], True, 1)
        self.assertTrue(command)
        for option, expected in (("--job", "Dry"), ("--warmupCount", "0"), ("--iterationCount", "1"), ("--invocationCount", "1")):
            self.assertIn(option, command)
            self.assertEqual(expected, command[command.index(option) + 1])

    def test_real_subprocess_log_preserves_bytes_and_nonzero_exit(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            log = root / "raw.log"
            command = [sys.executable, "-c", "import os,sys; os.write(1,b'raw\\r\\n\\xff'); os.write(2,b'error\\n'); sys.exit(7)"]
            with contextlib.redirect_stdout(io.StringIO()):
                result = collect.run_logged(command, root, log)
            self.assertEqual(7, result)
            self.assertEqual(b"raw\r\n\xfferror\n", log.read_bytes())

    def test_artifact_hashes_cover_raw_outputs_but_not_self_referential_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "console.log").write_bytes(b"unabridged\n")
            (root / "receipt.json").write_bytes(b"self reference excluded")
            manifest = collect.artifact_manifest(root)
            self.assertEqual({"console.log"}, set(manifest))
            self.assertEqual(hashlib.sha256(b"unabridged\n").hexdigest(), manifest["console.log"]["sha256"])


class CollectionWorkflowTests(unittest.TestCase):
    def run_fixture(self, root, *, fail_build=False, change_source=False, omit_binary=False, sdk_version="8.0.427"):
        root = root.resolve()
        source = root / "Kernel.cs"
        source.write_bytes(b"source before")
        output = root / "custom output"
        child = root / "tools" / "CompositionCost" / "bin" / "Release" / "net8.0" / "generated" / "bin" / "Release" / "net8.0"
        options = SimpleNamespace(out=output, dotnet="fixture-dotnet", agents=[1000], smoke=False, invocations=64,
                                  arguments=["--agents", "1000", "--out", str(output)])

        def external_dotnet(command, directory, log):
            self.assertEqual(root, directory)
            if command[1] == "--info":
                log.write_text("Synthetic SDK metadata\n", encoding="utf-8")
                return 0
            if command[1] == "--version":
                log.write_text(sdk_version + "\n", encoding="utf-8")
                return 0
            if command[1] == "build":
                log.write_text("Synthetic build output\n", encoding="utf-8")
                return 1 if fail_build else 0
            self.assertEqual("run", command[1])
            child.mkdir(parents=True)
            for name in ("generated.dll", "generated.deps.json", "generated.runtimeconfig.json", "SwarmSim.Core.dll", "SwarmSim.Benchmarks.dll", "CompositionCost.dll"):
                if not (omit_binary and name == "generated.dll"):
                    (child / name).write_bytes(("synthetic child " + name).encode())
            report = output / "bdn" / "results" / "CompositionCostBenchmarks-report-full.json"
            report.parent.mkdir(parents=True)
            report.write_text(json.dumps(REPORT), encoding="utf-8")
            log.write_text(CONSOLE.replace("/fixture in progress/child", str(child)), encoding="utf-8")
            if change_source:
                source.write_bytes(b"source changed during benchmark")
            return 0

        with patch.object(collect, "run_logged", side_effect=external_dotnet), contextlib.redirect_stdout(io.StringIO()):
            status = collect.collect_evidence(root, options)
        return status, output, child

    def test_aliased_root_records_child_hashes_without_a_performance_verdict(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "existing").mkdir()
            status, output, child = self.run_fixture(root / "existing" / "..")
            self.assertTrue((output / "receipt.json").is_file())
            receipt = json.loads((output / "receipt.json").read_text(encoding="utf-8"))
            self.assertEqual(0, status, receipt["errors"])
            self.assertTrue(receipt["evidence_complete"], receipt["errors"])
            self.assertEqual("not_evaluated", receipt["performance_verdict"])
            self.assertEqual("8.0.427", receipt.get("selected_sdk_version"))
            self.assertEqual([], receipt["errors"])
            self.assertEqual(str(child), receipt["built_binaries"][0]["directory"])
            self.assertEqual(hashlib.sha256(b"synthetic child SwarmSim.Core.dll").hexdigest(),
                             receipt["built_binaries"][0]["files"]["SwarmSim.Core.dll"]["sha256"])
            self.assertIn("console.log", receipt["artifacts"])
            self.assertEqual(receipt["source_before"], receipt["source_after"])

    def test_source_changes_make_run_incomplete_and_preserve_raw_evidence(self):
        with tempfile.TemporaryDirectory() as temporary:
            status, output, _ = self.run_fixture(Path(temporary), change_source=True)
            receipt = json.loads((output / "receipt.json").read_text(encoding="utf-8"))
            self.assertEqual(1, status, receipt["errors"])
            self.assertFalse(receipt["evidence_complete"], receipt["errors"])
            self.assertNotEqual(receipt["source_before"], receipt["source_after"], receipt["errors"])
            self.assertIn("Kernel.cs", receipt.get("changed_source_files", []), receipt["errors"])
            self.assertTrue((output / "console.log").is_file())

    def test_failed_build_retains_an_incomplete_receipt_and_build_log(self):
        with tempfile.TemporaryDirectory() as temporary:
            status, output, _ = self.run_fixture(Path(temporary), fail_build=True)
            receipt = json.loads((output / "receipt.json").read_text(encoding="utf-8"))
            self.assertEqual(1, status, receipt["errors"])
            self.assertFalse(receipt["evidence_complete"], receipt["errors"])
            self.assertEqual(1, receipt["commands"][-1]["exit_code"], receipt["errors"])
            self.assertTrue((output / "build.log").is_file())

    def test_missing_actual_generated_binary_is_incomplete(self):
        with tempfile.TemporaryDirectory() as temporary:
            status, output, _ = self.run_fixture(Path(temporary), omit_binary=True)
            receipt = json.loads((output / "receipt.json").read_text(encoding="utf-8"))
            self.assertEqual(1, status, receipt["errors"])
            self.assertFalse(receipt["evidence_complete"], receipt["errors"])
            self.assertTrue(any("generated.dll" in error for error in receipt["errors"]), receipt["errors"])

    def test_missing_selected_sdk_version_is_incomplete(self):
        with tempfile.TemporaryDirectory() as temporary:
            status, output, _ = self.run_fixture(Path(temporary), sdk_version="not an SDK version")
            receipt = json.loads((output / "receipt.json").read_text(encoding="utf-8"))
            self.assertEqual(1, status, receipt["errors"])
            self.assertFalse(receipt["evidence_complete"], receipt["errors"])
            self.assertTrue(any("SDK version" in error for error in receipt["errors"]), receipt["errors"])

    def test_unexpected_capture_exception_cannot_publish_a_complete_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = root / "evidence"
            options = SimpleNamespace(out=output, dotnet="fixture", agents=[1000], smoke=False, invocations=64, arguments=[])
            status = None
            with patch.object(collect, "run_logged", side_effect=RuntimeError("unexpected capture failure")), contextlib.redirect_stdout(io.StringIO()):
                try:
                    status = collect.collect_evidence(root, options)
                except RuntimeError:
                    pass
            receipt = json.loads((output / "receipt.json").read_text(encoding="utf-8"))
            self.assertFalse(receipt["evidence_complete"])
            self.assertEqual(1, status)
            self.assertTrue(any("unexpected capture failure" in error for error in receipt["errors"]))


if __name__ == "__main__":
    unittest.main()
