# ASM-GEAR-BRIDGE-X1 — Gear interface endpoint unification

## Executive verdict

**Accepted.** First-class Gear semantics now cross Subassembly boundaries as typed exposed endpoints and participate in the existing `Interface<Gear>` authority. No gear parameter record, compatibility evaluator, geometry generator, assembly solver, or child-STEP linkage was duplicated.

The Difference Engine assembly substrate is ready: a DigitModule/Register can expose Gear input/output ports through multiple hierarchy levels and connect them with ordinary `Interface<Gear>`. This milestone does not implement the Difference Engine itself.

## Boundary audit

1. `Interface<Gear>` consumed `GearAir` directly through `InterfaceAir<GearAir>`.
2. It could not be exposed because Assembly admitted only `SemanticValue` endpoints, rejected Assembly-profile `Interface<Gear>`, and had no binding from a `SemanticValue` back to the owning `GearAir`.
3. Assembly already represented explicitly authored axes and frames as semantic endpoints. The Gear AIR axis was definition data, but it was not projected as an Assembly endpoint and therefore did not receive occurrence transforms.
4. Assembly does not require a `Semantic Joint` wrapper. Its Interface roles already consume semantic references and structural capabilities; wrapping Gear in a generic Joint would discard richer Gear authority.
5. Type erasure occurred at the profile boundary: Gear parsing produced `GearAir`, while `ParseTree`, `InstanceScope`, `Expose`, and `BindPublicSemantic` carried only the Assembly semantic projection. No Gear binding survived that path.
6. One shared abstraction is sufficient: `TypedSemanticAuthorityBinding<TAuthority>` retains the producer-owned authority and semantic type. The Gear specialization is `TypedSemanticAuthorityBinding<GearAir>`; there is no Assembly Gear metadata copy or adapter chain.

## Endpoint authority

The authority chain is:

```text
Gear declaration
  -> GearAir (definition authority: family, teeth, module, pressure angle,
              pitch geometry, axis, phase, pair-critical derived values)
  -> TypedSemanticAuthorityBinding<GearAir> (typed reference, no copied fields)
  -> Expose Gear Name = internal-path (visibility alias plus relative occurrence path)
  -> occurrence transform (world origin/axis only)
  -> GearAuthoring.EvaluateInterface (the existing Interface<Gear> evaluator)
```

`GearAuthoring.Parse` and hierarchical Assembly both call the same public `GearAuthoring.EvaluateInterface`. Module, pressure-angle, family, internal/external, bevel/miter, ratchet/pawl, ratio, and rotation-sign logic therefore remain single-owned. Assembly adds only the checks that cannot exist without occurrences: transformed axis relation and actual world-space center distance.

`Expose` preserves semantic type. A Gear endpoint cannot be consumed as Axial/Revolute/Fixed by implicit coercion, and a non-Gear endpoint fails the `GearCapable` role.

## Hierarchy evidence

The canonical two-level fixture resolves:

```text
Machine/RegisterA/Output                  public port
Machine/RegisterA/Digit/Gear              source occurrence
Machine/RegisterB/Input                   public port
Machine/RegisterB/Digit/Gear              source occurrence
```

The Register and DigitModule definitions are each solved once and reused by occurrences. A 100-occurrence test proves one Digit definition backs 100 occurrence-specific ports with deterministic identities. An unexposed child Gear remains unreachable and produces `assembly-internal-member-hidden`.

The executable flagship reports `Register/Digit0/Output` as the port, `Register/Digit0/OutputGear` as its source, definition `OutputGear`, and occurrence `assembly-instance:Register.Digit0.OutputGear`. Phase `4.5deg` survives the alias.

## Compatibility evidence

The flagship pair is compatible:

- A: 40-tooth module-2 spur Gear at `Register/Digit0/OutputGear`
- B: 20-tooth module-2 spur Gear at `Register/Digit1/InputGear`
- ratio: `2`
- rotation sign: `-1`
- expected/actual center distance: `60mm / 60mm`
- transformed axes: parallel `[0,0,1]`

Typed invalid fixtures preserve the existing Gear failure authority for module mismatch and pressure-angle mismatch. Assembly occurrence validation rejects wrong center distance and wrong axis relation with the same `firmament-gear-interface-incompatible` diagnostic family and full hierarchical paths. Separate fixtures reject a non-Gear endpoint, a private Gear, and an invalid nested exposed path.

Internal/external spur, straight bevel, miter, and ratchet/pawl pairs all pass through the bridge and existing evaluator. Ratchet and Pawl remain in this endpoint only because GEAR-LIB-X0 already models their relationship in `Interface<Gear>`; no new involute assumptions were imposed on them.

## Geometry and AP242

Gear leaf definitions call `GearAuthoring.Materialize`. Assembly then follows its ordinary definition/occurrence execution and one whole-tree AP242 export. The flagship has two geometric Gear definitions and four leaf occurrences. No fixture names or imports a child STEP file.

A fresh packaged CLI, run outside the repository against a copied flagship, produced identical AP242 twice:

`BDA67B509EE722480DA0AFD37F3460634716D0C811AF130EF3265B2FBCAC079E`

Reimport succeeded with two geometric definitions and six product occurrences (including hierarchy occurrences). Gear endpoint metadata remains compiler inspection data; STEP geometry and product structure use the existing deterministic AP242 path.

## Validation

- Release solution build: passed, zero warnings/errors.
- Gear plus bridge focused tests: 28 passed.
- Assembly focused tests: 100 passed.
- CLI tests: 440 passed; public documentation qualification: 13 passed.
- Editable V8 regression: 3 passed.
- Bridge qualification: direct, exposed, nested, shared definition, phase, transformed axis, ratio/sign, center distance, internal spur, bevel, miter, ratchet/pawl, private access, endpoint type mismatch, seven invalid fixtures, 100 occurrences, deterministic M1/AP242, reimport, and no child STEP dependency passed.
- Canonical qualification: all five new AssemblyInterfaces Gear fixtures passed. The corpus runner then stopped only on the pre-existing `Assembly/annular-pair.firmament` `LegacyExplicit` policy scan after all canonical operations themselves passed.
- Full solution concurrent run: 3,520 passed and three unrelated timing-budget tests failed under contention (one recipe timing assertion and two display tessellation budgets). All five isolated theory/test cases passed immediately; focused CLI and Assembly suites are fully green.
- Repository layout guard: passed (4,164 tracked files).
- `git diff --check`: passed (line-ending notices only).

## Public surface and limits

Recommended syntax is only:

```firmament
Expose { Gear Output = OutputGear; }
Interface<Gear> Transfer { A: RegisterA.Output; B: RegisterB.Input; }
```

No `Gear<T>`, general mate solver, contact mechanics, torque dynamics, hierarchy flattening, private-boundary bypass, or external semantic STEP round-trip was added.
