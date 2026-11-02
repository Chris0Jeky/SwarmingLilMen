"""Fail-closed checks for the expensive scientific matrix's CI wrapper."""
import unittest

import scientific_parity
from scientific_parity import validate_result


class ScientificParityTests(unittest.TestCase):
    def setUp(self):
        self.rows = [
            {"Scenario": scenario, "Seed": seed, "Model": model}
            for scenario in ("S1", "S2")
            for seed in (7, 19)
            for model in ("classic", "reference")
        ]

    def check(self, mode="verify", code=0, text="", rows=None, before="same", after="same"):
        validate_result(mode, code, text, self.rows if rows is None else rows,
                        self.rows, before, after)

    def test_normal_verification_requires_success(self):
        self.check()
        for code in (1, 2, 3, -9):
            with self.subTest(code=code), self.assertRaises(ValueError):
                self.check(code=code)

    def test_alignment_witness_requires_comparison_rejection_and_polarization(self):
        self.check("alignment-off", 1, "S4 polarization: difference 0.9 > 0.04")
        for code in (0, 2, 3, -9):
            with self.subTest(code=code), self.assertRaises(ValueError):
                self.check("alignment-off", code, "S4 polarization: difference 0.9 > 0.04")

    def test_distance_only_or_missing_witness_is_not_a_passing_mutation(self):
        for text in ("", "S4 nearestNeighborMean: difference 1 > 0.5",
                     "Alignment-off did not violate an envelope."):
            with self.subTest(text=text), self.assertRaises(ValueError):
                self.check("alignment-off", 1, text)

    def test_partial_or_duplicate_matrix_is_rejected(self):
        for rows in ([], self.rows[:-1], self.rows[:-1] + [self.rows[0]],
                     self.rows + [self.rows[0]]):
            with self.subTest(rows=rows), self.assertRaises(ValueError):
                self.check(rows=rows)

    def test_different_seed_model_or_scenario_is_rejected(self):
        for key, value in (("Seed", 42), ("Model", "legacy"), ("Scenario", "other")):
            rows = [dict(row) for row in self.rows]
            rows[0][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.check(rows=rows)

    def test_matrix_order_is_not_a_semantic_change(self):
        self.check(rows=list(reversed(self.rows)))

    def test_baseline_bytes_cannot_change_during_verification(self):
        with self.assertRaises(ValueError):
            self.check(after="changed")


class ScientificTimingTests(unittest.TestCase):
    @staticmethod
    def timing(mode="verify"):
        normal = mode == "verify"
        return {
            "Version": 1,
            "PrimaryRuns": 100, "RepeatRuns": 100 if normal else 0,
            "NaiveIndexRuns": 50 if normal else 0,
            "PrimarySeconds": 12.0, "RepeatSeconds": 9.0 if normal else 0.0,
            "NaiveIndexSeconds": 20.0 if normal else 0.0,
            "MatrixSeconds": 42.5 if normal else 13.0,
            "MatrixScope": "Run loops and exact checks; excludes metadata and final comparison.",
        }

    def test_complete_workloads_keep_primary_and_verification_costs_distinct(self):
        for mode in ("verify", "alignment-off"):
            with self.subTest(mode=mode):
                scientific_parity.validate_timing(mode, self.timing(mode))

    def test_missing_timing_and_fields_are_rejected(self):
        invalid = [None, [], {}]
        for key in self.timing():
            candidate = self.timing()
            del candidate[key]
            invalid.append(candidate)
        for candidate in invalid:
            with self.subTest(candidate=candidate), self.assertRaises(ValueError):
                scientific_parity.validate_timing("verify", candidate)

    def test_unknown_version_or_undefined_scope_is_rejected(self):
        for key, values in (("Version", (0, 2, True, 1.0, "1")),
                            ("MatrixScope", (None, 0, "", "   "))):
            for value in values:
                candidate = self.timing()
                candidate[key] = value
                with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                    scientific_parity.validate_timing("verify", candidate)

    def test_counts_require_all_and_only_the_prescribed_completed_runs(self):
        for mode in ("verify", "alignment-off"):
            for key in ("PrimaryRuns", "RepeatRuns", "NaiveIndexRuns"):
                expected = self.timing(mode)[key]
                for value in (expected - 1, expected + 1, True, float(expected), str(expected)):
                    candidate = self.timing(mode)
                    candidate[key] = value
                    with self.subTest(mode=mode, key=key, value=value), self.assertRaises(ValueError):
                        scientific_parity.validate_timing(mode, candidate)

    def test_durations_must_be_finite_nonnegative_numbers(self):
        for key in ("PrimarySeconds", "RepeatSeconds", "NaiveIndexSeconds", "MatrixSeconds"):
            for value in (-0.001, float("nan"), float("inf"), -float("inf"), True, "12", None):
                candidate = self.timing()
                candidate[key] = value
                with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                    scientific_parity.validate_timing("verify", candidate)

    def test_witness_cannot_claim_time_for_omitted_verification_runs(self):
        for key in ("RepeatSeconds", "NaiveIndexSeconds"):
            candidate = self.timing("alignment-off")
            candidate[key] = 0.01
            with self.subTest(key=key), self.assertRaises(ValueError):
                scientific_parity.validate_timing("alignment-off", candidate)

    def test_phase_cost_cannot_exceed_the_matrix_interval(self):
        candidate = self.timing()
        candidate["MatrixSeconds"] = 40.0
        with self.assertRaises(ValueError):
            scientific_parity.validate_timing("verify", candidate)


if __name__ == "__main__":
    unittest.main()
