# SURF-X3b — generalized BodyState construction authority

## Executive verdict

**Accepted for the documented bounded lanes.** `BodyState` now owns a versioned `ConstructionState` containing a generalized `BaseConstruction` and an ordered linear list of typed operations. Rebuild realizes the base and replays each operation transactionally. `AddSectionChain` and `RemoveSectionChain` are persistent semantic operations and use a SectionChain-specific shared-topology construction path, not public generic CSG history.

This is not a claim of arbitrary SectionChain/body composition. X3b qualifies one planar housing east-support addition and one west-to-east through-duct removal. Unsupported bases, supports, frame/profile topology, crowns/patch predecessors, or payload versions fail closed.

## Authority audit

Before X3b, `BodyState` stored a deterministic state ID, one predecessor ID, stable body ID, authored name, realized BRep, a mutated `HousingConstruction`, semantic inventory, the latest `GeometricDelta`, validation evidence, persistent geometry associations, AP242 PMI/interfaces, and optional blend judgment trace.

The mutated `HousingConstruction` was authoritative during rebuild. `OffsetRegion`, `ReplaceRegion`, `BlendBoundary`, and `HoleFeature` each produced a new recipe and called `SculptedHousingBrepBuilder.Build`. Offset/Replace/Blend/Hole therefore survived only insofar as their effect had been collapsed into housing fields. Blend retained judgment evidence but its selected patch was the actual rebuild input. Imported-face replacement mutated only a realized imported BRep and a housing-shaped evidence recipe; it did not have a process-independent generalized base authority. Standalone SectionChain materialization bypassed BodyState entirely. A transient SectionChain graft would consequently have been erased by the next housing rebuild.

Housing-specific assumptions were present in the `BodyState.Construction` type, every SURF sculptor's dimension checks, inventory/PMI construction, blend candidate realization, safe-hole rebuild, and CLI inspection shape. The generalized portion needed to be base identity, operation payload/identity, reads, authorization, preservation, predecessor/output relationship, replay, schema version, and failure semantics. Geometry realization can remain bounded per operation family.

## Old versus new authority

```text
Before
HousingConstruction -> BRep -> latest transient/mutated sculpt evidence

After
BaseConstruction + typed ConstructionOperation[]
    -> failure-atomic replay
    -> validated GeometricDelta/correspondence
    -> realized BRep evidence
```

`HousingBaseConstruction` is the first admitted base kind. The interface and versioned envelope no longer require all future states to be housing-derived; no unsupported imported/ordinary CAD base kind is falsely advertised yet.

## Construction-state contract

The schema is `aetheris.surfacing.construction-state` version 1. Every operation envelope retains `OperationId`, `OperationKind`, payload version, authored and output state relationship, typed payload, reads, `MayModify`, authorized envelope, preservation contracts, delta, validation evidence, and replay status. JSON serialization has explicit polymorphic base/operation/SectionProfileCurve discriminators and a normalized `Direction3D` converter.

Replay is linear geometric SSA. Full replay is the correctness baseline. Every operation after the first must name the preceding authored operation's output as its predecessor; reordered serialized operations fail with `bodystate-operation-order-invalid`. An operation order/payload/identity/version mismatch or a geometric validation failure emits `bodystate-operation-replay-failed`, returns no new output, and exposes the last accepted predecessor as authoritative. No BRep mutation from the failed operation is committed.

## Persistent operation table

| Operation | Persistent typed payload | Rebuild tested | Delta retained |
|---|---:|---:|---:|
| OffsetRegion | yes | yes, including JSON round-trip | yes |
| ReplaceRegion | yes | existing X1/X1a path plus generalized envelope | yes |
| BlendBoundary | yes; request, policy, candidates/selection evidence retained | yes; remains BlendBoundary rather than ReplaceRegion | yes, including judgment provenance |
| HoleFeature | yes | yes, before and after admitted additive sculpt | yes |
| AddSectionChain | yes; chain and attachment retained | yes, source/JSON/repeat replay | yes |
| RemoveSectionChain | yes; chain and penetration supports retained | yes, source/repeat replay | yes |

## SectionChain blocker closure

| X3a blocker | X3b result |
|---|---|
| arbitrary changing SectionChain not admitted in BodyState | bounded four-line-span ruled chains are admitted on explicit planar housing supports |
| HousingConstruction rebuild erases graft | construction authority replays the typed chain; a downstream safe hole uses retained chain construction |
| AddSectionChain unavailable | first-class Firmament operation, semantic terminal/support attachment, direct one-shell topology, replay and invalidation tests |
| RemoveSectionChain unavailable | first-class Firmament operation, explicit two-support through corridor, inner opening loops, reversed cavity shell, replay tests |

## Bounded kernel expansion

No generic freeform Boolean was added. `SectionChainHousingBrepBuilder` consumes known sections, support identity, attachment/penetration topology, and ordered span correspondence. It emits housing faces, shared attachment/opening edges, ruled transition faces, cap where applicable, hole cylinders, one shell, and face-local pcurves directly.

The admitted addition uses `HousingSideEast`, support-relative +X frames, an `Open` attached first section equal to the complete support boundary, strictly exterior later sections, and a `Cap` free end. The admitted removal uses `Open/Open`, monotonically increasing +X sections from `HousingSideWest` to `HousingSideEast`, and profiles strictly inside the Y/Z housing boundary. Four ordered line spans are admitted; each transition is exact planar when coplanar and the builder has a non-rational bilinear B-spline path when not. General freeform Booleans, rotated arbitrary supports, profile topology changes, crowns/replacement-patch predecessors, and G1/G2 remain fail-closed.

## Flagship evidence

| Evidence | Additive grip | Subtractive duct |
|---|---:|---:|
| BaseConstruction | `housing-base`, Housing/v1 | `housing-base`, Housing/v1 |
| operation | `GripAdded.AddSectionChain` | `DuctRemoved.RemoveSectionChain` |
| sections / transitions | 5 / 4 | 5 / 4 |
| support | `HousingSideEast`, terminal `Attach` | `HousingSideWest`, `HousingSideEast` |
| preserved | bottom mounting interface, mounting-hole pattern | bottom mounting interface, mounting-hole pattern |
| bodies / shells after | 1 / 1 | 1 / 1 |
| faces / edges / vertices after STEP reimport | 24 / 50 / 28 | 24 / 54 / 32 |
| pcurves / edges | 100 / 50 | 108 / 54 |
| maximum pcurve error | `1.464821375527116E-14` mm | `7.32410687763558E-15` mm |
| surface inventory | 22 Plane, 2 Cylinder | 22 Plane, 2 Cylinder |
| rational / faceted product fallback | 0 / 0 | 0 / 0 |
| structural reimport | enclosed manifold | enclosed manifold |
| state ID | `state-5ddd57c502db352b2ea0` | `state-5e2cb0816724ebbb96e5` |

The additive independent diagnostic volume increases from `64165.64418886558` to `86201.64418886561` cubic millimetres. The subtractive SectionChain prismoid estimate is `7630.959341876355` cubic millimetres. The generic tessellated mass diagnostic does not subtract inner loops reliably and is therefore not promoted to removal authority; enclosure, reversed cavity topology, two explicit openings, edge incidence, and the semantic prismoid calculation are the qualified evidence.

## Replay, locality, and preservation

For identical authority, initial, deserialized/reloaded, and repeat replay produce the same StateId and byte-identical STEP in tests. The additive attachment is support-relative: changing base width from 80 mm to 90 mm preserves semantic `HousingSideEast`, translates the chain, and derives a new deterministic state. Replacing the support with `DeletedSupport` produces `bodystate-operation-support-missing`; no proximity binding occurs.

Every rebuild rechecks `realized bounds subset-of AuthorizedRegion`, SectionChain self-intersection qualification, intended attachment/penetration, remote intersection constraints, enclosure/orientation/pcurves, operation requirements, and exact semantic preservation fingerprints. `GeometricDelta` explicitly marks preserved, replaced, and introduced identities. Old side-support selectors become stale because their explicit successors are recorded rather than name-matched.

## Multi-operation and downstream feature witnesses

[`surf-x3b-multi-operation-replay.firmament`](../../fixtures/Canonical/BodyState/surf-x3b-multi-operation-replay.firmament) retains and replays `HoleFeature -> HoleFeature -> AddSectionChain`. [`surf-x3b-safe-feature-after-add.firmament`](../../fixtures/Canonical/BodyState/surf-x3b-safe-feature-after-add.firmament) retains `AddSectionChain -> HoleFeature`; the second operation reconstructs the grip instead of reverting to housing-only geometry.

## Manual artifacts

Generated artifacts follow the local-artifact policy and are not committed:

| Artifact | SHA-256 |
|---|---|
| `artifacts/local/surf-x3b-add-section-chain-grip.step` | `BF38176C38E2C3B4251EF487E0C81A7F7FCD3CF7DF0477736D4461D80EFDE9A3` |
| `artifacts/local/surf-x3b-remove-section-chain-duct.step` | `0AD39A465F25A0AD5C51FCC688011CB699BE75725F901D81984192C90692C9FF` |
| `artifacts/local/surf-x3b-multi-operation-replay.step` | `214D353818FC039912C0B00B73E098405795A82CD1B7F1BB5518B12ED0BB3F7D` |

Reproduce them with `aetheris build fixtures/Canonical/BodyState/<fixture>.firmament --output artifacts/local/<name>.step`.

## Validation record

- `dotnet build Aetheris.slnx -c Release --no-restore -m:1`: passed with 0 warnings and 0 errors.
- `dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1`: 3,191 passed, 0 failed. The pre-existing `Aetheris.FrictionLab.Tests` assembly reports no discoverable tests; all other test assemblies passed.
- `scripts/Test-CanonicalFixtures.ps1 -NoBuild`: all 112 canonical fixtures passed. This includes the four X3b BodyState programs, all four standalone SectionChain fixtures, and `Integration/paperclip-maximizer.firmament`.
- Fresh packed CLI: `Aetheris.CLI.2.0.0-preview.3.nupkg` installed into an empty isolated tool path. Its source validator accepted both flagships with zero warnings/fatals; it then built, inspected, analyzed, and reimport-verified the additive flagship. STEP reimport was `Valid`, one body/one shell was retained, and the installed tool reproduced SHA-256 `BF38176C38E2C3B4251EF487E0C81A7F7FCD3CF7DF0477736D4461D80EFDE9A3`.
- The same packed CLI built and analyzed the subtractive flagship with zero build diagnostics and the expected `state-5e2cb0816724ebbb96e5`, one-body/one-shell enclosed-manifold result.
- The invalid missing-support fixture failed closed with process exit 1, no STEP output, and `bodystate-operation-support-missing` among its typed diagnostics.
- NativeAOT, Forge interop, browser client, and VS Code extension lanes were not run because X3b changes only the managed Surfacing/CLI path and its fixtures; no code or packaging contract in those lanes changed.

The canonical harness now declares the already-documented `plastic-shell-constant-section-feature-zero-draft` warning for the exact six PlasticShell fixtures that intentionally exercise zero-draft constant sections. Missing expected diagnostics and all undeclared warning/error/fatal diagnostics remain qualification failures.

## Fresh-agent authoring audit

Two clean-context agents independently read the public documentation and authored test programs without implementation guidance. Both selected the intended `SculptState` / typed SectionChain operation path, correctly distinguished Add from Remove terminal/support semantics, and predicted persistent replay and downstream-hole retention. Their only ambiguity was whether a wider base should move the east support symmetrically and whether subtractive station coordinates were support-relative. The public contract now states this explicitly: admitted Add stations are relative to the semantic east support; admitted Remove stations are absolute +X corridor stations spanning west to east. The audit also prompted clarification that standalone SectionChain emits `.evidence.json`, while BodyState build emits construction authority and delta in `.delta.json`.

## Known limits

- `HousingBaseConstruction` is the only realized base kind in X3b. The interface is generalized; imported STEP and ordinary CAD bases remain future admitted kinds.
- Imported-face adoption is not serialized as a generalized base and is not claimed as process-independent replay.
- Add/Remove composition deliberately excludes crown/ReplaceRegion predecessors and arbitrary support surfaces.
- The compact sculpting-source SectionChain form currently accepts ordered rectangular line-span `Station` values. Standalone Concept Path/Profile SectionChain authoring remains available through the SectionChain command.
- The subtractive mass-property verifier's tessellation route does not account for inner loops as occupied-volume subtraction; X3b reports that limitation rather than presenting its larger diagnostic magnitude as physical volume.
- Incremental prefix caching is not implemented; full replay remains the correctness path.
- G1/G2 SectionChain continuity, rails, arbitrary freeform Boolean completeness, and mesh product fallback remain non-goals.

## Manual review request

Inspect the additive STEP for terminal attachment quality, surface seams, slivers, remote changes, and whether the grip reads as one body. Inspect the subtractive STEP for continuous openings/corridor, internal membranes, taper progression, and preserved mounting geometry.
