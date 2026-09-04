# Proposal: Child-Facing Trustworthy Limits UX

## Intent

Define the documentation contract for child-facing limits so the product UX is
firm, predictable, understandable, respectful, accessible, truthful, and
non-humiliating. Limits MUST be framed as family and safety boundaries, not as
punishment or a judgment of the child's character. The existing backlog remains
the source of truth; this change records how T25–T30 and the age-band contract
should be interpreted without authorizing implementation.

## Scope

### In Scope
- A normative UX contract for limits, reasons, transparency, progress, time,
  requests, rewards, warnings, and repair surfaces across ages 7–12, 13–16,
  and 17–18.
- A unified product document that combines 97 stable UI/UX requirements with
  evidence boundaries, non-blocking heuristics, examples, and hypotheses
  requiring child/caregiver research.
- A research validation protocol for accessibility, comprehension, dignity,
  truthful progress, and non-humiliating failure/recovery states.
- Delivered documentation in `openspec/changes/child-facing-trustworthy-limits-ux/specs/child-facing-ux/spec.md`
  and `docs/product/child-facing-ux-requirements.md`.

### Out of Scope
- Any code, UI, resource, test, configuration, or backend changes.
- Hermes profile creation and configuration; this work is deferred.
- Claims that colors, shapes, mascots, or phrases universally prevent distress
  or perceptions of punishment.
- Resolving backlog contradictions silently; unresolved questions remain
  explicitly documented for later decisions.

## Capabilities

### New Capabilities
- `child-facing-ux`: Normative child-facing limits contract plus unified product
  requirements, evidence and heuristic boundaries, and research validation method.

### Modified Capabilities
- None.

## Approach

Maintain the documentation/specification capability grounded in T25–T30 and the
service-authoritative architecture: the Service decides, SessionAgent renders,
and the child cannot configure policy. Record known gaps, including conflicting
progress counts (`0 of 4`, six-step route, and `5 of 5`) and the T25/T32
transparency wording versus backend behavioral events. Do not invent age-band,
request-time, reward, or repair behavior where the backlog is incomplete.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `openspec/changes/child-facing-trustworthy-limits-ux/` | Modified | Proposal and delivered `child-facing-ux` specification. |
| `docs/product/child-facing-ux-requirements.md` | New | Canonical product requirements, evidence, and guidance for trustworthy child-facing limits. |
| `backlog-control-parental-windows.md` | Read-only | T25–T30 remain the source of truth and are not edited. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Documentation overstates evidence or hides contradictions | Med | Label rules, heuristics, hypotheses, and open gaps separately. |
| Future reviewers treat visual conventions or indirect evidence as universal authority | Med | Make evidence labels, applicability caveats, and non-universal claims explicit. |

## Rollback Plan

Delete the `openspec/changes/child-facing-trustworthy-limits-ux/` directory and
`docs/product/child-facing-ux-requirements.md`. No source, backlog, configuration,
tests, Git history, or unrelated OpenSpec change is altered.

## Success Criteria

- [ ] The `child-facing-ux` capability and unified product requirements document are aligned.
- [ ] The unified document contains 97 stable, unique requirement IDs with their acceptance criteria.
- [ ] T25–T30 intent, age bands, authority boundaries, evidence tiers, and
       research protocol are traceable without inventing behavior.
- [ ] Known contradictions and decision gaps are recorded for review.
- [ ] The delivered scope remains documentation-only; no implementation is authorized.

## Proposal question round

Assumptions for later review: the backlog's current wording takes precedence;
age-band differences are documented as constraints or research questions until
validated; and accessibility/dignity review applies to every limit, request,
reward, and repair state. The next specification round should confirm whether
these assumptions need correction and which unresolved progress/transparency
questions have product-owner priority.
