# CIR-F14 — First Surface-Family Shell Assembly (Bounded `subtract(box,cylinder)`)

## Outcome

**Meaningful progression**.

A bounded assembler entrypoint was added and hard-gated by F13 readiness (`no shell-readiness, no assembly`).
The real blocker is now isolated: emitted patch bodies do not yet expose deterministic cross-patch topology identity remap metadata needed to merge patch-local topology into one coherent shell using evidence-only pairing.

## Supported shape (current)

- `subtract(box,cylinder)` only (canonical through-hole family).

## Readiness gate

`SurfaceFamilyShellAssembler.TryAssembleBoxMinusCylinder` always runs `ShellStitchingDryRunPlanner.Generate(root)` first.

- If readiness is not `ReadyForAssemblyEvidence`: reject and return diagnostics.
- If readiness is accepted: still refuse assembly until topology identity remap metadata exists.

## Assembly strategy status

Current implementation is **diagnostic-first scaffold**.
It consumes F10.8/F11/F13 evidence and emitted-patch summaries, but intentionally does not guess topology merges.

## Evidence requirements for next step

Need explicit mapping from pairing evidence token (`InternalTrimIdentityToken` / planned pair token) to emitted patch-local edge/coedge identities so assembly can stitch by evidence, not coordinate coincidence.

## Non-goals preserved

- no production rematerializer replacement,
- no STEP behavior changes,
- no CLI exposure changes,
- no generated topology naming/selectors.

## Next step

Extend planar/cylindrical emission results with stable edge/coedge identity metadata keyed by pairing evidence tokens, then implement deterministic shell merge and add success-path export smoke.
