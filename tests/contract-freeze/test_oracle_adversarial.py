import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).parents[2]
ORACLE_PATH = ROOT / "openspec/changes/shared-contracts-freeze/verify_freeze.py"
spec = importlib.util.spec_from_file_location("verify_freeze", ORACLE_PATH)
oracle = importlib.util.module_from_spec(spec)
spec.loader.exec_module(oracle)
FIX = ROOT / "openspec/changes/shared-contracts-freeze/fixtures"


def load(name):
    return json.loads((FIX / name).read_text(encoding="utf-8"))


def errors(name, doc):
    return oracle.check_message(name, doc, len(json.dumps(doc, ensure_ascii=False).encode()))


def rehash_policy(doc):
    payload = dict(doc["payload"])
    payload.pop("snapshot_hash", None)
    doc["payload"]["snapshot_hash"] = __import__("hashlib").sha256(oracle.canonical(payload).encode()).hexdigest()


class OracleAdversarialTests(unittest.TestCase):
    def test_oracle_rejects_exact_f1_policy_and_nested_probes(self):
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["schedules"] = [{"days": ["MON"], "action": "lock", "allow_list": []}]
        self.assertTrue(any(x.startswith("required:schedules") for x in errors("valid-policy-snapshot.json", policy)))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["category_assignments"] = ["not-an-object"]
        self.assertTrue(any("category_assignments" in x for x in errors("valid-policy-snapshot.json", policy)))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["app_policies"] = [{"package_name":"a.b", "state":"permit", "daily_limit_minutes":0, "category":"x", "allowed_windows":[]}]
        self.assertTrue(any("app_policies" in x for x in errors("valid-policy-snapshot.json", policy)))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["grants"] = [{"id":"00000000-0000-4000-8000-000000000001", "request_id":"00000000-0000-4000-8000-000000000002", "scope":"device", "minutes":1, "granted_at":"2026-08-28T12:00:00Z", "expires_at":"2026-08-28T11:59:59Z", "source":"manual"}]
        self.assertTrue(any("grant" in x or "expires" in x for x in errors("valid-policy-snapshot.json", policy)))

    def test_oracle_rejects_account_integrity_extensions_and_payload_limit(self):
        account = load("valid-set-protected-account.json")
        account["payload"]["username"] = ""
        self.assertTrue(errors("valid-set-protected-account.json", account))
        account = load("valid-set-protected-account.json")
        account["payload"]["sid"] = "not-a-sid"
        self.assertTrue(errors("valid-set-protected-account.json", account))
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["binary_sha256"] = "bad"
        self.assertTrue(errors("valid-integrity-evidence.json", evidence))
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["evidence_schema_version"] = 0
        self.assertTrue(errors("valid-integrity-evidence.json", evidence))
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["evidence_schema_version"] = True
        self.assertTrue(errors("valid-integrity-evidence.json", evidence))
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["evidence_schema_version"] = "1"
        self.assertTrue(errors("valid-integrity-evidence.json", evidence))
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["agent_version"] = ""
        self.assertTrue(errors("valid-integrity-evidence.json", evidence))
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["signer_summary"] = ""
        self.assertTrue(errors("valid-integrity-evidence.json", evidence))
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["signature_result"] = "forged"
        self.assertTrue(errors("valid-integrity-evidence.json", evidence))
        evidence = load("valid-integrity-evidence.json")
        evidence["extensions"] = {"x-authority": "safe"}
        self.assertTrue(errors("valid-integrity-evidence.json", evidence), "extensions: expected rejection")
        oversized = load("valid-policy-snapshot.json")
        oversized["payload"]["extensions"] = {"x-pad": "a" * 50000}
        self.assertTrue(any(x == "size" or x.startswith("string:") for x in errors("valid-policy-snapshot.json", oversized)))
        account = load("valid-set-protected-account.json")
        account["payload"]["extensions"] = {"x-authority": "secret"}
        self.assertTrue(errors("valid-set-protected-account.json", account))

    def test_canonical_numeric_vectors_are_stable(self):
        self.assertEqual(oracle.canonical(oracle.Decimal("1.0000000000000001")), "1.0000000000000001")
        self.assertEqual(oracle.canonical(oracle.Decimal("1e30")), "1000000000000000000000000000000")
        self.assertEqual(oracle.canonical(oracle.Decimal("1e-30")), "0.000000000000000000000000000001")
        self.assertEqual(oracle.canonical({"z": "😀", "a": [1, "\\\" "]}), '{"a":[1,"\\\\\\\" "],"z":"\\uD83D\\uDE00"}')

    def test_shared_golden_vectors_match_normative_python_canonicalizer(self):
        vectors = json.loads((ROOT / "tests/contract-freeze/canonical-golden.json").read_text(encoding="utf-8"))
        for vector in vectors:
            value = json.loads(vector["input"], parse_int=oracle.Decimal, parse_float=oracle.Decimal)
            self.assertEqual(oracle.canonical(value).encode("utf-8"), vector["canonical"].encode("utf-8"))

    def test_python_canonicalizer_rejects_non_finite_numbers(self):
        invalid = json.loads((ROOT / "tests/contract-freeze/canonical-invalid.json").read_text(encoding="utf-8"))
        for vector in invalid:
            with self.assertRaises(ValueError):
                raw = bytes.fromhex(vector["input_hex"].replace(" ", "")) if "input_hex" in vector else vector["input"].encode("utf-8")
                oracle.canonicalize_bytes(raw)
        for raw in (b"NaN", b"Infinity", b"-Infinity"):
            with self.assertRaises(ValueError):
                oracle.canonicalize_bytes(raw)

    def test_oracle_loader_rejects_non_finite_fixture_numbers(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "invalid.json"
            path.write_text('{"z":NaN}', encoding="utf-8")
            with self.assertRaises(ValueError):
                oracle.load(path)

    def test_python_canonicalizer_rejects_lexical_exponent_outside_bounds(self):
        for raw in (b'{"z":1.2300e1000001}', b'{"z":1.2300e-1000001}'):
            with self.assertRaises(ValueError):
                oracle.canonicalize_bytes(raw)

    def test_python_canonicalizer_accepts_compensated_lexical_boundaries(self):
        self.assertTrue(oracle.canonicalize_bytes(b'{"z":1.2300e-1000000}').startswith(b'{"z":0.'))
        self.assertTrue(oracle.canonicalize_bytes(b'{"z":1.2300e1000000}').endswith(b'0}'))

    def test_python_canonicalizer_rejects_unpaired_surrogates_contractually(self):
        for raw in (b'{"z":"\\uD800"}', b'{"z":"\\uDC00"}'):
            with self.assertRaises(ValueError):
                oracle.canonicalize_bytes(raw)

    def test_oracle_rejects_non_string_schedule_days_without_throwing(self):
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["schedules"][0]["days"] = [{}]
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy))

    def test_oracle_rejects_non_array_or_duplicate_allowed_window_days(self):
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["app_policies"][0]["allowed_windows"] = "not-an-array"
        self.assertTrue(errors("valid-policy-snapshot.json", policy))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["app_policies"][0]["allowed_windows"] = [{"days": ["MON", "MON"], "from": "10:00", "to": "11:00"}]
        self.assertTrue(errors("valid-policy-snapshot.json", policy))

    def test_python_canonicalizer_preserves_small_fraction(self):
        self.assertEqual("{\"m\":-0.001,\"n\":0.001}", oracle.canonical({"m": oracle.Decimal("-0.001"), "n": oracle.Decimal("0.001")}))

    def test_oracle_rejects_policy_limits_windows_and_outbox_timestamp(self):
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["category_limits"][0]["minutes"] = 1441
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["app_policies"][0]["allowed_windows"] = [{"days": ["MON"], "from": "99:99", "to": "10:00"}]
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy))
        outbox = load("valid-time-request-outbox.json")
        outbox["payload"]["next_attempt_at"] = "not-a-time"
        self.assertTrue(errors("valid-time-request-outbox.json", outbox))

    def test_oracle_rejects_non_ascii_integrity_text_and_invalid_schedule_time(self):
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["agent_version"] = "é"
        self.assertIn("agent_version", errors("valid-integrity-evidence.json", evidence))
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["signer_summary"] = "签名"
        self.assertIn("signer_summary", errors("valid-integrity-evidence.json", evidence))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["schedules"][0]["from"] = "99:99"
        self.assertTrue(any("time" in item for item in errors("valid-policy-snapshot.json", policy)))
    def test_oracle_accepts_complete_enum_values(self):
        cases = [
            ("valid-set-protected-account-response.json", "status", "pending"),
            ("valid-set-protected-account-response.json", "activation_state", "not_configured"),
            ("valid-integrity-verdict.json", "verdict", "revoked"),
        ]
        for fixture, member, value in cases:
            doc = load(fixture)
            doc["payload"][member] = value
            self.assertEqual(errors(fixture, doc), [], (fixture, member, errors(fixture, doc)))

    def test_oracle_rejects_invalid_identity_time_hash_and_nested_shape(self):
        doc = load("valid-create-time-request.json")
        doc["correlation_id"] = "00000000-0000-0000-0000-000000000000"
        self.assertIn("uuid:correlation_id", errors("valid-create-time-request.json", doc))

        doc = load("valid-create-time-request.json")
        doc["payload"]["created_at"] = "2026-08-27T11:59:59Z"
        self.assertIn("age:created_at", errors("valid-create-time-request.json", doc))

        doc = load("valid-policy-snapshot.json")
        doc["payload"]["snapshot_hash"] = "0" * 64
        self.assertTrue(any("hash" in x for x in errors("valid-policy-snapshot.json", doc)))
        doc["payload"]["unknown"] = True
        self.assertTrue(any(x.startswith("unknown:") for x in errors("valid-policy-snapshot.json", doc)))

        doc = load("valid-time-request-outbox.json")
        doc["payload"]["wire_request"]["origin"] = "invalid"
        self.assertTrue(any("wire_request" in x or "origin" in x for x in errors("valid-time-request-outbox.json", doc)))

    def test_oracle_rejects_unknown_and_zero_hint_identity(self):
        doc = load("valid-realtime-hint.json")
        doc["payload"]["correlation_hint"] = "00000000-0000-0000-0000-000000000000"
        self.assertTrue(any("uuid" in x or "hint" in x for x in errors("valid-realtime-hint.json", doc)), "expected uuid:correlation_hint")

    def test_oracle_accepts_lowercase_nonzero_uuidv7(self):
        doc = load("valid-realtime-hint.json")
        doc["payload"]["correlation_hint"] = "00000000-0000-7000-8000-000000000011"
        self.assertEqual(errors("valid-realtime-hint.json", doc), [])

    def test_oracle_accepts_uuid_with_rfc_variant_zero(self):
        doc = load("valid-realtime-hint.json")
        doc["correlation_id"] = "00000000-0000-7000-0000-000000000010"
        doc["payload"]["correlation_hint"] = "00000000-0000-7000-0000-000000000011"
        self.assertEqual(errors("valid-realtime-hint.json", doc), [])

    def test_oracle_rejects_authority_scalar_boundaries(self):
        outbox = load("valid-time-request-outbox.json")
        outbox["payload"]["attempt_count"] = -1
        self.assertTrue(any("attempt_count" in x for x in errors("valid-time-request-outbox.json", outbox)))
        outbox["payload"]["attempt_count"] = 4294967296
        self.assertTrue(any("attempt_count" in x for x in errors("valid-time-request-outbox.json", outbox)))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["app_policies"][0]["state"] = "limited"
        policy["payload"]["app_policies"][0]["daily_limit_minutes"] = True
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["grants"][0]["scope"] = ""
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy))

    def test_oracle_rejects_payload_canonical_size_not_only_envelope_size(self):
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["extensions"] = {"x-pad": "a" * 50000}
        rehash_policy(policy)
        self.assertIn("payload_size", errors("valid-policy-snapshot.json", policy))
        account = load("valid-set-protected-account.json")
        account["payload"]["extensions"] = {"x-pad": "a" * 50000}
        self.assertIn("payload_size", errors("valid-set-protected-account.json", account))

    def test_oracle_rejects_non_ascii_schedule_allow_list(self):
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["schedules"][1]["allow_list"] = ["应用"]
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy))

    def test_oracle_rejects_null_extensions_empty_days_and_non_ascii_category(self):
        account = load("valid-set-protected-account.json")
        account["payload"]["extensions"] = None
        self.assertTrue(errors("valid-set-protected-account.json", account))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["schedules"][0]["days"] = []
        self.assertTrue(errors("valid-policy-snapshot.json", policy))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["category_limits"][0]["category"] = "juegos-é"
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy))

    def test_oracle_rejects_out_of_range_limited_policy(self):
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["app_policies"][1]["daily_limit_minutes"] = 1441
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy), "expected value:app_policies/1/daily_limit_minutes")

    def test_oracle_accepts_safe_extensions_and_rejects_authority_names(self):
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["extensions"] = {"x-safe": {"display_hint": "safe"}}
        self.assertEqual(errors("valid-integrity-evidence.json", evidence), [])
        evidence["payload"]["extensions"] = {"x-safe": "x-authority"}
        self.assertTrue(errors("valid-integrity-evidence.json", evidence), "extensions: expected rejection")

    def test_oracle_rejects_all_authority_scalar_boundaries(self):
        outbox = load("valid-time-request-outbox.json")
        for value in (-1, 4294967296, True):
            outbox["payload"]["attempt_count"] = value
            self.assertTrue(errors("valid-time-request-outbox.json", outbox))
        policy = load("valid-policy-snapshot.json")
        for value in ("应用", ""):
            policy["payload"]["grants"][0]["scope"] = value
            rehash_policy(policy)
            self.assertTrue(errors("valid-policy-snapshot.json", policy), "expected value:grants/0")

    def test_oracle_is_total_and_enforces_authority_limits(self):
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["schedules"][0]["days"] = [{}]
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["schedules"][0]["days"] = ["MON", "MON"]
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["schedules"][0]["id"] = "应用"
        rehash_policy(policy)
        self.assertTrue(any("schedules/0/id" in item for item in errors("valid-policy-snapshot.json", policy)))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["app_policies"][0]["allowed_windows"] = "not-an-array"
        self.assertTrue(errors("valid-policy-snapshot.json", policy))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["extensions"] = {"x-authority": "safe"}
        rehash_policy(policy)
        self.assertTrue(errors("valid-policy-snapshot.json", policy), "extensions: expected rejection")
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["extensions"] = {"x-safe": {"x-authority": "safe"}}
        self.assertTrue(errors("valid-integrity-evidence.json", evidence))
        outbox = load("valid-time-request-outbox.json")
        outbox["payload"]["attempt_count"] = -1
        self.assertTrue(errors("valid-time-request-outbox.json", outbox))
        outbox["payload"]["attempt_count"] = 4294967296
        self.assertTrue(errors("valid-time-request-outbox.json", outbox))

    def test_oracle_rejects_impossible_timestamps_and_payload_overflow(self):
        verdict = load("valid-integrity-verdict.json")
        verdict["payload"]["evaluated_at"] = "2026-99-99T12:00:00Z"
        self.assertTrue(errors("valid-integrity-verdict.json", verdict))
        policy = load("valid-policy-snapshot.json")
        policy["payload"]["extensions"] = {"x-pad": "a" * 50000}
        rehash_policy(policy)
        self.assertIn("payload_size", errors("valid-policy-snapshot.json", policy))


    def test_oracle_rejects_recursive_secret_bearing_extensions(self):
        evidence = load("valid-integrity-evidence.json")
        for value in (
            {"x-safe": {"password": "secret"}},
            {"x-safe": [{"passwd": "secret"}]},
            {"x-safe": {"api_key": "secret"}},
            {"x-safe": {"api-key": "opaque"}},
            {"x-safe": [{"private.key": "opaque"}]},
        ):
            evidence["payload"]["extensions"] = value
            self.assertTrue(errors("valid-integrity-evidence.json", evidence), "expected extensions: rejection")

    def test_oracle_accepts_safe_extension_aliases_without_secret_tokens(self):
        evidence = load("valid-integrity-evidence.json")
        for value in ({"x-display": "label"}, {"x-safe": {"keynote": "safe"}}):
            evidence["payload"]["extensions"] = value
            self.assertEqual(errors("valid-integrity-evidence.json", evidence), [])

    def test_oracle_rejects_jwt_and_role_extension_names_but_allows_safe_nested_content(self):
        evidence = load("valid-integrity-evidence.json")
        evidence["payload"]["extensions"] = {"x-safe": {"display": "safe", "description": "normal role information"}}
        self.assertEqual(errors("valid-integrity-evidence.json", evidence), [])
        for name in ("jwt", "role"):
            evidence["payload"]["extensions"] = {"x-safe": {name: "opaque"}}
            self.assertTrue(errors("valid-integrity-evidence.json", evidence))

    def test_oracle_accepts_nonzero_lowercase_uuid_without_variant_constraint(self):
        hint = load("valid-realtime-hint.json")
        hint["correlation_id"] = "00000000-0000-7000-0000-000000000010"
        hint["payload"]["correlation_hint"] = "00000000-0000-7000-0000-000000000011"
        self.assertEqual(errors("valid-realtime-hint.json", hint), [])

if __name__ == "__main__":
    unittest.main()
