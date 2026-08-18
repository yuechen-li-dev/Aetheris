# CIR-RECOVERY-V14: stepped-hole explicit-placement corpus hardening

## Scope

V14 hardens the existing bounded **three-level stepped-hole** path from V13.2/V13.3 with additional corpus coverage.

Non-goals:
- no new hole variants,
- no arbitrary N-level stepped-hole support,
- no generic profile-stack executor,
- no STEP exporter behavior changes,
- no public API/CLI expansion.

## Added coverage

- Entry side corpus:
  - top-entry stepped-hole executes and exports,
  - bottom-entry stepped-hole executes and exports when admissible (explicitly rejected otherwise).
- Translation corpus:
  - translated host/tool geometry is recognized and executed with explicit placement z-span checks.
- Placement validation corpus:
  - reject unknown anchor,
  - reject blind tier through-flag misuse,
  - reject invalid z-span,
  - reject anchor mismatch,
  - reject missing placement diagnostics,
  - reject blind z-span outside host.
- Boundary/tolerance rejection corpus:
  - equal radius/depth ordering failures,
  - tangent/oversized large-tier radius rejection.
- Cross-steal corpus:
  - stepped does not steal through/blind/counterbore/countersink cases.

## Entry-side support status

Current stepped variant supports both top-entry and bottom-entry based on explicit anchor detection (`Top`/`Bottom`) for medium/large blind tiers while preserving a through-anchored small tier.

## Translation behavior

Current stepped path remains translation-safe for translation-only wrappers by preserving host/tool translations and deriving explicit z-spans from translated geometry.

## Invalid placement checks

Executor-side explicit-placement guardrails now reject before boolean for:
- unknown anchor,
- invalid through flags,
- invalid or degenerate z-spans,
- missing placement diagnostics,
- anchor mismatch,
- blind-tier z-span outside host bounds,
- through-tier z-span not covering host bounds.

## Tolerance and boundary policy

V14 keeps existing tolerance conventions and strict ordering/clearance checks:
- strict radius ordering: `small < medium < large`,
- strict depth ordering: `large < medium < through`,
- strict host side-wall clearance for largest tier.

## Cross-steal behavior

Hole family selection remains stable via `HoleRecoveryPolicy` judgment path; stepped rejects non-stepped motivating shapes so canonical through/blind/counterbore/countersink selection still wins.

## Recommended next milestone

If expanded support is needed, next step should be an explicitly scoped semantic recovery milestone for additional bounded stepped subclasses (for example, one additional constrained profile family), not arbitrary profile-stack generalization.
