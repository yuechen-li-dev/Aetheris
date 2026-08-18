# Construction Plane Hole source X3

Construction Plane Hole placement is a source-level spatial contract, not a
disguised face attachment. New semantic Hole sources resolve their location and
drilling axis from a traced Construction Plane; they never require an entry-face
name, a raw BRep id, a world-space center, or a world-space axis.

## Admitted source form

```firmament
Concept Struct SideLayout {
    PositiveXDatum: Plane {
        Origin: [-50mm, 0mm, 0mm]
        Normal: [1, 0, 0]
        Up: [0, 0, 1]
    }
}

Construction Plane PositiveXWorkplane { Trace: SideLayout.PositiveXDatum }

Struct SideHoleBracket {
    Box Base { Size: [100mm, 60mm, 12mm] }
    Modify Base {
        Hole<Shaft> SideMount {
            From: PositiveXWorkplane
            Center: Point2(10mm, 6mm)
            Diameter: 8mm
            End: ThroughAll
        }
    }
}
```

`Center` is local `(U,V)` in the Construction Plane. Local `+Z` is the drill
direction. In the fixture above the resolved world mouth is `[-50, 10, 6]` and
the drilling direction is world `+X`, proving that placement is not an implicit
world-Z or face-local operation.

## Placement and lowering contract

The bound declaration has an explicit placement union:

- `FirmamentV2ConstructionPlaneHolePlacement` holds the resolved Construction
  Plane, its source Concept Plane identity, and the local center.
- `FirmamentV2FaceLocalHolePlacement` preserves the existing compatibility lane.

Exactly one mode is valid. A `From` Construction Plane cannot be combined with
legacy `On` face placement. Missing `Center`, an unknown plane, or a non-
`ThroughAll` termination produces a typed source diagnostic. Construction Plane
resolution is performed by the existing Concept Plane/Construction Plane trace;
the Hole binder consumes that immutable result and does not derive a frame from
final BRep geometry.

New Hole sources lower directly to `AirConstructionPlaneHolePlacement`. Its
stable Hole id, Construction Plane id, source Concept Plane id, frame origin and
axes, local center, world mouth center, diameter, termination, provenance, and
source span survive to materialization. No fake face name is synthesized. The
legacy route lowers to `AirFaceLocalHolePlacement` and remains supported for
existing fixtures only.

## Bounded material route

Production dispatch is explicit:

```text
Firmament Hole From ConstructionPlane
  -> AirConstructionPlaneHolePlacement
  -> LocalFrameHoleBRepPlan
  -> ProfileExtrusionBRepPlan materialization
  -> exact STEP / reimport / M8
```

X3 admits only a simple Box host, an orientation accepted by the local-frame
planner (the verified signed-permutation frame with mouth at local `Z=0` and
host material in local `+Z`), and `ThroughAll`. The plan publishes the resolved
local host interval and owns the semantic correspondence: Mouth loop/edges,
Exit loop/edges, and Shaft-wall faces. Selection resolution consumes those
published descendants as `LoopSet`, `EdgeSet`, and `FaceSet`; it does not search
final coordinates.

Unsupported host/frame/termination requests fail explicitly through the
Construction Plane host, orientation, or extent diagnostic paths. X3 does not
claim blind/flat-bottom/DrillPoint geometry, `ShaftDepth`, `TotalDepth`,
composed-host traversal, arbitrary proper frames, CTC side holes, countersinks,
or counterbores. The extension point for blind DrillPoint is
`LocalFrameHoleBRepPlan`: add the local termination surfaces/loops and their
plan-owned correspondence before asking the shared profile-extrusion
materializer to make topology.

## Inspection and Cadmata

`aetheris inspect-selections <source> --json` emits the source-to-plan evidence
for each Construction Plane Hole: feature and plane ids, frame origin/axes,
local and world centers, diameter/radius, `ThroughAll`, host interval,
`LocalFrameHoleBRepPlan` id, source declaration, AIR placement, plan-owned
descendants, final topology ids, provenance, selections, diagnostics, and
timings.

The `construction-plane-through-hole` Cadmata fixture publishes the compiler-
owned Concept Plane and Construction Plane, local center, drill axis, analytic
cylindrical envelope, Mouth/Exit loops, and Shaft-wall descendants. Selecting
the Hole uses stable correspondence to highlight its frame and material
descendants; selecting the Construction Plane exposes the Hole as its consumer.
The viewport does not infer the cylinder from tessellation.

## Evidence

The production fixture is
`fixtures/Hole/valid/construction-plane-through-hole.firmament`.
Its exact source build exports a STEP artifact with SHA-256
`A6B557BD3DC54A6FF88686B82FA96F7CCFA4F40FC00E3353F40A59704204FB8C`.
The published local interval is `[0, 100]`, so the analytic removed volume is
`pi * 4^2 * 100 = 1600*pi` cubic millimetres. The exact independent STEP
analysis reports `72000 - 1600*pi = 66973.45175425633` cubic millimetres. It
deduplicates the two seam-partitioned cylinder faces as one physical bore. STEP
reimport reports one body, one enclosed shell, 8 faces, 18 edges, 12 vertices,
6 planar faces, and 2 cylindrical faces. M8's deterministic trimmed-face
boundary integral reports `66987.79421875` with conservative error bound
`7300.494418725005`; it is enclosed and orientation-consistent. Both bodies'
non-Z cylinder axis is the Construction Plane's world `+X` axis.
