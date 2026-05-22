# CIR-RECOVERY-V2 — Through-hole recovery executor (box/cylinder exact BRep)

## Scope

CIR-RECOVERY-V2 executes an already-selected `ThroughHoleRecoveryPlan` for exactly one specialization:

- host: rectangular box
- tool: cylindrical
- profile: circular
- axis: Z

It is internal-only execution and does not change STEP/export, public API, fall-forward, or rematerializer wiring.

## Execution route

The executor uses existing trusted BRep machinery:

1. `BrepPrimitives.CreateBox(width,height,depth)`
2. `BrepPrimitives.CreateCylinder(radius,height)`
3. translate both bodies by plan translations
4. `BrepBoolean.Subtract(box,cylinder)`
5. return `BrepBody`

## Constructor conventions confirmed

From existing primitive + boolean tests and primitive implementations:

- `CreateBox(width,height,depth)` creates a body centered at origin with extents `[-w/2,+w/2]`, `[-h/2,+h/2]`, `[-d/2,+d/2]`.
- `CreateCylinder(radius,height)` creates a Z-axis cylinder centered at origin with caps at `z=±height/2`.
- translation is applied by `FirmamentPrimitiveExecutionTranslation.TranslateBody` after primitive creation.
- plan `ThroughLength` is used as cylinder height, clamped to at least host depth to preserve through-span semantics (`max(ThroughLength, HostSizeZ)`).

## Diagnostics

The executor emits deterministic stage diagnostics for:

- executor start
- plan acceptance/rejection
- box primitive construction
- cylinder primitive construction
- subtract invocation/success/failure
- result production
- explicit V2 non-goals: no STEP export and no rematerializer/fall-forward wiring

## Non-goals (preserved)

- no STEP export wiring
- no `NativeGeometryRematerializer` integration
- no `CirBrepMaterializer` behavior changes
- no new topology builder
- no generic boolean/through-hole recovery expansion
- no generated topology naming

## SEM-A0 alignment

SEM-A0 guardrails remain preserved (`docs/firmament-semantic-topology-naming.md`): this milestone adds no generated topology naming and no new selector/provenance exposure.

## V3 next milestone

CIR-RECOVERY-V3 should add a narrow STEP smoke path for successful executor output while keeping execution strategy bounded.
