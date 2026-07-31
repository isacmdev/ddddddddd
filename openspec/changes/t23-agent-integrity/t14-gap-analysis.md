# T23/T14 Gap Analysis — Server Verdict Contract

## Status: gap-identified

## Executive Summary

T23 R4 (react to negative server verdict) cannot be implemented with the current T14 contract. T14 defines an integrity **reporting** endpoint (`POST /rest/v1/integrity_reports`) that returns only HTTP success/failure (`bool`). T23 requires the **verdict** of the server's evaluation to be communicated back to the agent. This gap must be resolved with the T14/backend team.

---

## Gap Evidence

### T14 backlog text (line 296)
> "endpoint de **reporte** de integridad (verifica firma/hash, lo alimenta T23)"

Interpretation: The backend **receives** the report and **verifies** it server-side. T23 is the consumer ("lo alimenta").

### T14 backlog text (line 296) — what it does NOT say
- No mention of a **response body** containing a verdict
- No mention of a **separate query endpoint** for verdict status
- No mention of **push notification** of verdict to the agent

### T23 requirement (line 392)
> "Reaccionar a veredicto negativo (alerta + degradación)"

This requires the agent to **receive** the verdict, not just send a report.

### Current `ReportIntegrityAsync` implementation
```csharp
// IBackendClient.cs:370-391
public async Task<bool> ReportIntegrityAsync(IntegrityReport report, ...)
{
    var url = $"{this.baseUrl}/rest/v1/integrity_reports";
    var payload = new {
        report_hash = report.ReportHash,
        timestamp = report.Timestamp.ToString("O"),
        agent_version = report.AgentVersion,
        platform = report.Platform,
    };
    var response = await this.httpClient.SendAsync(requestMsg, cancellationToken);
    return response.IsSuccessStatusCode; // bool only — no verdict
}
```

Return type: `Task<bool>` — HTTP success or failure only.

---

## What T14 Must Expose for R4

For T23 R4 to be implementable, the backend must communicate the verdict of its evaluation. Three viable options:

### Option A — Response body on the same POST (recommended)

```
POST /rest/v1/integrity_reports
Body: {
  report_hash: string,
  timestamp: string (ISO 8601),
  agent_version: string,
  platform: string,
  binary_hash: string,        // T23 adds this
  signature_valid: boolean    // T23 adds this
}
Response 200:
{
  verdict: "trust" | "revoked" | "unknown"
}
```

**Pros**: Single round-trip, agent reacts immediately, aligns with REST semantics.
**Cons**: Requires T14 backend modification.

### Option B — Separate query endpoint

```
POST /rest/v1/integrity_reports   → 202 Accepted (report received)
GET  /rest/v1/integrity_status    → { verdict: "trust" | "revoked" | "unknown" }
```

**Pros**: Decouples reporting from verdict retrieval.
**Cons**: Additional round-trip, verdict may not be immediately available.

### Option C — Push via existing WNS channel (T19)

```
POST /rest/v1/integrity_reports   → 202 Accepted
WNS raw notification              → { type: "integrity_verdict", verdict: "revoked" }
```

**Pros**: No new endpoint needed; uses existing push infrastructure.
**Cons**: Requires T19 WNS push to carry verdict payload; agent must handle async verdict.

---

## Recommended Option

**Option A** — adds a `verdict` field to the existing POST response. This is the minimal change to T14 that satisfies R4.

Required changes:
1. **T14 backend**: Return `{ "verdict": "trust"|"revoked"|"unknown" }` in the response body of `POST /rest/v1/integrity_reports`.
2. **T23 client**: Parse `verdict` from response body; change `ReportIntegrityAsync` return type from `Task<bool>` to `Task<IntegrityResponse>`.
3. **T23-3.5**: Implement `verdict == "revoked"` → `AddIssue(BinaryIntegrityFailure, Severe)`.

---

## T23 Tasks Blocked by This Gap

| Task | Blocker |
|------|---------|
| 3.5 — Server verdict reaction | **Blocked** — T14 does not return verdict |

All other T23 tasks (Phase 1–3.4, Phase 4, Phase 5) are implementable without this gap.

---

## Resolution Owner

This gap is a **cross-team dependency** between:
- T14 owner (backend contract)
- T23 owner (agent implementation)

The backend team must confirm which option (A, B, or C) they can implement, or propose an alternative that exposes the verdict to the agent.
