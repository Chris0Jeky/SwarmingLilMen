"""Fail-closed checks for the expensive scientific matrix's CI wrapper."""
import unittest

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


if __name__ == "__main__":
    unittest.main()
