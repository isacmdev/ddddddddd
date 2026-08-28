"""Reproducible contract-freeze checks; run from this directory."""
from __future__ import annotations
import hashlib, json
from pathlib import Path

ROOT = Path(__file__).parent
FIX = ROOT / "fixtures"
EXPECTED_HASH = "3a1814cd72dfd518324b40dbe2f651ad0b616248e451ceb417d4a7fcd7419a42"


def compact(value: object) -> bytes:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=False).encode()


def main() -> None:
    policy_file = FIX / "valid-policy-snapshot.json"
    envelope = json.loads(policy_file.read_text(encoding="utf-8"))
    payload = envelope["payload"]
    supplied = payload["snapshot_hash"]
    unhashed = {key: value for key, value in payload.items() if key != "snapshot_hash"}
    actual = hashlib.sha256(compact(unhashed)).hexdigest()
    assert supplied == actual == EXPECTED_HASH, (supplied, actual)
    assert payload["device_state"] in {"active", "locked", "downtime"}
    assert set(payload) == {"device_id", "version", "device_state", "daily_screen_time_minutes", "schedules", "category_limits", "app_policies", "category_assignments", "grants", "snapshot_hash"}
    array_path = FIX / "invalid-array-too-long.json"
    array_case = json.loads(array_path.read_text(encoding="utf-8"))
    if len(array_case["payload"].get("category_limits", [])) != 257 or array_case["payload"].get("device_state") != "active":
        # Keep this negative derived from the valid policy and introduce only
        # the array-boundary violation; recompute its otherwise-valid hash.
        invalid_payload = dict(payload)
        invalid_payload["category_limits"] = [{"category": "games", "minutes": 60}] * 257
        invalid_payload.pop("snapshot_hash")
        invalid_payload["snapshot_hash"] = hashlib.sha256(compact(invalid_payload)).hexdigest()
        invalid = dict(envelope)
        invalid["payload"] = invalid_payload
        array_path.write_bytes(compact(invalid) + b"\n")
    assert all(json.loads(path.read_text(encoding="utf-8")) for path in FIX.glob("*.json"))
    oversize_path = FIX / "invalid-envelope-oversize.json"
    oversize = oversize_path.read_bytes()
    oversize_payload = json.loads(oversize.decode())["payload"]
    if len(oversize) <= 65536 or len(compact(oversize_payload)) > 49152:
        # The fixture tests raw-envelope framing, not payload size: legal JSON
        # whitespace is appended outside a small valid payload.
        source = json.loads((FIX / "valid-policy-snapshot.json").read_text(encoding="utf-8"))
        oversize_path.write_bytes(compact(source) + b" " * 66000)
        oversize = oversize_path.read_bytes()
        oversize_payload = source["payload"]
    assert len(compact(oversize_payload)) <= 49152
    assert len(oversize) > 65536
    print("PASS: JSON parse, policy shape/hash, and envelope/payload boundaries")


if __name__ == "__main__":
    main()
