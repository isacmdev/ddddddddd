"""Mutation gate: baseline isolated copies, then require every mutant to die."""
from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
import os
import hashlib
import json
import re
from pathlib import Path

sys.dont_write_bytecode = True

_MUTATION_ROWS = (
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "EXPECTED_FILES = 28", "EXPECTED_FILES = 29", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "if supplied != actual:", "if False and supplied != actual:", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "if message_type == \"integrity.evidence\" and payload.get(\"signature_result\") not in {\"valid\", \"invalid\", \"unknown\"}", "if False and message_type == \"integrity.evidence\" and payload.get(\"signature_result\") not in {\"valid\", \"invalid\", \"unknown\"}", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or not 1 <= payload[\"evidence_schema_version\"] <= 4294967295", "or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "isinstance(payload.get(\"evidence_schema_version\"), bool)", "False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or not isinstance(payload.get(\"evidence_schema_version\"), int)", "or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or any(ord(c) > 127 for c in payload[\"signer_summary\"])", "or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or any(ord(c) > 127 for c in payload[\"agent_version\"])", "or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "if message_type == \"policy.snapshot\":", "if False and message_type == \"policy.snapshot\":", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or not re.fullmatch(r\"[0-9a-f]{64}\", payload.get(\"binary_sha256\", \"\"))", "or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/fixtures/valid-create-time-request.json", '"origin":"overlay"', '"origin":"invalid"', "oracle"),
    ("openspec/changes/shared-contracts-freeze/fixtures/valid-runtime-activation.json", '"activation_state":"degraded"', '"activation_state":"invalid"', "oracle"),
    ("openspec/changes/shared-contracts-freeze/fixtures/valid-set-protected-account-response.json", '"status":"accepted"', '"status":"invalid"', "oracle"),
    ("openspec/changes/shared-contracts-freeze/fixtures/valid-create-time-request.json", '"minutes":180', '"minutes":181', "oracle"),
    ("src/ControlParental.Service/BackendClient.cs", "if (typed.IsValid)", "if (!typed.IsValid)", "BackendClientTests"),
    ("src/ControlParental.Service/AntiTamperMonitor.cs", "if (reportResult.IsInvalidEnvelope)", "if (false)", "IntegrityEnvelopeZeroStateTests"),
    ("src/ControlParental.Service/PolicyRepository.cs", "this.SetPolicySnapshot(null, SameVersionHashMismatchReason);", "this.SetPolicySnapshot(policy, SameVersionHashMismatchReason);", "PolicyRepositoryTests"),
    ("src/ControlParental.App.UI/RealtimeSubscriber.cs", "if (isForeground && this.IsCurrentGenerationLocked(currentGeneration, requestedEpoch))", "if (!isForeground && this.IsCurrentGenerationLocked(currentGeneration, requestedEpoch))", "RealtimeSubscriberTests"),
    ("src/ControlParental.Service/DeviceAuthenticator.cs", "if (root.TryGetProperty(\"device_id\", out var deviceIdElement))", "if (root.TryGetProperty(\"sub\", out var deviceIdElement))", "Identity"),
    ("src/ControlParental.Service/PairingService.cs", "BackendIdentityErrorV1.NotFound => PairingResult.InvalidCode(),", "BackendIdentityErrorV1.NotFound => PairingResult.ExpiredCode(),", "Pairing"),
    ("src/ControlParental.Service/BackendIdentityContractV1.cs", "public bool ExternalVerified => false;", "public bool ExternalVerified => true;", "BackendIdentityAcceptancePackageTests"),
    ("src/ControlParental.Service/BackendClient.cs", "if (response.IsSuccessStatusCode)", "if (!response.IsSuccessStatusCode)", "BackendClientTests"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "not re.fullmatch(r\"(?:[01]\\d|2[0-3]):[0-5]\\d\", item[\"from\"])", "False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "if not isinstance(value, str) or not TS_RE.fullmatch(value):", "if True:", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or len({day for day in days if isinstance(day, str)}) != len(days)", "or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "not isinstance(item.get(\"minutes\"), int) or not 0 <= item[\"minutes\"] <= 1440", "not isinstance(item.get(\"minutes\"), int) or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "re.fullmatch(r\"(?:[01]\\d|2[0-3]):[0-5]\\d\", value)", "re.fullmatch(r\".*\", value)", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "if payload.get(\"next_attempt_at\") is not None: _timestamp(payload.get(\"next_attempt_at\"), \"next_attempt_at\", False, errors)", "if False: pass", "oracle"),
    ("tests/contract-freeze/canonicalize.py", "normalized = format(decimal, \"f\")", "normalized = \"0\"", "oracle"),
    ("src/ControlParental.Domain/WireContracts/CanonicalJson.cs", "if (digits.Length == 0) return \"0\";", "if (false) return \"0\";", "CanonicalJson"),
    ("src/ControlParental.Domain/WireContracts/ContractBoundary.cs", "Timestamp(p, \"collected_at\", e, true)", "Timestamp(p, \"collected_at\", e)", "ContractBoundary"),
    ("src/ControlParental.Domain/WireContracts/ContractBoundary.cs", "days.EnumerateArray().Select(day => day.GetString()).Distinct(StringComparer.Ordinal).Count() != days.GetArrayLength()", "days.EnumerateArray().Select(day => day.GetString()).Distinct(StringComparer.Ordinal).Count() == days.GetArrayLength()", "ContractBoundary"),

    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or not 0 <= item.get(\"daily_limit_minutes\", -1) <= 1440", "or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "if len(canonical_payload) > 49152: errors.append(\"payload_size\")", "if len(canonical_payload) > 65536: errors.append(\"payload_size\")", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "FORBIDDEN_TOKENS += (\"jwt\", \"role\")", "FORBIDDEN_TOKENS += (\"jwt\",)", "oracle"),
    ("openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json", '"SUN"', '"FUNDAY"', "oracle"),
    ("openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json", '"action":"lock"', '"action":"permit"', "oracle"),
    ("openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json", '"source":"extra_time"', '"source":"evil"', "oracle"),
    ("openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json", '"minutes":30', '"minutes":0', "oracle"),
    # R10.1 residuals: each target is a single independent contract invariant.
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or unsafe(value))", "or False)", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or any(ord(c) > 127 for c in item[\"id\"])", "or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "and set(value.replace(\"-\", \"\")) != {\"0\"}", "and (set(value.replace(\"-\", \"\")) != {\"0\"} or True)", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "or not isinstance(scope, str) or not scope or any(ord(c) > 127 for c in scope)", "or False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "normalized.startswith(f\"x_{token}\")", "False", "oracle"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "except (TypeError, ValueError, OverflowError):\n        errors.append(f\"timestamp:{field}\")", "except (TypeError, ValueError, OverflowError):\n        return", "oracle"),

    ("src/ControlParental.Domain/WireContracts/ContractBoundary.cs", "hintType.GetString()!.Length is >= 1 and <= 32", "false", "ContractBoundary"),
    ("src/ControlParental.Domain/WireContracts/ContractBoundary.cs", "=> SecretToken(name);", "=> false;", "ContractBoundary"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", "UUID_RE = re.compile(r\"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$\")", "UUID_RE = re.compile(r\"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$\")", "oracle"),
    ("tests/contract-freeze/canonicalize.py", "def reject_constant(value: str) -> Decimal:\n        raise ValueError(f\"non-finite JSON number: {value}\")", "def reject_constant(value: str) -> Decimal:\n        return Decimal(0)", "oracle"),
    ("src/ControlParental.Domain/WireContracts/ContractBoundary.cs", "|| !System.Text.RegularExpressions.Regex.IsMatch(ci.GetString() ?? string.Empty, \"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$\"))", "|| !System.Text.RegularExpressions.Regex.IsMatch(ci.GetString() ?? string.Empty, \"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$\") || ((ci.GetString() ?? string.Empty)[19] is not ('8' or '9' or 'a' or 'b')))", "ContractBoundary"),
    ("openspec/changes/shared-contracts-freeze/verify_freeze.py", ", parse_constant=reject_constant", "", "oracle"),
)


class MutationSpec:
    def __init__(self, path, old, new, check, expected_diagnostics=(), validator="", inverse_probe="", residual=False):
        self.path = path
        self.old = old
        self.new = new
        self.check = check
        self.expected_diagnostics = expected_diagnostics
        self.validator = validator
        self.inverse_probe = inverse_probe
        self.residual = residual

    def __eq__(self, other):
        return isinstance(other, MutationSpec) and self.__dict__ == other.__dict__

    def __hash__(self):
        return hash(tuple(self.__dict__.values()))

    def __iter__(self):
        yield self.path
        yield self.old
        yield self.new
        yield self.check

    def __getitem__(self, index):
        return (self.path, self.old, self.new, self.check)[index]

    @property
    def inverse_runner(self):
        return self.inverse_probe


# One authoritative inventory: residual metadata travels with its mutation.
_RESIDUAL_FIELDS = {
    ('openspec/changes/shared-contracts-freeze/verify_freeze.py', 'normalized.startswith(f"x_{token}")'): (("extensions:",), "extensions secret-name validator", "extensions fixture + only validator disabled"),
    ('openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json', '"SUN"'): (("value:schedules/0/days",), "schedule days validator", "policy fixture + only validator disabled"),
    ('openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json', '"action":"lock"'): (("value:schedules/0/action",), "schedule action validator", "policy fixture + only validator disabled"),
    ('openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json', '"source":"extra_time"'): (("value:grants/0",), "grant source validator", "policy fixture + only validator disabled"),
    ('openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json', '"minutes":30'): (("value:grants/0",), "grant minutes validator", "policy fixture + only validator disabled"),
    ('openspec/changes/shared-contracts-freeze/verify_freeze.py', "or unsafe(value))"): (("test_oracle_is_total_and_enforces_authority_limits",), "extensions unsafe-value validator", "extensions fixture + only validator disabled"),
    ('openspec/changes/shared-contracts-freeze/verify_freeze.py', 'and set(value.replace("-", "")) != {"0"}'): (("uuid:correlation_hint",), "uuid nonzero validator", "UUID fixture + only validator disabled"),
    ('openspec/changes/shared-contracts-freeze/verify_freeze.py', 'or not 0 <= item.get("daily_limit_minutes", -1) <= 1440'): (("value:app_policies/1/daily_limit_minutes",), "daily-limit validator", "app-policy fixture + only validator disabled"),
    ('openspec/changes/shared-contracts-freeze/verify_freeze.py', 'or not isinstance(scope, str) or not scope or any(ord(c) > 127 for c in scope)'): (("value:grants/0",), "grant scope validator", "grant fixture + only validator disabled"),
    ('openspec/changes/shared-contracts-freeze/verify_freeze.py', ', parse_constant=reject_constant'): (("non-finite JSON number",), "non-finite loader validator", "non-finite fixture + only validator disabled"),
}


def _make_mutation(row):
    path, old, new, check = row
    fields = _RESIDUAL_FIELDS.get((path, old))
    if fields is None:
        return MutationSpec(path, old, new, check)
    diagnostics, validator, inverse_probe = fields
    return MutationSpec(path, old, new, check, diagnostics, validator, inverse_probe, True)


MUTATIONS = tuple(_make_mutation(row) for row in _MUTATION_ROWS)
RESIDUAL_METADATA = {(m.path, m.old): m for m in MUTATIONS if m.residual}
EXPECTED_DIAGNOSTICS = {(m.path, m.old): m.expected_diagnostics for m in MUTATIONS if m.expected_diagnostics}


def inverse_probe_succeeds(mutation: MutationSpec) -> bool:
    """Require explicit fixture + sole-validator inverse isolation metadata."""
    return bool(mutation.residual and mutation.validator and mutation.inverse_probe and "only validator disabled" in mutation.inverse_probe)


_INVERSE_SCRIPT = r'''
import hashlib, importlib.util, json, sys
from pathlib import Path
base, validator, baseline_path = Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3])
spec = importlib.util.spec_from_file_location("oracle", base / "openspec/changes/shared-contracts-freeze/verify_freeze.py")
oracle = importlib.util.module_from_spec(spec); spec.loader.exec_module(oracle)
baseline_spec = importlib.util.spec_from_file_location("baseline_oracle", baseline_path)
baseline_oracle = importlib.util.module_from_spec(baseline_spec); baseline_spec.loader.exec_module(baseline_oracle)
def load(name):
    path = base / "openspec/changes/shared-contracts-freeze/fixtures" / name
    raw, doc = oracle.load(path)
    return name, doc
def expect_rejection(name, doc, marker):
    errors = baseline_oracle.check_message(name, doc)
    if not any(marker in error for error in errors):
        raise SystemExit(f"baseline did not reject {marker}: {errors}")
def expect_acceptance(name, doc):
    if name == "valid-policy-snapshot.json":
        payload = dict(doc["payload"]); payload.pop("snapshot_hash", None)
        doc["payload"]["snapshot_hash"] = hashlib.sha256(oracle.canonical(payload).encode()).hexdigest()
    errors = oracle.check_message(name, doc)
    if errors:
        raise SystemExit(f"inverse survivor rejected: {errors}")
if validator == "extensions secret-name validator":
    name, doc = load("valid-integrity-evidence.json")
    doc["payload"]["extensions"] = {"x-authority": "redacted"}
    expect_rejection(name, doc, "extensions:")
    expect_acceptance(name, doc)
elif validator == "extensions unsafe-value validator":
    name, doc = load("valid-integrity-evidence.json")
    doc["payload"]["extensions"] = {"x-safe": ["authority"]}
    expect_rejection(name, doc, "extensions:")
    expect_acceptance(name, doc)
elif validator == "uuid nonzero validator":
    name, doc = load("valid-realtime-hint.json")
    doc["payload"]["correlation_hint"] = "00000000-0000-0000-0000-000000000000"
    expect_rejection(name, doc, "uuid:correlation_hint")
    expect_acceptance(name, doc)
elif validator == "daily-limit validator":
    name, doc = load("valid-policy-snapshot.json")
    doc["payload"]["app_policies"][1]["daily_limit_minutes"] = 1441
    expect_rejection(name, doc, "required:app_policies/1/daily_limit_minutes")
    expect_acceptance(name, doc)
elif validator == "grant scope validator":
    name, doc = load("valid-policy-snapshot.json")
    doc["payload"]["grants"][0]["scope"] = ""
    expect_rejection(name, doc, "value:grants/0")
    expect_acceptance(name, doc)
elif validator == "non-finite loader validator":
    path = base / "openspec/changes/shared-contracts-freeze/fixtures/valid-create-time-request.json"
    text = path.read_text(encoding="utf-8")
    if '"minutes":180' not in text:
        raise SystemExit("non-finite setup did not find numeric token")
    path.write_text(text.replace('"minutes":180', '"minutes":NaN', 1), encoding="utf-8")
    try:
        baseline_oracle.load(path)
    except ValueError:
        pass
    try:
        oracle.load(path)
    except ValueError:
        raise SystemExit("non-finite input still rejected")
    raise SystemExit(0)
else:
    name, doc = load("valid-policy-snapshot.json")
if validator == "schedule days validator":
    expect_rejection(name, doc, "value:schedules/0/days")
    doc["payload"]["schedules"][0]["id"] = "badéid"
    if not any("value:schedules/0/id" in error for error in oracle.check_message(name, doc)):
        raise SystemExit("independent schedule-id defect was hidden")
    doc["payload"]["schedules"][0]["id"] = "bedtime"
elif validator == "schedule action validator":
    expect_rejection(name, doc, "value:schedules/0/action")
    doc["payload"]["schedules"][0]["id"] = "badéid"
    if not any("value:schedules/0/id" in error for error in oracle.check_message(name, doc)):
        raise SystemExit("independent schedule-id defect was hidden")
    doc["payload"]["schedules"][0]["id"] = "bedtime"
elif validator == "grant source validator":
    expect_rejection(name, doc, "value:grants/0")
    doc["payload"]["grants"][0]["expires_at"] = doc["payload"]["grants"][0]["granted_at"]
    if not any("value:grants/0/expires_at" in error for error in oracle.check_message(name, doc)):
        raise SystemExit("independent grant-expiry defect was hidden")
    doc["payload"]["grants"][0]["expires_at"] = "2026-08-28T12:00:00Z"
elif validator == "grant minutes validator":
    expect_rejection(name, doc, "value:grants/0")
    doc["payload"]["grants"][0]["expires_at"] = doc["payload"]["grants"][0]["granted_at"]
    if not any("value:grants/0/expires_at" in error for error in oracle.check_message(name, doc)):
        raise SystemExit("independent grant-expiry defect was hidden")
    doc["payload"]["grants"][0]["expires_at"] = "2026-08-28T12:00:00Z"
elif validator == "grant scope validator":
    expect_rejection(name, doc, "value:grants/0")
    doc["payload"]["grants"][0]["expires_at"] = doc["payload"]["grants"][0]["granted_at"]
    if not any("value:grants/0/expires_at" in error for error in oracle.check_message(name, doc)):
        raise SystemExit("independent grant-expiry defect was hidden")
    doc["payload"]["grants"][0]["expires_at"] = "2026-08-28T12:00:00Z"
elif validator == "daily-limit validator":
    expect_rejection(name, doc, "required:app_policies/1/daily_limit_minutes")
    doc["payload"]["app_policies"][1]["package_name"] = "badéapp"
    if not any("value:app_policies/1/package_name" in error for error in oracle.check_message(name, doc)):
        raise SystemExit("independent package-name defect was hidden")
    doc["payload"]["app_policies"][1]["package_name"] = "instagram"
expect_acceptance(name, doc)
'''

_INVERSE_BYPASSES = {
    "schedule days validator": (
        'any(day not in {"MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"} for day in days if isinstance(day, str))',
        'False',
    ),
    "schedule action validator": (
        'item.get("action") not in {"lock", "allow_only"}',
        'False',
    ),
    "grant source validator": (
        'item.get("source") not in {"extra_time", "reward", "manual"}',
        'False',
    ),
    "grant minutes validator": (
        'not 1 <= item.get("minutes", 0) <= 180',
        'False',
    ),
    "grant scope validator": (
        'not isinstance(scope, str) or not scope or any(ord(c) > 127 for c in scope)',
        'False',
    ),
    "daily-limit validator": (
        'not 0 <= item.get("daily_limit_minutes", -1) <= 1440',
        'False',
    ),
}


def _apply_inverse_bypass(base: Path, mutation: MutationSpec) -> list[tuple[Path, str]]:
    if not mutation.path.endswith("valid-policy-snapshot.json"):
        return []
    bypass = _INVERSE_BYPASSES.get(mutation.validator)
    if bypass is None:
        return []
    path = base / "openspec/changes/shared-contracts-freeze/verify_freeze.py"
    original = path.read_text(encoding="utf-8")
    if bypass[0] not in original:
        raise ValueError(f"inverse validator target not found: {mutation.validator}")
    path.write_text(original.replace(bypass[0], bypass[1], 1), encoding="utf-8")
    return [(path, original)]


def run_inverse_probe(base: Path, mutation: MutationSpec) -> bool:
    """Execute the residual with only its target validator disabled.

    The mutation, bypass, and child probe run in a fresh nested copy.  This
    prevents fixture edits made by the child from contaminating the workspace
    used by the subsequent kill check; metadata alone can never make this pass.
    """
    if not inverse_probe_succeeds(mutation):
        return False
    with tempfile.TemporaryDirectory(prefix="inverse-probe-") as directory:
        probe_root = Path(directory) / "repo"
        try:
            shutil.copytree(base, probe_root, ignore=shutil.ignore_patterns(".git", ".codegraph", "bin", "obj"))
            mutation_path = probe_root / mutation.path
            original = mutation_path.read_text(encoding="utf-8")
            baseline_path = probe_root / "_inverse_baseline_oracle.py"
            oracle_path = probe_root / "openspec/changes/shared-contracts-freeze/verify_freeze.py"
            baseline_path.write_text(oracle_path.read_text(encoding="utf-8"), encoding="utf-8")
            before = original
            apply_mutation(probe_root, mutation)
            if mutation_path.read_text(encoding="utf-8") == before:
                return False
            if mutation.validator in _INVERSE_BYPASSES:
                _apply_inverse_bypass(probe_root, mutation)
            repair_policy_hash(probe_root, mutation.path)
            command = [sys.executable, "-c", _INVERSE_SCRIPT, str(probe_root), mutation.validator, str(baseline_path)]
            result = subprocess.run(command, cwd=probe_root, capture_output=True, text=True,
                                    check=False, timeout=120,
                                    env={**os.environ, "PYTHONDONTWRITEBYTECODE": "1"})
            return result.returncode == 0
        except (OSError, subprocess.SubprocessError, ValueError):
            return False


def oracle_probe_name(mutation: MutationSpec) -> str | None:
    probes = {
        "extensions secret-name validator": "OracleAdversarialTests.test_oracle_accepts_safe_extensions_and_rejects_authority_names",
        "extensions unsafe-value validator": "OracleAdversarialTests.test_oracle_is_total_and_enforces_authority_limits",
        "uuid nonzero validator": "OracleAdversarialTests.test_oracle_rejects_unknown_and_zero_hint_identity",
        "daily-limit validator": "OracleAdversarialTests.test_oracle_rejects_out_of_range_limited_policy",
        "grant scope validator": "OracleAdversarialTests.test_oracle_rejects_all_authority_scalar_boundaries",
        "non-finite loader validator": "OracleAdversarialTests.test_oracle_loader_rejects_non_finite_fixture_numbers",
    }
    return probes.get(mutation.validator)


def mutations_for_scope(scope: str):
    r10_2a_runtime_checks = {"BackendClientTests", "IntegrityEnvelopeZeroStateTests", "PolicyRepositoryTests"}
    if scope == "all":
        return MUTATIONS
    if scope == "contract":
        return tuple(item for item in MUTATIONS if item[3] in {"oracle", "CanonicalJson", "ContractBoundary"})
    if scope == "runtime-r10.2a":
        return tuple(item for item in MUTATIONS if item[3] in r10_2a_runtime_checks)
    if scope == "runtime":
        return tuple(item for item in MUTATIONS if item[3] not in {"oracle", "CanonicalJson", "ContractBoundary"})
    raise ValueError(f"unknown mutation scope: {scope}")


def is_semantic_success(returncode: int, output: str) -> bool:
    return returncode == 0 and not any(marker in output for marker in ("NETSDK", "MSB", "timed out", "assets file"))


def has_expected_diagnostics(output: str, expected: tuple[str, ...]) -> bool:
    """Require the mutation's intended diagnostic set, not merely a failure."""
    if not expected or not all(marker in output for marker in expected):
        return False
    diagnostic_re = re.compile(
        r"(?:extensions:|uuid:[A-Za-z0-9_./-]+|value:[A-Za-z0-9_./-]+|"
        r"timestamp:[A-Za-z0-9_./-]+|future:[A-Za-z0-9_./-]+|age:[A-Za-z0-9_./-]+|"
        r"shape:[A-Za-z0-9_./-]+|required:[A-Za-z0-9_./-]+|unknown:[A-Za-z0-9_./-]+|"
        r"hash|payload_size|non-finite JSON number)"
    )
    observed = set(diagnostic_re.findall(output))
    def is_expected(diagnostic):
        return any(
            diagnostic.startswith(marker) if marker.endswith(":") else diagnostic == marker
            for marker in expected
        )
    return (not observed and all(marker in output for marker in expected)) or (
        bool(observed) and all(is_expected(diagnostic) for diagnostic in observed)
    )


def has_expected_probe(output: str, mutation: MutationSpec) -> bool:
    """Separate probe identity from contractual diagnostic attribution."""
    probe = oracle_probe_name(mutation)
    diagnostic_re = re.compile(
        r"(?:extensions:|uuid:[A-Za-z0-9_./-]+|value:[A-Za-z0-9_./-]+|"
        r"timestamp:[A-Za-z0-9_./-]+|future:[A-Za-z0-9_./-]+|age:[A-Za-z0-9_./-]+|"
        r"shape:[A-Za-z0-9_./-]+|required:[A-Za-z0-9_./-]+|unknown:[A-Za-z0-9_./-]+|"
        r"hash|payload_size|non-finite JSON number)"
    )
    return bool(probe and probe in output and not diagnostic_re.search(output))




def workspace_snapshot(root: Path) -> dict[str, tuple[int, int, str]]:
    snapshot = {}
    for path in root.rglob("*"):
        if not path.is_file() or any(part in {".git", ".codegraph", "bin", "obj"} for part in path.parts):
            continue
        relative = path.relative_to(root).as_posix()
        stat = path.stat()
        snapshot[relative] = (stat.st_size, stat.st_mtime_ns, hashlib.sha256(path.read_bytes()).hexdigest())
    return snapshot


def project_for_kind(kind: str) -> str:
    if kind in {"CanonicalJson", "ContractBoundary"}:
        return "tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj"
    if kind == "RealtimeSubscriberTests":
        return "tests/ControlParental.App.UI.Tests/ControlParental.App.UI.Tests.csproj"
    return "tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj"


def restore_project(base: Path, kind: str) -> subprocess.CompletedProcess[str]:
    project = project_for_kind(kind)
    return subprocess.run(["dotnet", "restore", str(base / project), "--verbosity", "quiet"], cwd=base, capture_output=True, text=True, check=False, timeout=300)


def run_check(base: Path, kind: str, probe: str | None = None) -> subprocess.CompletedProcess[str]:
    env = os.environ.copy()
    env["PYTHONDONTWRITEBYTECODE"] = "1"
    if kind == "oracle":
        command = [sys.executable, str(base / "openspec/changes/shared-contracts-freeze/verify_freeze.py")]
    else:
        project = project_for_kind(kind)
        command = ["dotnet", "test", str(base / project), "--no-restore", "--filter", f"FullyQualifiedName~{kind}", "--verbosity", "quiet"]
    try:
        before = workspace_snapshot(base) if kind == "oracle" else None
        result = subprocess.run(command, cwd=base, env=env, capture_output=True, text=True, check=False, timeout=120)
    except subprocess.TimeoutExpired as timeout:
        return subprocess.CompletedProcess(command, 124, timeout.stdout or "", (timeout.stderr or "") + "mutation check timed out")
    if kind == "oracle" and before != workspace_snapshot(base):
        return subprocess.CompletedProcess(command, 125, result.stdout, result.stderr + "oracle mutated workspace")
    if kind == "oracle" and result.returncode == 0:
        adversarial_before = workspace_snapshot(base)
        adversarial_target = "tests/contract-freeze/test_oracle_adversarial.py"
        if probe:
            adversarial_target = ["tests/contract-freeze/test_oracle_adversarial.py", probe]
        else:
            adversarial_target = [adversarial_target]
        adversarial = subprocess.run(
            [sys.executable, *adversarial_target],
            cwd=base, env=env, capture_output=True, text=True, check=False)
        if adversarial.returncode != 0:
            return adversarial
        if adversarial_before != workspace_snapshot(base):
            return subprocess.CompletedProcess(command, 125, adversarial.stdout, adversarial.stderr + "oracle adversarial mutated workspace")
    return result


def apply_mutation(base: Path, mutation: tuple[str, str, str, str]) -> None:
    relative, old, new, _ = mutation
    target = base / relative
    original = target.read_text(encoding="utf-8")
    if old not in original:
        raise ValueError(f"mutation target not found: {relative}")
    target.write_text(original.replace(old, new, 1), encoding="utf-8")


def repair_policy_hash(base: Path, relative: str) -> None:
    """Keep fixture mutations focused on their semantic invariant, not its hash."""
    if not relative.endswith("valid-policy-snapshot.json"):
        return
    sys.path.insert(0, str(base / "tests/contract-freeze"))
    from canonicalize import canonicalize
    path = base / relative
    document = json.loads(path.read_text(encoding="utf-8"))
    payload = document["payload"]
    payload.pop("snapshot_hash", None)
    payload["snapshot_hash"] = hashlib.sha256(canonicalize(payload).encode()).hexdigest()
    path.write_text(json.dumps(document, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")


def main() -> int:
    parser = __import__("argparse").ArgumentParser()
    parser.add_argument("--repo", default=".", type=Path)
    parser.add_argument("--scope", choices=("all", "contract", "runtime-r10.2a", "runtime"), default="all")
    args = parser.parse_args()
    root = args.repo.resolve()
    mutations = mutations_for_scope(args.scope)
    residuals = [mutation for mutation in mutations if mutation.residual]
    if args.scope in {"all", "contract"}:
        if len(residuals) != len(RESIDUAL_METADATA) or {
                (mutation.path, mutation.old) for mutation in residuals} != set(RESIDUAL_METADATA):
            print("FAIL: residual inventory and metadata are not identical", file=sys.stderr)
            return 2
        if any(not mutation.expected_diagnostics or not inverse_probe_succeeds(mutation)
               for mutation in residuals):
            print("FAIL: residual missing exact diagnostic or inverse-probe metadata", file=sys.stderr)
            return 2
    keys = [(path, old) for path, old, _, _ in mutations]
    if len(keys) != len(set(keys)):
        print("FAIL: duplicate mutation targets", file=sys.stderr)
        return 2
    missing = [path for path, old, _, _ in mutations
               if not (root.joinpath(path).exists() and old in root.joinpath(path).read_text(encoding="utf-8"))]
    if missing:
        print("FAIL: mutation target not found: " + ", ".join(missing), file=sys.stderr)
        return 2
    for index, (left_path, left_old, _, _) in enumerate(mutations):
        for right_path, right_old, _, _ in mutations[index + 1:]:
            if left_path == right_path and (left_old in right_old or right_old in left_old):
                print(f"FAIL: overlapping mutation targets: {left_path}", file=sys.stderr)
                return 2
    before = set(subprocess.run(["git", "status", "--porcelain", "--untracked-files=all"], cwd=root, capture_output=True, text=True, check=True).stdout.splitlines())
    survivors: list[str] = []
    with tempfile.TemporaryDirectory(prefix="contract-mutations-") as directory:
        base = Path(directory) / "repo"
        shutil.copytree(root, base, ignore=shutil.ignore_patterns(".git", ".codegraph", "bin", "obj"))
        baseline_kinds = {kind for *_, kind in mutations}
        restored_projects = set()
        for kind in sorted(baseline_kinds):
            if kind == "oracle":
                baseline = run_check(base, kind)
                if not is_semantic_success(baseline.returncode, baseline.stdout + baseline.stderr):
                    print("FAIL baseline oracle: isolated oracle/adversarial checks did not pass", file=sys.stderr)
                    print((baseline.stdout + baseline.stderr)[-4000:], file=sys.stderr)
                if baseline.returncode != 0:
                    return 2
                continue
            project = project_for_kind(kind)
            if project not in restored_projects:
                restore = restore_project(base, kind)
                if restore.returncode != 0:
                    print(f"FAIL baseline {kind}: isolated restore failed", file=sys.stderr)
                    print((restore.stdout + restore.stderr)[-4000:], file=sys.stderr)
                    return 2
                restored_projects.add(project)
            baseline = run_check(base, kind)
            if not is_semantic_success(baseline.returncode, baseline.stdout + baseline.stderr):
                print(f"FAIL baseline {kind}: isolated restore/build/test did not pass", file=sys.stderr)
                print((baseline.stdout + baseline.stderr)[-4000:], file=sys.stderr)
                return 2
        for number, mutation in enumerate(mutations, 1):
            relative, old, new, check = mutation
            target = base / relative
            original = target.read_text(encoding="utf-8")
            if old not in original:
                print(f"FAIL M{number:02d}: mutation target not found: {relative}", file=sys.stderr)
                return 2
            if mutation.residual and not run_inverse_probe(base, mutation):
                print(f"FAIL M{number:02d}: inverse probe did not execute a semantic survivor", file=sys.stderr)
                return 1
            target.write_text(original.replace(old, new, 1), encoding="utf-8")
            repair_policy_hash(base, relative)
            result = run_check(base, check, oracle_probe_name(mutation) if check == "oracle" else None)
            target.write_text(original, encoding="utf-8")
            if not is_semantic_success(result.returncode, result.stdout + result.stderr) and any(marker in result.stdout + result.stderr for marker in ("NETSDK", "MSB", "timed out", "assets file")):
                print(f"FAIL M{number:02d}: infrastructure failure ({check})", file=sys.stderr)
                return 2
            if result.returncode == 0:
                survivors.append(f"M{number:02d}")
                print(f"FAIL M{number:02d}: survived ({check})")
            elif (relative, old) in RESIDUAL_METADATA and not (
                    has_expected_diagnostics(result.stdout + result.stderr,
                                             EXPECTED_DIAGNOSTICS.get((relative, old), ()))
                    or has_expected_probe(result.stdout + result.stderr, mutation)):
                expected = EXPECTED_DIAGNOSTICS.get((relative, old), ())
                print(f"FAIL M{number:02d}: wrong diagnostic ({check}) expected={expected}\n{result.stdout[-2000:]}\n{result.stderr[-2000:]}", file=sys.stderr)
                return 1
            else:
                print(f"PASS M{number:02d}: killed ({check})")
        multi_indices = [index for index, mutation in enumerate(mutations) if mutation in (MUTATIONS[4], MUTATIONS[12])]
        if len(multi_indices) == 2:
            apply_mutation(base, mutations[multi_indices[0]])
            apply_mutation(base, mutations[multi_indices[1]])
        multi_cause = run_check(base, "oracle") if len(multi_indices) == 2 else None
        if multi_cause is not None and multi_cause.returncode == 0:
            print("FAIL: multi-cause mutation survived", file=sys.stderr)
            return 1
        print("PASS multi-cause mutation: oracle rejected combined corruption")
    after = set(subprocess.run(["git", "status", "--porcelain", "--untracked-files=all"], cwd=root, capture_output=True, text=True, check=True).stdout.splitlines())
    if before != after:
        print("FAIL: git status changed", file=sys.stderr)
        return 1
    if survivors:
        print("FAIL: surviving mutations " + ", ".join(survivors), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
