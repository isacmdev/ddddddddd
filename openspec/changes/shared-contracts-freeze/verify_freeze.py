"""Independent, read-only oracle for the frozen fixture corpus."""
from __future__ import annotations

import hashlib
import json
import re
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from decimal import Decimal

if hasattr(sys, "set_int_max_str_digits"):
    sys.set_int_max_str_digits(0)

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).parents[3] / "tests" / "contract-freeze"))
from canonicalize import canonicalize, canonicalize_bytes

# Compatibility name used by the adversarial test harness.
canonical = canonicalize

FIX = Path(__file__).parent / "fixtures"
EXPECTED_FILES = 28
CLOCK = datetime.fromisoformat("2026-08-28T12:00:00+00:00")
UUID_RE = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
TS_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")
REASON_RE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")
EXT_RE = re.compile(r"^x-[a-z0-9][a-z0-9._-]{0,63}$")
FORBIDDEN_TOKENS = ("authority", "identity", "secret", "credential", "token", "grant", "verdict", "password", "passwd", "api_key", "apikey", "private_key", "access_key")
FORBIDDEN_TOKENS += ("jwt", "role")

def _secret_token(value: str) -> bool:
    normalized = re.sub(r"[-_.]+", "_", value.lower())
    return any(
        normalized == token
        or normalized.startswith(f"x_{token}")
        for token in FORBIDDEN_TOKENS
    )

REQUIRED = {
    "set_protected_account.request": {"operation_id", "username", "sid", "requested_at"},
    "set_protected_account.response": {"operation_id", "status", "activation_state", "correlation_id", "reason_code"},
    "runtime_activation.state": {"activation_state", "reason_code", "observed_at", "state_version"},
    "create_time_request": {"request_id", "scope", "minutes", "origin", "policy_version", "device_id", "created_at"},
    "time_request.outbox": {"request_id", "device_generation", "wire_request", "outbox_state", "attempt_count", "next_attempt_at"},
    "policy.snapshot": {"device_id", "version", "device_state", "daily_screen_time_minutes", "schedules", "category_limits", "app_policies", "category_assignments", "grants", "snapshot_hash"},
    "integrity.evidence": {"evidence_id", "device_generation", "agent_version", "binary_sha256", "signature_result", "signer_summary", "collected_at", "evidence_schema_version"},
    "integrity.verdict": {"verdict", "evidence_id", "evaluated_at", "verdict_version", "reason_code"},
    "wns.hint": set(), "realtime.hint": set(),
}
ALLOWED = {key: values | {"extensions"} for key, values in REQUIRED.items()}
ALLOWED["create_time_request"] |= {"reason"}
ALLOWED["wns.hint"] = ALLOWED["realtime.hint"] = {"hint_type", "correlation_hint", "extensions"}
MESSAGE_TYPES = set(REQUIRED)
UUID_FIELDS = {
    "set_protected_account.request": {"operation_id"},
    "set_protected_account.response": {"operation_id", "correlation_id"},
    "create_time_request": {"request_id", "device_id"},
    "time_request.outbox": {"request_id", "device_generation"},
    "policy.snapshot": {"device_id"},
    "integrity.evidence": {"evidence_id", "device_generation"},
    "integrity.verdict": {"evidence_id"},
}
INT_FIELDS = {
    "create_time_request": {"policy_version"}, "runtime_activation.state": {"state_version"},
    "policy.snapshot": {"version"}, "integrity.verdict": {"verdict_version"},
    "time_request.outbox": {"attempt_count"},
}

def walk(v: Any):
    yield v
    if isinstance(v, dict):
        for x in v.values(): yield from walk(x)
    elif isinstance(v, list):
        for x in v: yield from walk(x)

def depth(v: Any, n=1): return max([n] + [depth(x,n+1) for x in (v.values() if isinstance(v,dict) else v if isinstance(v,list) else [])])
def one_cause(name: str, d: Any) -> str | None:
    if name == "invalid-major.json" and d.get("version") != 1: return "major"
    if name == "invalid-enum.json" and d.get("payload",{}).get("origin") == "other": return "enum"
    if name == "invalid-missing-required.json" and "operation_id" not in d.get("payload",{}): return "missing_required"
    if name == "invalid-unknown-required-member.json" and "required_by_newer_peer" in d.get("payload",{}): return "unknown_member"
    if name == "invalid-envelope-oversize.json" and len((FIX/name).read_bytes()) > MAX_ENVELOPE: return "envelope_oversize"
    p=d.get("payload",{}); reason=p.get("reason","")
    if name == "invalid-reason-too-long.json" and len(reason.encode()) == 257: return "reason_257_bytes"
    if name == "invalid-depth.json" and depth(d) >= 17: return "depth_17"
    if name == "invalid-array-too-long.json" and any(isinstance(x,list) and len(x)==257 for x in walk(d)): return "array_257"
    if name == "invalid-string-too-long.json" and any(isinstance(x,str) and len(x.encode())==4097 for x in walk(d)): return "string_4097_bytes"
    if name == "invalid-extensions.json" and "bad-key" in d.get("payload",{}).get("extensions",{}): return "extension_key"
    if name == "invalid-timestamp-future.json" and isinstance(p.get("created_at"),str): return "future_301_seconds"
    if name == "invalid-minutes-too-high.json" and p.get("minutes") == 181: return "minutes_181"
    if name == "invalid-hint-member.json" and "policy" in p: return "hint_unknown_member"
    if name == "invalid-hint-1025.json" and len((FIX/name).read_bytes()) == 1025: return "hint_1025_bytes"
    return None

def _parsed_number(text: str) -> Decimal:
    exponent = text.lower().partition("e")[2]
    if exponent and not -1_000_000 <= int(exponent) <= 1_000_000:
        raise ValueError("number exponent is outside the supported canonical range")
    return Decimal(text)


def reject_duplicates(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate:{key}")
        result[key] = value
    return result


def reject_constant(value: str):
    raise ValueError(f"non-finite JSON number: {value}")


def load(path: Path):
    raw = path.read_bytes()
    text = raw.decode("utf-8", "strict")
    if text.startswith("\ufeff"):
        raise ValueError("bom")
    return raw, json.loads(text, object_pairs_hook=reject_duplicates, parse_int=int, parse_float=_parsed_number, parse_constant=reject_constant)


def walk(value, path=""):
    yield path, value
    if isinstance(value, dict):
        for key, child in value.items():
            yield from walk(child, f"{path}/{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from walk(child, f"{path}/{index}")


def depth(value):
    if isinstance(value, dict):
        return 1 + max((depth(v) for v in value.values()), default=0)
    if isinstance(value, list):
        return 1 + max((depth(v) for v in value), default=0)
    return 0



def _uuid(value):
    return isinstance(value, str) and bool(UUID_RE.fullmatch(value)) and set(value.replace("-", "")) != {"0"}


def _timestamp(value, field, windowed, errors):
    if not isinstance(value, str) or not TS_RE.fullmatch(value):
        errors.append(f"timestamp:{field}")
        return
    try:
        instant = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except (TypeError, ValueError, OverflowError):
        errors.append(f"timestamp:{field}")
        return
    if windowed:
        if instant > CLOCK + timedelta(seconds=300):
            errors.append(f"future:{field}")
        if instant < CLOCK - timedelta(hours=24):
            errors.append(f"age:{field}")


def _extensions(value, path, errors):
    if value is None:
        errors.append(f"extensions:{path}")
        return
    def unsafe(item):
        if isinstance(item, dict):
            return any(_secret_token(key) or unsafe(child) for key, child in item.items())
        if isinstance(item, list):
            return any(unsafe(child) for child in item)
        return isinstance(item, str) and _secret_token(item)
    try:
        canonical_size = len(canonicalize(value).encode())
    except (TypeError, ValueError, OverflowError):
        canonical_size = 2**31 - 1
    if (not isinstance(value, dict) or len(value) > 8
            or canonical_size > 8192
            or any(not EXT_RE.fullmatch(k) or _secret_token(k) for k in value)
            or unsafe(value)):
        errors.append(f"extensions:{path}")


def _walk_extensions(value, path, errors):
    if isinstance(value, dict):
        for key, child in value.items():
            child_path = f"{path}/{key}"
            if key == "extensions":
                _extensions(child, child_path, errors)
            _walk_extensions(child, child_path, errors)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            _walk_extensions(child, f"{path}/{index}", errors)


def _nested_policy(payload, errors):
    shapes = {
        "schedules": {"id", "days", "from", "to", "action"},
        "category_limits": {"category", "minutes"},
        "app_policies": {"package_name", "state", "daily_limit_minutes", "category", "allowed_windows"},
        "grants": {"id", "request_id", "scope", "minutes", "granted_at", "expires_at", "source"},
    }
    for collection, required in shapes.items():
        values = payload.get(collection)
        if not isinstance(values, list):
            errors.append(f"shape:{collection}")
            continue
        for index, item in enumerate(values):
            if not isinstance(item, dict):
                errors.append(f"shape:{collection}/{index}")
                continue
            unknown = set(item) - required - ({"allow_list"} if collection == "schedules" else set())
            if unknown:
                errors.extend(f"unknown:{collection}/{index}/{key}" for key in sorted(unknown))
            if not required <= set(item) and collection in {"category_limits", "grants"}:
                errors.extend(f"required:{collection}/{index}/{key}" for key in sorted(required - set(item)))
            if collection == "category_limits" and (not isinstance(item.get("category"), str) or not 1 <= len(item["category"].encode()) <= 64 or any(ord(c) > 127 for c in item.get("category", "")) or isinstance(item.get("minutes"), bool) or not isinstance(item.get("minutes"), int) or not 0 <= item["minutes"] <= 1440):
                errors.append(f"value:{collection}/{index}")
            if collection == "schedules":
                if not required <= set(item):
                    errors.extend(f"required:{collection}/{index}/{key}" for key in sorted(required - set(item)))
                days = item.get("days", [])
                if (not isinstance(item.get("id"), str) or not 1 <= len(item["id"].encode()) <= 64
                        or any(ord(c) > 127 for c in item["id"])):
                    errors.append(f"value:{collection}/{index}/id")
                if (not isinstance(days, list) or not days or any(not isinstance(day, str) for day in days)
                        or len({day for day in days if isinstance(day, str)}) != len(days)
                        or any(day not in {"MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"} for day in days if isinstance(day, str))):
                    errors.append(f"value:{collection}/{index}/days")
                if (not isinstance(item.get("from"), str) or not re.fullmatch(r"(?:[01]\d|2[0-3]):[0-5]\d", item["from"])
                        or not isinstance(item.get("to"), str) or not re.fullmatch(r"(?:[01]\d|2[0-3]):[0-5]\d", item["to"])):
                    errors.append(f"value:{collection}/{index}/time")
                allow_list = item.get("allow_list", [])
                if (not isinstance(allow_list, list) or any(not isinstance(value, str) or not value or any(ord(c) > 127 for c in value) for value in allow_list)
                        or item.get("action") not in {"lock", "allow_only"}
                        or (item.get("action") == "allow_only" and not allow_list)):
                    errors.append(f"value:{collection}/{index}/action")
            if collection == "grants":
                scope = item.get("scope")
                if (item.get("source") not in {"extra_time", "reward", "manual"} or not isinstance(item.get("minutes"), int)
                        or isinstance(item.get("minutes"), bool) or not 1 <= item.get("minutes", 0) <= 180
                        or not isinstance(scope, str) or not scope or any(ord(c) > 127 for c in scope)):
                    errors.append(f"value:{collection}/{index}")
                _timestamp(item.get("granted_at"), "granted_at", False, errors)
                _timestamp(item.get("expires_at"), "expires_at", False, errors)
                if isinstance(item.get("granted_at"), str) and isinstance(item.get("expires_at"), str) and item["expires_at"] <= item["granted_at"]:
                    errors.append(f"value:{collection}/{index}/expires_at")
            if collection == "app_policies":
                if item.get("state") not in {"allowed", "blocked", "limited", "always_allowed"}:
                    errors.append(f"value:{collection}/{index}/state")
                if (not isinstance(item.get("package_name"), str) or not item["package_name"] or any(ord(c) > 127 for c in item["package_name"])):
                    errors.append(f"value:{collection}/{index}/package_name")
                if ("category" in item and (not isinstance(item.get("category"), str) or not 1 <= len(item["category"].encode()) <= 64 or any(ord(c) > 127 for c in item["category"]))
                        or "daily_limit_minutes" in item and (isinstance(item.get("daily_limit_minutes"), bool)
                        or not isinstance(item.get("daily_limit_minutes"), int) or not 0 <= item.get("daily_limit_minutes", -1) <= 1440)
                        or item.get("state") == "limited" and "daily_limit_minutes" not in item):
                    errors.append(f"required:{collection}/{index}/daily_limit_minutes")
                windows = item.get("allowed_windows", [])
                if "allowed_windows" in item and not isinstance(windows, list):
                    errors.append(f"shape:{collection}/{index}/allowed_windows")
                else:
                    for wi, window in enumerate(windows):
                        if (not isinstance(window, dict) or set(window) != {"days", "from", "to"}
                                or not isinstance(window.get("days"), list) or not window["days"]
                                or any(not isinstance(day, str) for day in window["days"])
                                or len({day for day in window["days"] if isinstance(day, str)}) != len(window["days"])
                                or any(day not in {"MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"} for day in window["days"] if isinstance(day, str))
                                or not all(isinstance(value, str) and re.fullmatch(r"(?:[01]\d|2[0-3]):[0-5]\d", value) for value in (window.get("from"), window.get("to")))):
                                errors.append(f"value:{collection}/{index}/allowed_windows/{wi}")


def check_message(name, doc, raw_len=0):
    errors = []
    if not isinstance(doc, dict):
        return ["envelope"]
    envelope_required = {"contract", "version", "message_type", "correlation_id", "payload"}
    errors.extend(f"required:{key}" for key in envelope_required - set(doc))
    errors.extend(f"unknown:{key}" for key in set(doc) - envelope_required - {"extensions"})
    if doc.get("contract") != "control-parental.windows": errors.append("contract")
    if doc.get("version") != 1 or isinstance(doc.get("version"), bool) or not isinstance(doc.get("version"), int): errors.append("version")
    message_type = doc.get("message_type")
    if message_type not in MESSAGE_TYPES: errors.append("message_type")
    if not _uuid(doc.get("correlation_id")): errors.append("uuid:correlation_id")
    payload = doc.get("payload")
    if not isinstance(payload, dict):
        errors.append("payload")
        return errors
    _walk_extensions(doc, "", errors)
    errors.extend(f"required:{key}" for key in REQUIRED.get(message_type, set()) - set(payload))
    errors.extend(f"unknown:{key}" for key in set(payload) - ALLOWED.get(message_type, set()))

    if message_type in {"wns.hint", "realtime.hint"}:
        if not ("hint_type" in payload or "correlation_hint" in payload): errors.append("hint_empty")
        if "hint_type" in payload and (not isinstance(payload["hint_type"], str) or not 1 <= len(payload["hint_type"].encode()) <= 32 or any(ord(c) > 127 for c in payload["hint_type"])): errors.append("hint_type")
        if "correlation_hint" in payload and not _uuid(payload["correlation_hint"]): errors.append("uuid:correlation_hint")
    for field in UUID_FIELDS.get(message_type, set()):
        if field in payload and not _uuid(payload[field]): errors.append(f"uuid:{field}")
    for field in INT_FIELDS.get(message_type, set()):
        upper = 4294967295 if field == "attempt_count" else 9223372036854775807
        if field in payload and (isinstance(payload[field], bool) or not isinstance(payload[field], int) or not 0 <= payload[field] <= upper): errors.append(f"uint32:{field}" if field == "attempt_count" else f"int64:{field}")
    if message_type == "set_protected_account.response":
        if payload.get("status") not in {"accepted", "pending", "rejected", "failed"}: errors.append("status")
        if payload.get("activation_state") not in {"not_configured", "activating", "active", "degraded", "failed"}: errors.append("activation_state")
    if message_type == "runtime_activation.state" and payload.get("activation_state") not in {"not_configured", "activating", "active", "degraded", "failed"}: errors.append("activation_state")
    if message_type == "integrity.evidence" and payload.get("signature_result") not in {"valid", "invalid", "unknown"}: errors.append("signature_result")
    if message_type == "integrity.verdict" and payload.get("verdict") not in {"trust", "revoked", "unknown"}: errors.append("verdict")
    if message_type == "create_time_request":
        if not isinstance(payload.get("minutes"), int) or isinstance(payload.get("minutes"), bool) or not 1 <= payload["minutes"] <= 180: errors.append("minutes")
        if payload.get("origin") not in {"status_page", "overlay"}: errors.append("origin")
        if not isinstance(payload.get("scope"), str) or not 1 <= len(payload["scope"].encode()) <= 64 or any(ord(c) > 127 for c in payload.get("scope", "")): errors.append("scope")
        if "reason" in payload and (not isinstance(payload["reason"], str) or not 1 <= len(payload["reason"].encode()) <= 256): errors.append("reason")
    if message_type == "set_protected_account.request":
        if not isinstance(payload.get("username"), str) or not 1 <= len(payload["username"].encode()) <= 256: errors.append("username")
        if (not isinstance(payload.get("sid"), str) or len(payload.get("sid", "").encode()) > 184
                or not re.fullmatch(r"S-1-(?:0|[1-5])(?:-(?:0|[1-9]\d{0,17})){1,15}", payload.get("sid", ""))): errors.append("sid")
    if message_type == "integrity.evidence":
        if not isinstance(payload.get("binary_sha256"), str) or not re.fullmatch(r"[0-9a-f]{64}", payload.get("binary_sha256", "")): errors.append("binary_sha256")
        if (not isinstance(payload.get("agent_version"), str) or not 1 <= len(payload["agent_version"].encode()) <= 128
                or any(ord(c) > 127 for c in payload["agent_version"])): errors.append("agent_version")
        if (not isinstance(payload.get("signer_summary"), str) or not 1 <= len(payload["signer_summary"].encode()) <= 256
                or any(ord(c) > 127 for c in payload["signer_summary"])): errors.append("signer_summary")
        if (isinstance(payload.get("evidence_schema_version"), bool)
                or not isinstance(payload.get("evidence_schema_version"), int)
                or not 1 <= payload["evidence_schema_version"] <= 4294967295):
            errors.append("evidence_schema_version")
    if message_type == "time_request.outbox":
        if (isinstance(payload.get("attempt_count"), bool) or not isinstance(payload.get("attempt_count"), int)
                or not 0 <= payload.get("attempt_count", -1) <= 4294967295):
            errors.append("attempt_count")
        if payload.get("outbox_state") not in {"queued", "pending", "approved", "denied", "failed", "applied"}: errors.append("outbox_state")
        if payload.get("next_attempt_at") is not None: _timestamp(payload.get("next_attempt_at"), "next_attempt_at", False, errors)
        nested = payload.get("wire_request")
        if not isinstance(nested, dict): errors.append("shape:wire_request")
        else:
            nested_errors = check_message("nested-create_time_request", {"contract": "control-parental.windows", "version": 1, "message_type": "create_time_request", "correlation_id": doc.get("correlation_id"), "payload": nested})
            errors.extend(f"nested:{item}" for item in nested_errors if not item.startswith("unknown:extensions"))
            if nested.get("request_id") != payload.get("request_id"):
                errors.append("identity_mismatch:wire_request/request_id")
    if message_type == "policy.snapshot":
        if payload.get("device_state") not in {"active", "locked", "downtime"}: errors.append("device_state")
        if (isinstance(payload.get("daily_screen_time_minutes"), bool) or not isinstance(payload.get("daily_screen_time_minutes"), int) or not 0 <= payload.get("daily_screen_time_minutes", -1) <= 1440): errors.append("daily_screen_time_minutes")
        snapshot = dict(payload)
        supplied = snapshot.pop("snapshot_hash", None)
        _nested_policy(payload, errors)
        if (not isinstance(payload.get("category_assignments"), dict)
                or any(not isinstance(k, str) or not 1 <= len(k.encode()) <= 64 or any(ord(c) > 127 for c in k)
                       or not isinstance(v, str) or not 1 <= len(v.encode()) <= 64 or any(ord(c) > 127 for c in v)
                       for k, v in payload.get("category_assignments", {}).items())):
            errors.append("category_assignments")
        has_oversized_collection = any(isinstance(payload.get(key), list) and len(payload[key]) > 256 for key in ("schedules", "category_limits", "app_policies", "grants"))
        if not isinstance(supplied, str) or not re.fullmatch(r"[0-9a-f]{64}", supplied): errors.append("hash_format")
        elif not has_oversized_collection and len(canonical(snapshot).encode()) <= 49152:
            actual = hashlib.sha256(canonical(snapshot).encode()).hexdigest()
            if supplied != actual: errors.append("hash")
        else: errors.append("payload_size")
    for field in {"requested_at", "created_at", "collected_at"} & set(payload): _timestamp(payload[field], field, True, errors)
    for field in {"observed_at", "evaluated_at"} & set(payload):
        if payload[field] is not None: _timestamp(payload[field], field, False, errors)
    for field in {"reason_code"} & set(payload):
        if not isinstance(payload[field], str) or not REASON_RE.fullmatch(payload[field]): errors.append(field)
    if raw_len > (1024 if message_type in {"wns.hint", "realtime.hint"} else 65536): errors.append("size")
    try:
        canonical_payload = canonical(payload).encode()
        if len(canonical_payload) > 49152: errors.append("payload_size")
    except (TypeError, ValueError, OverflowError):
        errors.append("payload_size")
    for path, value in walk(doc):
        if isinstance(value, str) and len(value.encode()) > 4096: errors.append(f"string:{path}")
        if isinstance(value, list) and len(value) > 256: errors.append(f"array:{path}")
    if depth(doc) > 16: errors.append("depth")
    if name.startswith("valid-"):
        return errors
    if name.startswith("invalid-") and not errors:
        return ["accepted_negative"]
    return errors


def main() -> int:
    errors = []
    files = sorted(FIX.glob("*.json"))
    if len(files) != EXPECTED_FILES: errors.append(f"file_count:{len(files)}")
    try:
        _, manifest = load(FIX / "fixture-manifest.json")
    except Exception as ex:
        errors.append(f"manifest:{ex}"); manifest = {}
    references = [item for values in manifest.get("fixtures", {}).values() for item in values] if isinstance(manifest, dict) else []
    actual = {path.name for path in files if path.name != "fixture-manifest.json"}
    if set(references) != actual: errors.append("manifest_set")
    for path in files:
        if path.name == "fixture-manifest.json": continue
        try:
            raw, doc = load(path)
            meta = manifest.get("metadata", {}).get(path.name, {})
            if meta and (meta.get("sha256") != hashlib.sha256(raw).hexdigest() or meta.get("bytes") != len(raw)): errors.append(f"stale_metadata:{path.name}")
            fixture_errors = check_message(path.name, doc, len(raw))
            if path.name.startswith("invalid-"):
                if not fixture_errors: errors.append(f"{path.name}:expected_rejection")
            else: errors.extend(f"{path.name}:{item}" for item in fixture_errors)
        except Exception as ex: errors.append(f"{path.name}:{ex}")
    if errors:
        print("FAIL\n" + "\n".join(errors)); return 1
    print(f"PASS: read-only oracle validated {len(files)} JSON files and manifest"); return 0


if __name__ == "__main__": sys.exit(main())
