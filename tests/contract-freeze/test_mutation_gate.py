import importlib.util
from unittest.mock import patch
from pathlib import Path
import unittest
import tempfile
import shutil

ROOT = Path(__file__).parents[2]
PATH = ROOT / "tests/contract-freeze/run_mutation_gate.py"
spec = importlib.util.spec_from_file_location("mutation_gate", PATH)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class MutationGateContractScopeTests(unittest.TestCase):
    def test_mutations_are_unique_and_all_targets_exist(self):
        keys = [(path, old) for path, old, _, _ in gate.MUTATIONS]
        self.assertEqual(len(keys), len(set(keys)))
        missing = [path for path, old, _, _ in gate.MUTATIONS
                   if not (Path(path).exists() and old in Path(path).read_text(encoding="utf-8"))]
        self.assertEqual(missing, [])

    def test_residual_reproductions_have_one_mutant_each(self):
        residuals = {('normalized.startswith(f"x_{token}")', 'False'), ('"SUN"', '"FUNDAY"'),
                     ('"action":"lock"', '"action":"permit"'),
                     ('"source":"extra_time"', '"source":"evil"'),
                     ('"minutes":30', '"minutes":0'),
                     ('or unsafe(value))', 'or False)'),
                     ('and set(value.replace("-", "")) != {"0"}', 'and (set(value.replace("-", "")) != {"0"} or True)'),
                     ('or not 0 <= item.get("daily_limit_minutes", -1) <= 1440', 'or False'),
                     ('or not isinstance(scope, str) or not scope or any(ord(c) > 127 for c in scope)', 'or False'),
                     (', parse_constant=reject_constant', '')}
        represented = {(old, new) for _, old, new, _ in gate.MUTATIONS}
        self.assertTrue(residuals <= represented)

    def test_contract_scope_excludes_downstream_mutations_and_keeps_contract_seams(self):
        selected = gate.mutations_for_scope("contract")
        checks = {item[3] for item in selected}
        self.assertIn("oracle", checks)
        self.assertIn("CanonicalJson", checks)
        self.assertIn("ContractBoundary", checks)
        self.assertNotIn("RealtimeSubscriberTests", checks)
        self.assertNotIn("BackendClientTests", checks)
        self.assertNotIn("Pairing", checks)

    def test_runtime_r10_2a_scope_requires_productive_policy_and_integrity_mutants(self):
        selected = gate.mutations_for_scope("runtime-r10.2a")
        self.assertEqual(
            {mutation[3] for mutation in selected},
            {"BackendClientTests", "IntegrityEnvelopeZeroStateTests", "PolicyRepositoryTests"},
        )
        self.assertTrue(any(
            mutation[0] == "src/ControlParental.Service/PolicyRepository.cs"
            and mutation[1] == "this.SetPolicySnapshot(null, SameVersionHashMismatchReason);"
            for mutation in selected
        ))
        self.assertNotIn("RealtimeSubscriberTests", {mutation[3] for mutation in selected})
        self.assertNotIn("BackendIdentityAcceptancePackageTests", {mutation[3] for mutation in selected})
        self.assertNotIn("Pairing", {mutation[3] for mutation in selected})

    def test_full_runtime_scope_retains_downstream_mutants_and_r10_2a_is_subset(self):
        selected = set(gate.mutations_for_scope("runtime-r10.2a"))
        runtime = set(gate.mutations_for_scope("runtime"))
        self.assertTrue(selected <= runtime)
        self.assertIn("RealtimeSubscriberTests", {mutation[3] for mutation in runtime})
        self.assertIn("BackendIdentityAcceptancePackageTests", {mutation[3] for mutation in runtime})
        self.assertIn("Pairing", {mutation[3] for mutation in runtime})

    def test_full_runtime_scope_requires_identity_pairing_and_external_verification_mutants(self):
        checks = {mutation[3] for mutation in gate.mutations_for_scope("runtime")}
        self.assertIn("Identity", checks)
        self.assertIn("Pairing", checks)
        self.assertIn("BackendIdentityAcceptancePackageTests", checks)

    def test_baseline_check_requires_semantic_test_success(self):
        self.assertTrue(gate.is_semantic_success(0, "Passed!"))
        self.assertFalse(gate.is_semantic_success(1, "NETSDK1004: assets file not found"))
        self.assertFalse(gate.is_semantic_success(124, "mutation check timed out"))

    def test_mutation_result_requires_expected_diagnostic(self):
        self.assertTrue(gate.has_expected_diagnostics("FAIL\nvalue:schedules/0/days", ("value:schedules/0/days",)))
        self.assertFalse(gate.has_expected_diagnostics("FAIL\nvalue:schedules/0/action", ("value:schedules/0/days",)))
        self.assertFalse(gate.has_expected_diagnostics("FAIL\nvalue:schedules/0/days\nvalue:schedules/0/action", ("value:schedules/0/days",)))
        self.assertFalse(gate.has_expected_diagnostics("unittest failure value:schedules/0/days plus value:schedules/0/action", ("value:schedules/0/days",)))

    def test_every_contract_mutation_has_exact_diagnostic_metadata(self):
        selected = gate.mutations_for_scope("contract")
        residuals = [mutation for mutation in selected if mutation.residual]
        self.assertEqual(len(residuals), 10)
        self.assertEqual(
            {(mutation.path, mutation.old) for mutation in residuals},
            set(gate.RESIDUAL_METADATA),
        )
        for mutation in residuals:
            self.assertTrue(mutation.expected_diagnostics)
            self.assertTrue(mutation.validator)
            self.assertTrue(mutation.inverse_probe)
            self.assertEqual(
                gate.RESIDUAL_METADATA[(mutation.path, mutation.old)],
                mutation,
            )

    def test_inverse_probe_requires_mutant_to_survive_only_target_validator(self):
        for mutation in gate.RESIDUAL_METADATA.values():
            self.assertTrue(gate.inverse_probe_succeeds(mutation))

    def test_inverse_probe_rejects_noop_mutation_before_subprocess(self):
        mutation = next(iter(gate.RESIDUAL_METADATA.values()))
        with patch.object(gate, "apply_mutation"), patch.object(gate, "repair_policy_hash"), \
                patch.object(gate.subprocess, "run") as run:
            self.assertFalse(gate.run_inverse_probe(ROOT, mutation))
        self.assertFalse(run.called)

    def test_inverse_bypasses_are_atomic_validator_rules(self):
        self.assertTrue(gate._INVERSE_BYPASSES)
        for validator, (old, new) in gate._INVERSE_BYPASSES.items():
            self.assertNotIn("_nested_policy", old, validator)
            self.assertNotIn("if collection", old, validator)
            self.assertNotIn("lambda", new, validator)

    def test_inverse_probe_does_not_leave_child_fixture_mutations_behind(self):
        mutation = next(
            mutation for mutation in gate.RESIDUAL_METADATA.values()
            if mutation.validator == "non-finite loader validator"
        )
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory) / "repo"
            shutil.copytree(ROOT, base, ignore=shutil.ignore_patterns(".git", ".codegraph", "bin", "obj"))
            fixture = base / "openspec/changes/shared-contracts-freeze/fixtures/valid-create-time-request.json"
            before = fixture.read_bytes()
            self.assertTrue(gate.run_inverse_probe(base, mutation))
            self.assertEqual(before, fixture.read_bytes())


if __name__ == "__main__":
    unittest.main()
