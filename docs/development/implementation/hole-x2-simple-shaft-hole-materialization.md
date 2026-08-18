# HOLE-X2 simple shaft hole materialization

HOLE-X2 makes the first production semantic-hole vertical slice executable: a semantic `AirHoleFeature` simple shaft hole can now lower through a bounded Firmament materialization lane into BRep output while preserving the semantic hole as parent intent.

## What materializes now

The implemented executable family is intentionally narrow:

- semantic `AirHoleFeature` only;
- simple shaft hole only;
- planar face-local entry placement on the supported rectangular profile-stack host lane;
- top/+Z and bottom/-Z entry directions for the current rectangular host path;
- shaft diameter/radius from the semantic feature;
- `throughAll` and fixed `depth` end conditions;
- BRep output via the existing `ProfileStackExtrudeExecutor`/safe box-cylinder composition route.

The current production lane uses the same rectangular profile-stack execution infrastructure used by the bounded hole-family materializer work. Face-local `U`/`V` coordinates are validated against the rectangular entry face, preserved in the HOLE-X2 plan, and passed into the profile-stack executor as the shaft center for this simple rectangular host lane.

## Materialization path

The executable route is:

```text
AirHoleFeature
  -> AirHoleSimpleShaftMaterializationPlan
      SemanticFeature / SemanticFeatureId / SemanticSourceKind
      EntryFaceName + face-local U/V
      AxisZ
      Radius
      CutZMin/CutZMax
      EndConditionKind
  -> ProfileStackExtrudeSpec
  -> ProfileStackExtrudeExecutor
  -> BrepBody
```

`ProfileStackExtrudeSpec` is lower-level implementation furniture only. It is not exposed as the AIR semantic source model, and the plan keeps the source `AirHoleFeature` object plus stable feature id so tests and diagnostics can trace the materialized body back to the semantic parent.

## End-condition behavior

- `throughAll` creates one circular cut interval spanning the full host thickness.
- `depth` creates a bounded circular cut interval from the entry face inward plus a solid interval for the uncut remainder.
- Depths larger than the host thickness clamp to the full host span in this lane, which deterministically yields through-like geometry while preserving `EndConditionKind.Depth` in the semantic plan. Invalid non-positive depths remain rejected by HOLE-X1 validation before materialization.

## Unsupported/deferred features

HOLE-X2 deliberately does not add counterbore, countersink, mouth chamfer, drill-tip geometry, thread/tap geometry, standards/fit libraries, hole groups, patterns, arbitrary datum placement, `upToFace`, `upToNext`, non-planar entry faces, multi-body propagation, broad parser syntax, or general raw 3D Boolean authoring.

Unsupported placement produces deterministic diagnostics and no anonymous `CylinderCut` fallback.

## Relationship to `07-holes-are-semantic-features.md`

The semantic rule remains unchanged: holes are semantic features, not anonymous cylinders. HOLE-X2 adds an executable lowering boundary for the smallest safe family while keeping profile stacks, cylindrical faces, safe booleans, trim loops, and BRep details below that boundary.

## Validation commands

The HOLE-X2 changes were validated with:

```bash
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --filter AirHoleSimpleShaft --logger "console;verbosity=minimal"
```

Full milestone validation should also run the solution restore/build and focused `Air`/`Hole`/`FirmamentV2` filters documented in the milestone prompt.

Forward link: HOLE-X3 extends this lane with semantic counterbore/countersink stack components in `docs/development/implementation/hole-x3-stacked-hole-components.md`.
