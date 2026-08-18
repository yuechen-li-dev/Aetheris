# HOLE-X3 stacked semantic hole components

HOLE-X3 extends the production semantic `AirHoleFeature` lane from simple shaft holes to owned axial stacks for counterbore and countersink holes. The authored source of truth remains one semantic hole feature; stack components are semantic children, not independent cylinder/cone cuts.

## Supported stack shapes

- Simple shaft: one `AirHoleShaftComponent`.
- Counterbore: one entry `AirHoleCounterboreComponent` followed by the shaft component.
- Countersink: one entry `AirHoleCountersinkComponent` followed by the shaft component. The production declaration uses entry diameter plus included angle and derives sink depth from the shaft radius.

Supported placement and end conditions remain the HOLE-X2 subset: face-local top/bottom planar entry placement, Z-aligned entry normal/axis, throughAll, and fixed depth.

## Materialization path and provenance

The lowering/materialization path is:

```text
AirHoleFeature
  -> AirHoleStack owned by that feature
  -> AirHoleSimpleShaftMaterializationPlan with StackKind/StackComponentRoles
  -> ProfileStackExtrudeSpec/ProfileStackLayer implementation furniture
  -> ProfileStackExtrudeExecutor
  -> BRep output
```

`ProfileStackExtrudeSpec` is still only a downstream execution contract. The materialization plan keeps the source `AirHoleFeature`, semantic feature id, source kind, stack kind, and component roles so tests and diagnostics can trace BRep output back to the parent semantic feature.

## Validation behavior

Counterbore validation rejects non-finite or too-small entry diameters, non-positive/non-finite counterbore depths, and counterbore depths that exceed a bounded depth end condition.

Countersink validation rejects non-finite or too-small entry diameters, invalid included angles, non-positive derived sink depths, and derived depths that exceed a bounded depth end condition.

## Deferred features

HOLE-X3 does not add parser syntax, standards or fit libraries, thread/tap geometry, drill-tip geometry, hole groups, patterns, upToFace/upToNext, arbitrary datum placement, STEP/DisplayIR/frontend/product behavior changes, or generic raw 3D boolean authoring.

## Relationship to semantic-hole strategy

This implements the principle from `docs/development/architecture/llm-cad-strategy/07-holes-are-semantic-features.md`: counterbores and countersinks are variants of a semantic hole feature. The entry preparation and shaft remain an owned axial feature stack because later standards, PMI, manufacturing intent, and edits need a single parent hole identity.

## Validation commands

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --no-build --filter "AirHole|Stack|Counterbore|Countersink"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter AirHoleFeature
dotnet test Aetheris.slnx -f net10.0 --no-build --filter "Hole|FirmamentV2|Air"
git diff --check
git status --short
```

Forward link: HOLE-X4 adds the Firmament V2 source hook for `hole<shaft>`, `hole<counterbore>`, and `hole<countersink>` declarations in `docs/development/implementation/hole-x4-firmament-v2-semantic-hole-source.md`.
