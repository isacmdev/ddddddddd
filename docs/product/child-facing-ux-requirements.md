# Child-Facing UX Requirements, Evidence, and Guidance

## Decision and authority

**The product goal is not to make every limit pleasant, remove frustration, or
make a child agree with a boundary. The goal is to make every limit firm,
predictable, understandable, respectful, accessible, truthful, and
non-humiliating.**

A limit can be unwelcome and still meet this contract. Child-facing experiences
MUST present limits, monitoring, progress, requests, rewards, degraded
protection, and recovery as truthful system states. The Service remains
authoritative: app UI and SessionAgent surfaces render Service evidence and act
on supported choices, but do not create, reinterpret, remove, or negotiate
policy. When an authoritative fact is unavailable, the UI says it is unknown or
omits it; it does not invent a timer, promise, action, or success state.

## Purpose and readers

This is the canonical product document for future child-facing UI design, copy,
research, implementation, and testing. It combines stable, testable requirement
IDs with the evidence limits and practical guidance needed to apply them,
without assessing the current implementation.

Intended readers are product managers, designers, content designers,
accessibility specialists, researchers, privacy and safeguarding reviewers,
Windows UI engineers, QA engineers, and reviewers of future child-facing work.

## Source hierarchy

The sources have different authority domains. A lower row supplies detail but
does not override a higher row in that domain.

| Priority | Source | Authority in this document |
| --- | --- | --- |
| 1 | [`backlog-control-parental-windows.md`](../../backlog-control-parental-windows.md), especially global architecture and T25-T32 | Product scope, runtime authority, data flow, supported actions, and task dependencies |
| 2 | [Child-Facing UX OpenSpec specification](../../openspec/changes/child-facing-trustworthy-limits-ux/specs/child-facing-ux/spec.md) | Normative child-facing behavior and acceptance boundaries |
| 3 | This document | Canonical product requirements, planning IDs, evidence limits, examples, and future design/test gates derived from the sources above |
| 4 | [External sources](#sources-and-applicability) | Supporting rights, accessibility, indirect empirical, and platform guidance within each source's stated caveat |

When sources expose a contradiction or an incomplete contract, this catalog
keeps it open. Product, technical, privacy, or safeguarding owners MUST resolve
the relevant facts before dependent design is ready.

## Non-goals

- Auditing screens, code, tests, accessibility, localization, or runtime behavior.
- Assigning a present compliance status to any requirement.
- Designing final screens, selecting implementation technology, or prescribing unapproved control details.
- Changing policy, runtime authority, backend behavior, the backlog, or the normative OpenSpec specification.
- Defining, creating, or configuring Hermes profiles; that work is deferred.
- Selecting final terminology, visual identity, age variants, or unresolved flow behavior.

## Review quick path

1. Confirm the exact Service state, authority, known timing, freshness, and supported actions.
2. Check that the surface explains what happened, why, duration or next availability when known, and what the child can do now.
3. Remove identity judgment, punishment framing, manipulation, fake urgency, hidden or theatrical monitoring, and unsupported choices.
4. Trace progress, requests, errors, degraded protection, repair, and recovery to authoritative evidence.
5. Verify equivalent meaning and operation without color, shape, animation, sound, pointer input, or precise vision.
6. Classify each finding as a blocker, warning, or research-required outcome.
7. Route claims about interpretation, age fit, visual treatment, or psychology to child/caregiver validation.

### Rule and review classification

| Classification | Language or trigger | Review disposition |
| --- | --- | --- |
| Normative | `MUST`, `MUST NOT`, or an explicit product prohibition | A violation is a **blocker** and must be corrected before approval. |
| Heuristic | `SHOULD`, `SHOULD NOT`, `MAY`, or a suggested presentation pattern | A credible risk is a **warning**; context may justify a documented departure that violates no `MUST`. |
| Research | `RQ`, `V`, or dependence on an unvalidated interpretation or outcome | Mark **research-required**; team opinion and engagement metrics alone cannot close it. |

A surface can receive more than one classification. For example, sensory-only
status is a blocker, while comprehension of a particular alternative across age
bands may also be research-required.

### Evidence labels and claim limits

Labels identify a claim's basis and limits, not its severity. More than one can
apply.

| Label | Meaning | Claim limit |
| --- | --- | --- |
| `R` | Rights or regulatory basis | Informs child rights, privacy, transparency, and anti-deception review; jurisdiction requires separate assessment. |
| `A` | Accessibility basis | Informs perceivability, operability, understandability, motion, focus, status, and cognitive accessibility. |
| `E` | Indirect empirical evidence | Adjacent psychology or parenting evidence; never direct proof that this UI causes a child outcome. |
| `P` | Product ethics decision | A repository-owned dignity, truthfulness, or non-manipulation rule, which may be stricter than a cited minimum. |
| `V` | Requires product validation | A proposed interpretation, age variant, visual treatment, phrase, or outcome to test with children and caregivers. |

Normative rules may be grounded in `R`, `A`, or `P`. Familiar child-oriented
design does not make a heuristic universal. Evidence marked `E` MUST NOT be
represented as direct UI evidence, and `V` claims remain open until suitable
product research supports them.

## Using the catalog

### IDs

IDs are stable planning references. `UX-<DOMAIN>-<NUMBER>` identifies a
cross-cutting requirement. `UX-SURFACE-<SURFACE>-<NUMBER>` identifies a
surface-specific requirement. IDs MUST NOT be renumbered when a requirement is
retired; mark the ID retired in a future revision and create a new ID when the
meaning changes materially.

Acceptance criteria describe observable outcomes without selecting unapproved
layouts, controls, colors, shapes, mascots, animations, or wording.

## Cross-cutting requirements

### Product intent and framing (`UX-INTENT`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-INTENT-001` | MUST | Present limits as understandable boundaries, not punishments. | Each limit explains the state and available next step without punishment labels, threats, humiliation, or forced positivity. |
| `UX-INTENT-002` | MUST | Describe state, not the child's identity or character. | Copy refers to the app, time, request, family rule, or system state and contains no judgment such as good, bad, responsible, selfish, or disappointing. |
| `UX-INTENT-003` | MUST NOT | Manipulate, shame, frighten, blame, mock, or pressure the child. | No flow depends on fake urgency, deceptive countdowns, nagging, surveillance theater, ridicule, or celebration of denial/failure. |
| `UX-INTENT-004` | MUST | Preserve the Service-authoritative boundary. | Every displayed policy, decision, progress value, timing value, and recovery claim is traceable to authoritative evidence or is explicitly unknown. |

### Information architecture and screen intention (`UX-IA`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-IA-001` | SHOULD | Keep one primary visual focus on time-sensitive status and limit surfaces. | Daily status, warnings, countdowns, and blocking states prioritize the current time-sensitive state and next real action; context may justify another hierarchy without weakening truthfulness or accessibility. |
| `UX-IA-002` | SHOULD | Put the primary state and next real action before secondary detail. | A child can locate the current state and available action without opening secondary explanation. |
| `UX-IA-003` | MUST | Keep daily limits and monitoring information discoverable. | The child can reach current limits and the monitoring explanation through a stable, documented path that does not depend on an error or block occurring. |
| `UX-IA-004` | SHOULD | Use progressive disclosure without hiding reason or authority. | Optional policy detail may be layered, but the immediate state, reason, authority, and real action remain available on the primary surface. |

### Visual design system and state semantics (`UX-VIS`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-VIS-001` | MUST | Define reusable visual tokens before surface-level styling. | The future design source defines named tokens for color roles, typography, spacing, sizing, elevation/borders where used, motion, focus, and state semantics. |
| `UX-VIS-002` | MUST | Support applicable native Windows themes and user settings. | Essential content and actions remain understandable in supported light, dark, and Windows contrast themes without surface-specific semantic drift. |
| `UX-VIS-003` | MUST | Establish a readable type system that tolerates scaling and localization. | Type roles have documented hierarchy and remain legible without clipped or obscured essential text at supported Windows text/display scaling and target locales. |
| `UX-VIS-004` | SHOULD | Use a consistent spacing and density system. | Related content, actions, and status regions use documented spacing rules; density changes do not remove required limit anatomy. |
| `UX-VIS-005` | MUST | Give every state a consistent semantic treatment. | Loading, current, warning, blocked, pending, approved, denied, offline, stale, degraded, error, and recovered states are distinguishable by more than one sensory cue and retain the same meaning across surfaces. |
| `UX-VIS-006` | RQ | Validate proposed visual identity and age-band treatments in context. | Claims about interpretation or age fit are recorded as hypotheses and tested; no color, shape, mascot, animation, or style is accepted as universally calming, trustworthy, therapeutic, or non-punitive. |

### Native Windows accessibility (`UX-A11Y`)

These requirements apply to native Windows behavior. They do not claim web or
WCAG conformance; this document uses web sources only as documented benchmarks.

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-A11Y-001` | MUST | Provide complete keyboard operation. | Every essential path and action is reachable and operable without pointer input, with no keyboard trap except a platform-required boundary that has a documented accessible path. |
| `UX-A11Y-002` | MUST | Provide logical, visible, and recoverable focus. | Focus order follows reading/task order, focus is visibly identifiable in supported themes, and focus moves or returns predictably after dialogs, overlays, navigation, and state changes. |
| `UX-A11Y-003` | MUST | Expose useful accessible names, roles, values, descriptions, and actions. | A screen reader can identify each essential control, current value, limit reason, authority, timing, and available action without relying on nearby visual text. |
| `UX-A11Y-004` | MUST | Expose status changes accessibly. | Warnings, request delivery/resolution, errors, degraded protection, and recovery are announced at an appropriate priority without unnecessary repetition or disruptive focus theft. |
| `UX-A11Y-005` | MUST | Meet documented contrast requirements in every supported theme and state. | Text, essential graphics, controls, state indicators, and focus indicators pass the project's approved native Windows contrast criteria. |
| `UX-A11Y-006` | MUST | Support Windows text and display scaling. | At supported scaling settings, essential content and actions remain available, readable, and operable without overlap, clipping, or meaning lost off-screen. |
| `UX-A11Y-007` | MUST | Respect reduced-motion and animation settings. | Motion that is not essential can be removed; essential state changes have an equivalent non-motion presentation and no interaction requires tracking animation. |
| `UX-A11Y-008` | MUST NOT | Depend on a single sensory characteristic or unsafe flashing. | Color, shape, position, motion, sound, or timing alone never carries essential meaning, and no surface uses unsafe flashing. |

### Copy, content, localization, and honest authority (`UX-COPY`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-COPY-001` | MUST | Maintain one localizable source for child-facing copy. | Every child-facing string, substitution, accessible label, and status message has a localization key and no essential sentence is assembled in a way translators cannot reorder. |
| `UX-COPY-002` | SHOULD | Prefer short, literal, concrete language. | Copy uses familiar units and direct verbs, avoids unexplained technical/legal language, and preserves required facts when shortened. |
| `UX-COPY-003` | MUST | State what happened and why. | A limit identifies the affected app/scope or device state and the authoritative reason without blaming the child. |
| `UX-COPY-004` | MUST | Identify authority honestly. | Copy distinguishes family settings, caregiver decisions, Service enforcement, and technical failure where relevant and never implies that local UI has authority it lacks. |
| `UX-COPY-005` | MUST | State duration or next availability only when known. | Authoritative timing is shown in understandable units; unknown timing is explicitly unknown or omitted, with no invented timer, promise, or precision. |
| `UX-COPY-006` | MUST | State only actions that are real now. | Each named action is available in the current state, states its effect accurately, and does not imply that hard policy can be dismissed or overridden. |

### Age bands (`UX-AGE`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-AGE-001` | MUST | Plan variants for `7-12`, `13-16`, and `17-18`. | Every child-facing surface specification identifies content, density, examples, and visual-treatment decisions for all three required product segments. |
| `UX-AGE-002` | MUST | Preserve facts, dignity, authority, and available actions across age variants. | No age variant hides a material fact, adds unsupported agency, weakens accessibility, or changes the underlying policy outcome. |
| `UX-AGE-003` | RQ | Determine exact differentiation through product research. | Wording and presentation differences are hypotheses tested with children and caregivers across relevant literacy, language, disability, technical familiarity, and family contexts. |
| `UX-AGE-004` | MUST NOT | Treat age band as proof of an individual's ability, preference, or emotional response. | Design rationale contains no universal developmental claim and provides accessible alternatives independent of age segment. |

### Child and caregiver research protocol (`UX-RESEARCH`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-RESEARCH-001` | MUST | Ask for open-ended interpretation before introducing labels or intended outcomes. | Research presents a realistic state or prototype without naming the intended emotion or interpretation, then first asks participants what happened, who decided, what they can do, and what is monitored. |
| `UX-RESEARCH-002` | MUST | Use teach-back across every age band and relevant state. | Children and caregivers in `7-12`, `13-16`, and `17-18` explain boundaries, timing, authority, monitoring, progress, requests, rewards, repair, failure, recovery, next actions, and how the experience treated them in their own words where applicable. |
| `UX-RESEARCH-003` | MUST | Record interpretation risks separately from engagement. | Misunderstandings, false expectations, dignity concerns, accessibility barriers, task outcomes, and minority or outlier harms are recorded separately from completion, clicks, time, request frequency, retention, or other engagement measures. |
| `UX-RESEARCH-004` | MUST NOT | Use engagement metrics alone as evidence of comprehension, dignity, trust, or informed understanding. | A research conclusion about those outcomes requires qualitative interpretation and real-action evidence; engagement measures alone cannot close the question. |

### Interaction and bounded agency (`UX-ACT`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-ACT-001` | MUST | Make controls perform the action they name. | Activation produces the described effect or a truthful delivery/failure state; it never silently substitutes a different action. |
| `UX-ACT-002` | MUST | Offer child agency only within supported policy and runtime capabilities. | Requests, retries, allowed alternatives, details, and repair handoffs appear only in states where those actions are real. |
| `UX-ACT-003` | MUST NOT | Make fixed policy look dismissible, removable, or locally negotiable. | Visual treatment, labels, and interaction behavior do not suggest that closing a surface changes an authoritative rule. |
| `UX-ACT-004` | MUST | Keep policy configuration outside child-facing agency. | Child-facing controls can inspect state or invoke supported requests/actions but cannot edit family policy or represent adult consent/elevation as child-controlled. |

### Progress, loading, error, offline, degraded, and recovery (`UX-STATE`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-STATE-001` | MUST | Derive visible state from authoritative evidence. | Each state specification identifies its source, freshness, confidence, and supported transitions; missing facts are not inferred. |
| `UX-STATE-002` | MUST | Distinguish determinate from indeterminate progress. | A numeric count, percentage, or countdown appears only when the total or duration is authoritative; otherwise the UI uses an honest non-numeric state. |
| `UX-STATE-003` | MUST | Explain loading without implying completion. | Loading preserves context, identifies what is being retrieved or applied when useful, and does not display placeholder data as confirmed data. |
| `UX-STATE-004` | MUST | Distinguish empty, stale, offline, and technical-error states. | Each state has distinct copy, freshness information where available, actual effect on enforcement, and only supported retry or next actions. |
| `UX-STATE-005` | MUST | Report request and decision transitions truthfully. | Saved locally, queued, sent, pending, approved, denied, throttled, expired, and failed states are not collapsed when their child-facing meaning differs. |
| `UX-STATE-006` | MUST | Represent degraded protection honestly. | The surface names the concrete problem, practical effect, required actor, and real next action without claiming full protection or using false urgency. |
| `UX-STATE-007` | MUST | Claim recovery only after authoritative confirmation. | Recovery copy and visuals appear only after the Service reports restoration and identify any remaining limitation. |
| `UX-STATE-008` | MUST | Preserve usable recovery from interruption. | Closing, restarting, losing connectivity, or returning from an external repair step restores the latest confirmed context without duplicating requests or falsely advancing progress. |

### Privacy and transparency (`UX-PRIV`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-PRIV-001` | MUST | Keep monitoring information persistently discoverable. | The child can find the transparency surface during normal use; no hidden monitoring mode or secret-state treatment exists. |
| `UX-PRIV-002` | MUST | Explain monitoring with verified what, what-not, who, and purpose facts. | The surface identifies collected data, meaningful exclusions, recipients/access, and uses in language appropriate to the selected age variant. |
| `UX-PRIV-003` | MUST | Match deployed collection and behavioral-event behavior. | Every disclosure claim traces to a verified data inventory and recipient/access contract, including applicable T32 events; categorical claims remain blocked while facts conflict. |
| `UX-PRIV-004` | MUST NOT | Exaggerate surveillance or conceal uncertainty. | No copy, iconography, motion, or status implies access to content or continuous observation beyond verified behavior; unknown facts are not guessed. |

### Rewards and motivation (`UX-REWARD`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-REWARD-001` | MUST | Present earned time as an authoritative grant outcome. | The UI reports the known amount, scope, source, balance effect, and applicable cap or boundary without implying unlimited access. |
| `UX-REWARD-002` | MUST | Preserve hard-policy boundaries. | Reward copy and actions do not imply that earned time overrides `blocked`, `allow_only`, or any other rule the grant cannot lift. |
| `UX-REWARD-003` | MUST NOT | Connect rewards to moral worth, affection, identity, or demanded obedience. | Messages describe what was added and applicable rules, not what a good, deserving, loyal, or disappointing child is. |
| `UX-REWARD-004` | RQ | Validate motivational interpretation without assuming benefit. | Research records comprehension, pressure, dignity concerns, and unintended effects separately from engagement; no universal motivation or wellbeing claim is made. |

## Surface requirements

Cross-cutting requirements apply to every relevant surface in addition to the
requirements below.

### Onboarding (`UX-SURFACE-ONBOARD`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-ONBOARD-001` | MUST | Explain the current setup step, why it matters, and what happens next. | Every step names its purpose, responsible actor, prerequisites, and next transition without representing adult consent or elevation as child-controlled. |
| `UX-SURFACE-ONBOARD-002` | MUST | Show only verified progress. | Completed foundations and any `N of M` value match authoritative state; an unresolved or unverified total is not presented as determinate progress. |
| `UX-SURFACE-ONBOARD-003` | MUST | Support safe interruption and resumption. | Returning to onboarding restores verified completed/pending steps and does not repeat consent, requests, or privileged actions unnecessarily. |
| `UX-SURFACE-ONBOARD-004` | MUST | Sequence an applicable strengthened-mode offer after a truthful STANDARD first win. | Where strengthened protection is applicable, its offer follows standard setup and the first value demonstration; declining, deferring, or lacking support for it does not block or invalidate STANDARD completion. |

### Daily status and home (`UX-SURFACE-HOME`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-HOME-001` | MUST | Show the most relevant current status with one primary focus. | The child can identify remaining time, what is allowed now, and the next known downtime without interpreting competing headline states. |
| `UX-SURFACE-HOME-002` | MUST | Keep values current and visibly qualified. | Values come from Service evidence; stale, loading, offline, or unknown values are labeled and never presented as current confirmed data. |
| `UX-SURFACE-HOME-003` | MUST | Provide stable paths to real actions and explanations. | Supported request, limits detail, allowed alternatives, reward, and transparency destinations are discoverable without making policy configurable. |
| `UX-SURFACE-HOME-004` | MUST | Show earned-time balance and reward confirmation in daily status. | Daily status presents the authoritative earned-time balance and confirms newly applied reward grants, including known scope and cap, without requiring navigation to a separate reward destination. |

### Warnings and countdowns (`UX-SURFACE-WARN`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-WARN-001` | MUST | Present warnings at the authoritative 10-minute and 5-minute thresholds. | When an uninterrupted eligible countdown crosses each Service-reported threshold, the warning identifies the affected scope, exact remaining time, and what changes at the boundary. |
| `UX-SURFACE-WARN-002` | MUST | Update countdowns honestly. | Displayed time follows authoritative time, handles delay or uncertainty explicitly, and never creates urgency through invented precision. |
| `UX-SURFACE-WARN-003` | MUST | Provide an accessible warning sequence without surprise cuts or coercive repetition. | Both warnings precede an expected cut when a continuous countdown crosses 10 and 5 minutes; equivalent visual and assistive-technology status is available, and repetition follows authoritative transitions rather than pressure or nagging. |

### Blocking overlay (`UX-SURFACE-BLOCK`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-BLOCK-001` | MUST | Present the complete limit anatomy. | The overlay states what is blocked, why, authority, duration/next availability when known, and real actions available now. |
| `UX-SURFACE-BLOCK-002` | MUST | Distinguish the policy reason from technical failure. | The displayed reason matches the Service decision and does not replace a degraded/error condition with a family-rule explanation or vice versa. |
| `UX-SURFACE-BLOCK-003` | MUST | Offer only supported alternatives. | Request more time, view allowed apps, view limits, or other controls appear only when available and state their bounded effect. |
| `UX-SURFACE-BLOCK-004` | MUST NOT | Imply that dismissing or closing presentation removes the block. | No control or visual affordance promises exit from policy unless the Service supports that outcome for the current state. |

### Request more time (`UX-SURFACE-REQUEST`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-REQUEST-001` | MUST | Show request entry only for an eligible state and scope. | Eligibility comes from authoritative support, and the UI explains what can be requested without implying guaranteed approval. |
| `UX-SURFACE-REQUEST-002` | MUST | Confirm scope, duration, and optional-data handling before submission. | The child can verify the requested app/category/device scope and duration and knows whether a reason is optional and how it is used. |
| `UX-SURFACE-REQUEST-003` | MUST | Separate local save, delivery, pending, and resolution. | Offline-queued, sent, pending, failed, throttled, expired, approved, and denied states use distinct truthful messages where applicable. |
| `UX-SURFACE-REQUEST-004` | MUST | Prevent accidental duplicates and manipulative repeat prompts. | Repeated activation cannot silently create duplicate requests; throttling explains the actual constraint and next available action without blame. |
| `UX-SURFACE-REQUEST-005` | MUST | Preserve hard-policy boundaries. | The flow explains that approval affects only the supported time-limited scope and does not promise to override blocked or allow-only policy. |

### Approval and denial (`UX-SURFACE-DECISION`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-DECISION-001` | MUST | Identify the decision and its effect. | Approval reports applied duration, scope, start/expiry when known, and remaining rules; denial reports that no time was added. |
| `UX-SURFACE-DECISION-002` | MUST | Distinguish decision evidence from grant application. | The UI does not claim usable time until authoritative grant state is applied; delay or application failure has its own state. |
| `UX-SURFACE-DECISION-003` | MUST | Keep denial neutral and actionable. | Denial contains no mockery, moral judgment, or pressure and shows next availability or other real action when known. |
| `UX-SURFACE-DECISION-004` | MUST | Announce and persist the result accessibly. | The result is exposed as an accessible status and remains available long enough or through a stable destination for the child to review. |

### Earned time and rewards (`UX-SURFACE-EARNED`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-EARNED-001` | MUST | Show the applied reward facts. | The surface presents known amount, scope, source, balance, cap, expiry, and remaining policy boundaries without inventing missing values. |
| `UX-SURFACE-EARNED-002` | MUST | Distinguish granted, applied, seen, expired, and unavailable states. | Confirmation follows authoritative grant/application evidence and does not imply benefit after expiry or outside the grant scope. |
| `UX-SURFACE-EARNED-003` | MUST | Use informational, non-moralizing framing. | The presentation reports an outcome without praise or criticism tied to identity, affection, worth, or obedience. |

### Transparency and monitoring (`UX-SURFACE-TRANSPARENCY`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-TRANSPARENCY-001` | MUST | Provide a child-accessible monitoring overview. | The surface covers verified collected data, meaningful exclusions, access/recipients, purposes, active status, and where to get more detail. |
| `UX-SURFACE-TRANSPARENCY-002` | MUST | Separate monitoring facts from adult consent and legal notices. | Child-facing explanation remains understandable and discoverable during normal use while adult affirmative consent stays an adult-controlled onboarding requirement. |
| `UX-SURFACE-TRANSPARENCY-003` | MUST | Avoid hidden or theatrical monitoring presentation. | No mode conceals monitoring from the child, and no wording or imagery suggests broader observation than verified collection and events. |

### Degraded, repair, and recovery (`UX-SURFACE-REPAIR`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-REPAIR-001` | MUST | Name the concrete degraded condition and practical effect. | The surface states what is not working, which protections may be affected, and what remains active without fear-based language. |
| `UX-SURFACE-REPAIR-002` | MUST | Identify the responsible actor and real repair path. | Child and caregiver responsibilities are distinct; elevation or settings changes are not presented as child actions. |
| `UX-SURFACE-REPAIR-003` | MUST | Represent repair progress from evidence. | Starting, waiting, external handoff, failed, retryable, and verification states remain distinct and do not imply repair merely because an action was tapped. |
| `UX-SURFACE-REPAIR-004` | MUST | Confirm recovery only after verification. | The recovered state follows Service evidence, states the restored level and any remaining limitation, and is announced accessibly. |
| `UX-SURFACE-REPAIR-005` | MUST | Provide one-touch repair or handoff for each known degradation cause. | Each authoritative cause maps to one primary activation that starts its supported repair or required-actor handoff and does not claim recovery merely because it was activated. |
| `UX-SURFACE-REPAIR-006` | MUST | Group and rate-limit degradation alerts by incident. | Repeated signals for the same unresolved incident remain one coherent alert state, and a documented per-incident rate limit prevents repeated interruption; a distinct or materially changed incident remains representable. |

### Empty, stale, loading, offline, and technical-error states (`UX-SURFACE-SYSTEM`)

| ID | Strength | Requirement | Concise acceptance criteria |
| --- | --- | --- | --- |
| `UX-SURFACE-SYSTEM-001` | MUST | Give empty states a truthful cause and next action. | "No data," "no limits," "no requests," and "not yet loaded" are not conflated; each state offers only an applicable action. |
| `UX-SURFACE-SYSTEM-002` | MUST | Mark stale information with last-confirmed context. | The surface identifies that data may be outdated, shows last-confirmed time when known, and does not silently style it as current. |
| `UX-SURFACE-SYSTEM-003` | MUST | Explain offline behavior and enforcement separately. | The child can tell what is saved locally, what has not been sent/updated, which cached rules still apply, and what will happen after reconnection. |
| `UX-SURFACE-SYSTEM-004` | MUST | Make technical errors specific enough for recovery without exposing sensitive internals. | Error copy identifies the failed task, current policy effect, and supported retry/handoff while avoiding blame, secrets, raw identifiers, or child content. |
| `UX-SURFACE-SYSTEM-005` | MUST | Preserve state truth through retries and late responses. | Retry, cancellation, reconnect, and delayed completion cannot duplicate actions, overwrite newer evidence, or turn an unknown outcome into success. |

## Surface orientation

This scan map helps reviewers find the governing catalog sections. It does not
replace the cross-cutting or surface requirements.

| Review area | Start with | Key orientation question |
| --- | --- | --- |
| Setup | [Onboarding](#onboarding-ux-surface-onboard) | Is progress verified, and are adult consent or elevation clearly outside child control? |
| Everyday time | [Daily status](#daily-status-and-home-ux-surface-home) and [warnings](#warnings-and-countdowns-ux-surface-warn) | Can the child understand what is available now and the next authoritative boundary without surprise or fake precision? |
| Enforced limits | [Blocking](#blocking-overlay-ux-surface-block), [requests](#request-more-time-ux-surface-request), and [decisions](#approval-and-denial-ux-surface-decision) | Are block, bounded agency, delivery, decision, and grant application kept distinct? |
| Earned time | [Rewards](#earned-time-and-rewards-ux-surface-earned) | Does the UI report a bounded grant outcome rather than moral worth or unlimited access? |
| Monitoring | [Transparency](#transparency-and-monitoring-ux-surface-transparency) | Can the child find and explain verified collection, exclusions, recipients/access, and purpose? |
| Protection health | [Repair](#degraded-repair-and-recovery-ux-surface-repair) and [system states](#empty-stale-loading-offline-and-technical-error-states-ux-surface-system) | Are practical effect, responsible actor, evidence-backed repair, and verified recovery explicit? |

## Required limit copy anatomy

Every limit presentation MUST provide the following information in visible and
accessible form. The presentation order may vary when research and context
support it, but no item may be replaced with invented information.

| Anatomy | Required content | Governing IDs |
| --- | --- | --- |
| What happened | Affected app, category, device, schedule, or other scope and its current state | `UX-COPY-003`, `UX-SURFACE-BLOCK-001` |
| Why | The authoritative policy or technical reason, stated without character judgment | `UX-COPY-003`, `UX-INTENT-002` |
| Authority | Family settings, caregiver decision, Service enforcement, or technical condition as supported by evidence | `UX-COPY-004`, `UX-INTENT-004` |
| Duration or next availability | Known duration, expiry, or next availability; an honest unknown/omission when unavailable | `UX-COPY-005`, `UX-STATE-002` |
| Real actions now | Supported request, retry, allowed alternative, detail, or caregiver handoff and its bounded effect | `UX-COPY-006`, `UX-ACT-002` |

## Use and avoid

These examples illustrate structure, not approved final copy. Dynamic facts
must come from authoritative data, and monitoring language must be reconciled
with deployed collection, recipients/access, sharing, exclusions, and T32 event
behavior before use.

| Situation | Use | Avoid |
| --- | --- | --- |
| Hard limit | "Games are blocked because today's game-time limit has been used." | "You lost games because you could not control yourself." |
| Authority | "This limit comes from your family settings. This app cannot change it." | "Tap here to unlock" when the UI has no such authority. |
| Unknown timing | "We cannot confirm when this app will be available. You can check your limits again later." | A fabricated countdown or promise. |
| Ten-minute warning | "10 minutes of game time remain. Games will close at 6:30 PM." | "Act now before everything is taken away!" |
| Offline request | "Your request is saved on this PC and has not been sent yet. It will send when the connection returns." | "Request sent" before delivery evidence. |
| Request pending | "Your request for 20 more minutes was sent. It is waiting for a caregiver decision." | Repeated prompts designed to pressure the child to ask again. |
| Approval | "20 minutes was added to games. Other family rules still apply." | "You won! All blocks are gone." |
| Denial | "No extra time was added. Games will be available tomorrow at 4:00 PM." | "Try behaving better next time." |
| Earned time | "20 minutes was added to games. Your daily cap still applies." | "Good kids earn screen time." |
| Degraded protection | "Some limits may not apply because the protection service is not running." | "You are fully protected" or threat-based breach language |
| Repair complete | "Protection is active again. The service reported recovery at 3:42 PM." | "Fixed!" before the Service confirms recovery. |
| Monitoring | "This PC records app-use time and protection events." followed by verified exclusions, access, and purpose | "We see everything" or imagery implying broader observation than actual collection. |
| Technical error | "We could not load the latest limit status. The last confirmed update was 2:10 PM. Try again." | Showing stale data as current or blaming the child. |

### Full blocked-state example

Use this structure when the Service reports a blocked app, knows the next
availability, and does not support a request path for this state:

```text
Game time is finished for today

Minecraft is blocked because today's family game-time limit has been used.

Set by: Your family settings
Available again: Tomorrow at 4:00 PM

You can use apps that are still allowed. This app cannot change this limit.

[View allowed apps]  [See today's limits]
```

The app name, reason, authority, and time MUST come from authoritative data. If
next availability is unknown, use an honest unknown state. Add `Request more
time` only when it is supported and its delivery and result states exist.

## Child and caregiver research method

`UX-RESEARCH-001` through `UX-RESEARCH-004` define the acceptance boundary.
Research plans also need the following operating method so those requirements
produce credible, safe evidence:

1. Apply appropriate caregiver consent, child assent, privacy, and safeguarding controls to every study.
2. Use realistic states or interactive prototypes and preserve the open-ended-first and teach-back sequence required by `UX-RESEARCH-001` and `UX-RESEARCH-002`.
3. Test real actions, not preference alone: find an allowed app, locate next availability, submit a supported request, identify delivery, interpret a decision, find monitoring information, and begin a repair handoff.
4. Cover all three age bands while varying literacy, language, localization length, technical familiarity, disability, assistive settings, and family context; age never substitutes for individual evidence.
5. Include keyboard, screen-reader, magnification, Windows contrast, reduced-motion, and other relevant assistive use, measuring comprehension as well as technical operation.
6. Exercise offline, delayed, unknown, denied, degraded, error, interruption, and recovery states, not only the happy path.
7. Preserve misunderstandings, false expectations, dignity concerns, accessibility barriers, and minority or outlier harms separately from aggregate task outcomes.
8. Re-test materially changed copy and interactions in context.

Completion rate, clicks, time on task, request frequency, retention, and other
engagement measures are not evidence by themselves of comprehension, dignity,
trust, or informed understanding. A child can engage with a manipulative or
misunderstood interface; qualitative interpretation and real-action evidence
are required.

## Future definition of ready: UI design

A surface is ready to enter detailed UI design only when all applicable items
below are satisfied. This is a future planning gate, not a finding about the
current product.

- [ ] The surface intention, entry paths, exits, responsible actor, and authoritative state source are documented.
- [ ] Required states and transitions include success, unknown, empty, stale, loading, offline, delayed, degraded, technical error, retry, and recovery where applicable.
- [ ] Supported and unsupported actions are confirmed against policy and runtime authority.
- [ ] The required limit copy anatomy and dynamic data contract are available for every limit state.
- [ ] Applicable requirement IDs are selected without renumbering or weakening them.
- [ ] Variants for `7-12`, `13-16`, and `17-18` are planned as hypotheses, with shared facts and actions identified.
- [ ] Localization scope, target locales, expansion risk, terminology dependencies, and accessible content needs are documented.
- [ ] Visual token, Windows theme, contrast, scaling, keyboard, focus, screen-reader, status, and reduced-motion expectations are documented.
- [ ] Privacy, recipient/access, collection, and event facts used by the surface are verified by accountable owners.
- [ ] Dependent open product decisions below are resolved or explicitly prevent the affected design from advancing.
- [ ] Any claim about comprehension, dignity, age fit, trust, motivation, or emotional interpretation has an appropriate research plan and claim boundary.

## Future definition of done: UI implementation

A future implementation is done only when all applicable items below have
verifiable evidence. This is not an assessment of existing code.

- [ ] Every applicable requirement ID has linked design, exact copy, state mapping, and test/research evidence.
- [ ] Displayed state, progress, timing, authority, requests, decisions, grants, degradation, and recovery trace to authoritative evidence, including unknown and delayed cases.
- [ ] Keyboard, focus, screen-reader semantics/status, supported themes, contrast, scaling, localization, and reduced-motion behavior are verified on supported Windows targets.
- [ ] Essential meaning and action remain available without color, shape, position, motion, sound, pointer input, or precise vision alone.
- [ ] Empty, stale, loading, offline, technical-error, interrupted, retry, and late-response paths are verified alongside success paths.
- [ ] Localized strings and substitutions are complete, reviewable in context, and resilient to target-locale expansion.
- [ ] Child-facing variants preserve the same facts and policy boundaries; research-dependent interpretation claims are either supported by suitable evidence or remain explicitly unclaimed.
- [ ] Monitoring copy matches the approved deployed data inventory, recipients/access, purposes, exclusions, retention references, and behavioral events.
- [ ] No action implies unsupported agency, no recovery is announced early, and no policy boundary is made to look dismissible.
- [ ] Product, accessibility, content, privacy/safeguarding, research, engineering, and QA owners have reviewed evidence within their authority domains.

## Known repository gaps and open product decisions

Do not resolve these by copying the most convenient current artifact. Each
decision requires an accountable owner and verified input.

| Decision | Preserved uncertainty | Evidence needed before resolution |
| --- | --- | --- |
| Onboarding progress model | Current artifacts contain `0 of 4`, a six-step route, and `5 of 5`. | Canonical set of foundations, which are mandatory/optional, authoritative completion signals, and treatment of unsupported capabilities |
| Monitoring sharing and recipients | T25 requires collection/use/sharing disclosure; T32 sends minimized behavioral events to backend storage, but final recipient/access and sharing wording is not reconciled. | Deployed event/data inventory, access and recipient map, purposes, retention, backend behavior, and privacy/safeguarding approval |
| Exact age-band differentiation | The three bands are required, but complete copy, density, examples, and visual variants are not defined. | Research across bands and relevant literacy, locale, disability, family, and technical contexts without universal developmental claims |
| Request behavior and variants | Eligibility, optional reason handling, offline delivery, pending, throttle, expiry, failure, delayed resolution, and grant-application variants are incomplete. | End-to-end state/action contract and approved child-facing behavior for every supported transition |
| Reward behavior and variants | Balance, source, cap, scope, expiry, application, and age variants are incomplete. | Authoritative grant/balance contract, caps, expiry behavior, and product/research review of motivational framing |
| Repair behavior and variants | Child/caregiver handoff, external settings/elevation, failure, retry, verification, and recovery variants are incomplete. | Cause-to-action matrix, actor authority, supported Windows paths, verification evidence, and age/accessibility variants |
| Product terminology | Names for protection levels, limits, requests, rewards, monitoring, family/caregiver authority, and repair are not final. | Approved terminology system tested for accuracy, localization, accessibility, and comprehension |
| Visual identity | Theme expression, typography choices, imagery, iconography, shape language, and motion are not final. | Accessible token proposal and contextual research; no unsupported claim that a treatment universally creates calm, trust, wellbeing, or non-punitive interpretation |

## Evidence and claim boundaries

This document makes no universal claim that colors, rounded shapes, mascots,
animations, particular phrases, visual styles, or age-band treatments prevent
distress, create calm, build trust, improve mental health, or eliminate a
perception of punishment. It also does not claim that a respectful limit will
feel pleasant or cease to be perceived as punishment.

Claims about calm, trust, mental health, developmental fit, emotional
interpretation, or elimination of punishment perception MUST be labeled `V`
and tested in the product context. The psychology and parenting sources below
remain `E`: they inform product ethics and research questions but are not direct
proof of UI outcomes.

## Traceability

The OpenSpec references below name normative requirement headings. Supporting
sections point to local orientation, examples, research, and evidence boundaries
without replacing the requirement rows.

| Requirement group | Backlog source | Normative OpenSpec source | Supporting section |
| --- | --- | --- | --- |
| `UX-INTENT` | T25, T27-T30; global Service authority | Limits are truthful and dignified; Surfaces do not manipulate or mock | [Decision and authority](#decision-and-authority), [claim boundaries](#evidence-and-claim-boundaries) |
| `UX-IA` | T25-T30 | Limits are truthful and dignified; Status and progress mirror authority | [Review quick path](#review-quick-path), [surface orientation](#surface-orientation) |
| `UX-VIS` | T25-T30 | Surfaces do not manipulate or mock; Accessibility and age variants are validated | [Evidence labels](#evidence-labels-and-claim-limits), [claim boundaries](#evidence-and-claim-boundaries) |
| `UX-A11Y` | T25-T30 future child-facing surfaces | Accessibility and age variants are validated | [Review quick path](#review-quick-path), [research method](#child-and-caregiver-research-method) |
| `UX-COPY` | T25; consumed by T26-T30 | Limits are truthful and dignified | [Copy anatomy](#required-limit-copy-anatomy), [examples](#use-and-avoid) |
| `UX-AGE` | T25; variants across T26-T30 | Accessibility and age variants are validated; Research separates acceptance from hypotheses | [Research method](#child-and-caregiver-research-method), [claim boundaries](#evidence-and-claim-boundaries) |
| `UX-RESEARCH` | T25-T30 future surface validation; T26 usability testing | Research separates acceptance from hypotheses | [Research method](#child-and-caregiver-research-method) |
| `UX-ACT` | T26-T30 | Limits are truthful and dignified; Status and progress mirror authority | [Decision and authority](#decision-and-authority), [surface orientation](#surface-orientation) |
| `UX-STATE` | T26-T30 | Status and progress mirror authority; Limits are truthful and dignified | [Review quick path](#review-quick-path), [surface orientation](#surface-orientation) |
| `UX-PRIV` | T25, T32 | Monitoring is visible and understandable | [Monitoring orientation](#surface-orientation), [open decisions](#known-repository-gaps-and-open-product-decisions) |
| `UX-REWARD` | T29, T32 | Surfaces do not manipulate or mock | [Reward orientation](#surface-orientation), [examples](#use-and-avoid) |
| `UX-SURFACE-ONBOARD` | T25-T26, T31-T32 | Status and progress mirror authority | [Surface orientation](#surface-orientation), [open decisions](#known-repository-gaps-and-open-product-decisions) |
| `UX-SURFACE-HOME` | T27-T29 | Status and progress mirror authority | [Surface orientation](#surface-orientation) |
| `UX-SURFACE-WARN` | T27, T32; warning authority from T06 | Limits are truthful and dignified; Surfaces do not manipulate or mock | [Surface orientation](#surface-orientation), [examples](#use-and-avoid) |
| `UX-SURFACE-BLOCK` | T25, T28, T32; overlay authority from T08 | Limits are truthful and dignified; Surfaces do not manipulate or mock | [Copy anatomy](#required-limit-copy-anatomy), [blocked-state example](#full-blocked-state-example) |
| `UX-SURFACE-REQUEST` | T28, T32 | Limits are truthful and dignified; Status and progress mirror authority | [Examples](#use-and-avoid), [open decisions](#known-repository-gaps-and-open-product-decisions) |
| `UX-SURFACE-DECISION` | T28 | Limits are truthful and dignified; Status and progress mirror authority | [Examples](#use-and-avoid) |
| `UX-SURFACE-EARNED` | T29, T32 | Surfaces do not manipulate or mock; Status and progress mirror authority | [Examples](#use-and-avoid), [open decisions](#known-repository-gaps-and-open-product-decisions) |
| `UX-SURFACE-TRANSPARENCY` | T25, T32 | Monitoring is visible and understandable | [Surface orientation](#surface-orientation), [open decisions](#known-repository-gaps-and-open-product-decisions) |
| `UX-SURFACE-REPAIR` | T30, T32 | Status and progress mirror authority | [Surface orientation](#surface-orientation), [open decisions](#known-repository-gaps-and-open-product-decisions) |
| `UX-SURFACE-SYSTEM` | T25-T30; offline/event behavior in T28, T32 | Limits are truthful and dignified; Status and progress mirror authority | [Review quick path](#review-quick-path), [examples](#use-and-avoid) |

## Sources and applicability

These sources inform the evidence labels and review rules above. They are not
interchangeable, and citation does not establish legal applicability or direct
product causation.

| Date | Source | Labels | Applicability and caveat |
| --- | --- | --- | --- |
| 2 March 2021 | [UN Committee on the Rights of the Child, General Comment No. 25 (2021) on children's rights in relation to the digital environment](https://www.ohchr.org/en/documents/general-comments-and-recommendations/general-comment-no-25-2021-childrens-rights-relation) | `R` | Rights framework for the digital environment and child participation. It is not direct empirical proof for a specific UI treatment; legal implementation depends on jurisdiction. |
| 12 August 2020 | [ICO, Age appropriate design: a code of practice for online services](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/childrens-information/childrens-code-guidance-and-resources/age-appropriate-design-a-code-of-practice-for-online-services/) | `R` | UK statutory code issued on this date and in force from 2 September 2020. ICO jurisdictional applicability MUST be assessed separately; this document does not make that determination. |
| September 2022 | [US Federal Trade Commission, Bringing Dark Patterns to Light](https://www.ftc.gov/reports/bringing-dark-patterns-light) | `R` `P` | Supports review of deceptive and manipulative design. It is not child-specific product research, and legal applicability requires separate assessment. |
| 12 December 2024 | [W3C, Web Content Accessibility Guidelines (WCAG) 2.2](https://www.w3.org/TR/WCAG22/) | `A` | Used here as an accessibility benchmark for native Windows UI. WCAG is a web standard; this document makes no native Windows WCAG conformance claim. |
| 29 April 2021 | [W3C, Making Content Usable for People with Cognitive and Learning Disabilities](https://www.w3.org/TR/coga-usable/) | `A` `V` | Supplemental Working Group Note for web content and applications, including clear content and user testing. It is not a WCAG conformance requirement, a native standard, or child-specific proof. |
| 8 July 2026 | [Microsoft, Guidelines for progress controls - Windows apps](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/progress-controls) | `A` | Native Windows platform guidance for determinate and indeterminate progress. It does not establish child comprehension or emotional outcomes. |
| 2008 | [Joussemet, Landry, and Koestner, A self-determination theory perspective on parenting](https://doi.org/10.1037/a0012754) | `E` | Indirect parenting evidence relevant to autonomy support and controlling language. It is not direct evidence about parental-control UI. |
| 1999 | [Deci, Koestner, and Ryan, A meta-analytic review of experiments examining the effects of extrinsic rewards on intrinsic motivation](https://doi.org/10.1037/0033-2909.125.6.627) | `E` | Indirect evidence for caution around controlling rewards. It does not prove the effect of this product's reward presentation. |
| 5 November 2018 | [American Academy of Pediatrics, What's the Best Way to Discipline My Child?](https://www.healthychildren.org/English/family-life/family-dynamics/communication-discipline/Pages/Disciplining-Your-Child.aspx) | `E` | Parenting guidance supporting clear limits and avoidance of harsh or shaming language. It is indirect evidence for UI and not a clinical outcome claim. |
