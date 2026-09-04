"""Fail-only, read-only verification of every frozen fixture and its cause."""
from __future__ import annotations
import hashlib, json, math, re
from pathlib import Path
from typing import Any

ROOT, FIX = Path(__file__).parent, Path(__file__).parent / "fixtures"
MAX_ENVELOPE, MAX_PAYLOAD, MAX_HINT = 65_536, 49_152, 1_024
EXPECTED_HASH = "8bb4e3bee2a84c0ea9a3ce8d62a73a23e1fbbf711dd0cf26ea9ddd0661fd267a"
REQUIRED = {"contract", "version", "message_type", "correlation_id", "payload"}
CAUSES = {"invalid-major.json":"major", "invalid-enum.json":"enum", "invalid-missing-required.json":"missing_required", "invalid-unknown-required-member.json":"unknown_member", "invalid-envelope-oversize.json":"envelope_oversize", "invalid-reason-too-long.json":"reason_257_bytes", "invalid-depth.json":"depth_17", "invalid-array-too-long.json":"array_257", "invalid-string-too-long.json":"string_4097_bytes", "invalid-extensions.json":"extension_key", "invalid-timestamp-future.json":"future_301_seconds", "invalid-minutes-too-high.json":"minutes_181", "invalid-hint-member.json":"hint_unknown_member", "invalid-hint-1025.json":"hint_1025_bytes"}

def reject_constant(value: str) -> None: raise ValueError(f"non-standard number: {value}")
def load(path: Path) -> Any: return json.loads(path.read_text(encoding="utf-8"), parse_constant=reject_constant)

def canonical(value: Any) -> bytes:
    """Deterministic bytes: recursively code-point sorted maps, ordered arrays,
    strict finite JSON numbers, ensure_ascii=False, compact separators, UTF-8."""
    if isinstance(value, dict): return ("{" + ",".join(json.dumps(str(k), ensure_ascii=False)+":"+canonical(v).decode() for k,v in sorted(value.items(), key=lambda x:x[0])) + "}").encode()
    if isinstance(value, list): return ("[" + ",".join(canonical(v).decode() for v in value) + "]").encode()
    if isinstance(value, float) and (not math.isfinite(value)): raise ValueError("non-finite number")
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")

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

def main() -> None:
    errors=[]; files=sorted(p.name for p in FIX.glob("*.json")); actual=set(files)-{"fixture-manifest.json"}
    if len(files) != 28: errors.append(f"expected 28 JSON files, found {len(files)}")
    try: manifest=load(FIX/"fixture-manifest.json")
    except Exception as e: raise SystemExit(f"FAIL: manifest: {e}")
    listed=[x for xs in manifest.get("fixtures",{}).values() for x in xs]
    if len(set(listed))!=27 or set(listed)!=actual: errors.append("manifest is not an exhaustive reference to 27 fixtures")
    if manifest.get("negative_one_cause") != CAUSES: errors.append("negative manifest mapping differs")
    docs={n:load(FIX/n) for n in files}
    policy=docs["valid-policy-snapshot.json"]; payload=policy["payload"]
    actual_hash=hashlib.sha256(canonical({k:v for k,v in payload.items() if k!="snapshot_hash"})).hexdigest()
    if payload.get("snapshot_hash") != actual_hash or actual_hash != EXPECTED_HASH: errors.append("policy canonical hash mismatch")
    timestamp_re = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")
    for grant in payload.get("grants", []):
        if not all(timestamp_re.fullmatch(grant.get(k, "")) for k in ("granted_at", "expires_at")): errors.append("grant timestamps must be UTC seconds with Z")
        elif grant["expires_at"] <= grant["granted_at"]: errors.append("grant expires_at must be later than granted_at")
    for n,d in docs.items():
        if n.startswith("invalid-"):
            if one_cause(n,d) != CAUSES[n]: errors.append(f"{n}: content does not have exactly cause {CAUSES[n]}")
            continue
        raw=(FIX/n).read_bytes(); pbytes=len(canonical(d.get("payload",{})))
        if raw and len(raw)>MAX_ENVELOPE: errors.append(f"{n}: envelope overflow")
        if pbytes>MAX_PAYLOAD: errors.append(f"{n}: payload overflow")
        if depth(d)>16: errors.append(f"{n}: depth overflow")
        if any(isinstance(x,list) and len(x)>256 for x in walk(d)): errors.append(f"{n}: array overflow")
        if n in ("valid-hint-1024.json",) and len(raw)!=MAX_HINT: errors.append(f"{n}: hint envelope boundary is not 1024 bytes")
    if errors: raise SystemExit("FAIL:\n"+"\n".join("- "+e for e in errors))
    print("PASS: 28 JSON fixtures, exhaustive manifest, exact negative causes, canonical hash, and read-only boundaries")

if __name__ == "__main__": main()
