# CIR-RECOVERY-V1B: ThroughHoleRecoveryPolicy scaffold

## Why semantic-through-hole (not pair-specific top-level)

CIR-BREP-X6 recommended a semantic policy lane (`ThroughHoleRecoveryPolicy`) with bounded internal host/tool specializations instead of promoting a pair-specific `BoxCylinderThroughHolePolicy` to top level. This keeps policy growth manageable as feature families expand.

## First specialization in V1B

V1B adds one internal specialization through existing production recognizer behavior:
- host: rectangular box
- tool: cylindrical subtractor
- feature: strict-clearance through-hole along Z

Recognizer source remains `CirBoxCylinderRecognizer`.

## Recovery plan shape

`ThroughHoleRecoveryPlan` carries semantic execution intent for V2:
- host/tool/profile/axis kinds
- through length, host/tool dimensions, translations
- entry/exit surface participation
- expected patch roles and trim roles
- capability + diagnostics

This is intentionally semantic and bounded (not a topology graph).

## Scoring and evidence

`ThroughHoleRecoveryPolicy` uses deterministic fixed score `1000` for recognized canonical semantic through-hole with `ExactBRep` capability, intentionally above fallback bands.

Evidence includes:
- `semantic-through-hole`
- `rectangular-box-host`
- `cylindrical-tool`
- `through-hole`
- `strict-clearance`
- translation support note
- replay consistency/diagnostic note
- expected patch/trim role hints

## Rejected cases

Policy rejects and forwards recognizer reasons/diagnostics for:
- non subtract roots
- non box/cylinder operands
- blind cylinders
- tangent/grazing cuts
- unsupported transforms
- other unsupported recognizer cases

## Not included in V1B

- no BRep body construction
- no STEP export wiring
- no rematerializer/fall-forward changes
- no boolean-generic recovery
- no shell stitching / topology mutation / naming

## Next milestone (V2)

Consume `ThroughHoleRecoveryPlan` in an executor that builds exact BRep surfaces/trims for this semantic lane, then integrate into materialization pipeline with capability-gated fallback.
