"""Portable canonical JSON implementation shared by fixture tooling.

Objects are ordered by UTF-16 code units (StringComparer.Ordinal semantics),
arrays retain order, strings use deterministic JSON escaping, and
integral-equivalent decimal numbers are normalized.
"""
from __future__ import annotations
import json
from decimal import Decimal


def _number(value: int | Decimal) -> str:
    decimal = value if isinstance(value, Decimal) else Decimal(value)
    if not decimal.is_finite():
        raise ValueError("non-finite numbers are not valid canonical JSON")
    if decimal == 0:
        return "0"
    # Keep the exact coefficient; Decimal.normalize() rounds under the ambient context.
    normalized = format(decimal, "f")

    if "." in normalized:
        normalized = normalized.rstrip("0").rstrip(".")
    return normalized


def _string(value: str) -> str:
    if any(0xD800 <= ord(c) <= 0xDFFF for c in value):
        raise ValueError("unpaired UTF-16 surrogate is not valid canonical JSON")
    rendered = json.dumps(value, ensure_ascii=False, separators=(",", ":"))
    return "".join(
        f"\\u{((ord(c) - 0x10000) // 0x400 + 0xD800):04X}\\u{((ord(c) - 0x10000) % 0x400 + 0xDC00):04X}"
        if ord(c) > 0xFFFF else c for c in rendered)


def _parsed_number(text: str) -> Decimal:
    exponent = text.lower().partition("e")[2]
    if exponent and not -1_000_000 <= int(exponent) <= 1_000_000:
        raise ValueError("number exponent is outside the supported canonical range")
    return Decimal(text)


def canonicalize(value: object) -> str:
    if isinstance(value, dict):
        # StringComparer.Ordinal in .NET compares UTF-16 code units.
        keys = sorted(value, key=lambda k: k.encode("utf-16-be", "surrogatepass"))
        return "{" + ",".join(_string(k) + ":" + canonicalize(value[k]) for k in keys) + "}"
    if isinstance(value, list):
        return "[" + ",".join(canonicalize(item) for item in value) + "]"
    if isinstance(value, bool):
        return "true" if value else "false"
    if value is None:
        return "null"
    if isinstance(value, (int, Decimal)) and not isinstance(value, bool):
        return _number(value)
    if isinstance(value, str):
        return _string(value)
    raise TypeError(type(value).__name__)


def canonicalize_bytes(raw: bytes) -> bytes:
    def reject_constant(value: str) -> Decimal:
        raise ValueError(f"non-finite JSON number: {value}")

    def reject_duplicates(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"duplicate JSON member: {key}")
            result[key] = value
        return result

    return canonicalize(
        json.loads(
            raw.decode("utf-8"),
            object_pairs_hook=reject_duplicates,
            parse_int=_parsed_number,
            parse_float=_parsed_number,
            parse_constant=reject_constant,
        )
    ).encode("utf-8")


if __name__ == "__main__":
    import sys
    sys.stdout.buffer.write(canonicalize_bytes(sys.stdin.buffer.read()))
