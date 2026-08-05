# Profile composition X1

`Compose` is the first source form for one prismatic material body whose section changes at named axial levels. Its doctrine is:

> Compose material regions first. Emit topology once.

```firmament
Struct Part {
    Compose Body {
        Placement World {
            Anchor: [0mm, 0mm, 0mm]
            ProfilePlane: XY
            Axis: +Z
            ReferenceDirection: +X
        }
        Base Stock { Profile: Plate; From: 0mm; To: 10mm; Role: Stock }
        Add Boss { Profile: Pad; From: 10mm; To: 15mm; Role: Boss }
        Remove Relief { Profile: Pocket; From: 7mm; To: 10mm; Role: Relief }
    }
}
```

`Base` is mandatory and unique. `Add` and `Remove` retain operation name, referenced scaffold-backed `Profile`, interval, role, and source span. The parser resolves every named profile independently using the existing `Point2`/`Line2`/`Circle2` profile route.

`Placement` makes the positional contract explicit. X1 accepts the current production world placement only: anchor `[0,0,0]`, profile plane `XY`, extrusion axis `+Z`, and in-plane reference direction `+X`. Other anchors/orientations reject rather than being silently ignored. Older fixtures retain a labeled legacy implicit placement; reconstruction artifacts should declare it.

The Feature AIR is `PrismaticProfileCompositionFeature`; lowering creates `PrismaticSectionStackConstruction` with sorted critical levels, open slabs, explicit material regions, transition regions, provenance, and analytic volume. The emitter owns one `PrismaticSectionStackBrepPlan` and preserves deterministic slab partitions rather than aggressively merging faces.

X1 normalizes each active slab through `ProfileArrangement2D`: it analytically intersects bounded line-line, line-circle, and circle-circle source curves, splits every source parameter interval deterministically, samples the two sides of each atomic fragment with the set predicate `(Base ∪ Add) − Remove`, and retains only material/void boundaries. Source curves are split by geometry, but never stripped of provenance. The resulting material-left loops are reconstructed deterministically; the production route rejects disconnected material, dangling boundaries, zero-area loops, and ambiguous angular continuation.

Coincident source support is normalized by side classification: duplicate Add/Add and Remove/Remove fragments vanish as internal boundaries, while the stable lowest source ID retains the provenance of a surviving boundary. Point-only tangencies naturally produce multiple outer loops and are rejected instead of being perturbed into a crossing. The arrangement output is converted to the same explicit outer/inner section-region contract consumed by the one authoritative section-stack BRep plan; no operation solids or 3D Booleans are materialized.

At a critical level the compiler compares the two adjacent sections. `below - above` produces upward material (including a pocket floor); `above - below` produces downward material. A difference may yield several disconnected horizontal cap patches while the slabs remain single connected regions; CTC-01's Z=-60 shoulder is the pressure case and yields two exact patches. Lines become planar vertical faces and arcs cylindrical vertical faces. Directed edge topology is shared across slabs and caps; coedge reversal is used exactly once per traversal.

Use `aetheris inspect-compose file.firmament --json` before `build`. The report includes operations, levels, active slabs, areas, transition region counts and areas, analytic volume, independently evaluated in-memory BRep status/volume/error bound/diagnostics, plan signature, and per-slab arrangement source/intersection/fragment/loop/perimeter/timing/provenance evidence.

`aetheris inspect-profile composition.firmament --json` also accepts a Compose source and reports every referenced Profile independently, including validation, signed area, line/arc counts, and segment provenance. This keeps profile evidence available without extracting temporary standalone sources.

The raised-pad, shallow-pocket, and through-cut fixtures remain enclosed manifold STEPs with analytic/in-memory/reimported BRep agreement. Overlapping additive rectangles and overlapping removal rectangles now normalize to one exact line-only region and round-trip as an enclosed analytic STEP. The line parameter is normalized endpoint distance `[0,1]`; an arc parameter is normalized along its explicit signed angular sweep. This distinction is required because STEP export may resolve a vertex from its incident edge trim.

The mixed line/arc additive fixture additionally proves that a split arc is emitted as a `CIRCLE` with its actual bounded angular trim, including reverse traversal, rather than as a full circle or a spline. Profiles are the bounded planar escape hatch for custom prismatic blockout, not the default representation for every semantic CNC feature.

The CTC-01 blockout remains one `PrismaticSectionStackBrepPlan` with three normalized slabs and four transitions. The real model required two narrow production changes: transition region sets for multiple exact cap patches, and a deterministic alternate visible-bridge search for tessellating one inner loop inside a concave analytic outer loop. Neither change adds a source authoring feature, a 3D Boolean, fuzzy healing, or a second topology owner.

`Rect2` may replace manually declared rectangular corners and sides in this source route. It uses `Center: [xmm, ymm]` and `Size: [wmm, hmm]`; derived finite sides are traced as `Base.Bottom`, `Base.Right`, `Base.Top`, and `Base.Left` with corresponding named corners.

Profiles are a bounded planar blockout and escape-hatch mechanism. Higher-level semantic CNC features remain preferred where they express the actual intent.

## Semantic shaft holes in a composition

`Compose` admits one bounded semantic subtractive form when a circle Profile would hide manufacturing intent:

```firmament
Hole<Shaft> Mount {
    Center: [25mm, 10mm]
    Diameter: 8mm
    End: ThroughAll
    Role: MountingHole
}
```

This X1 route accepts `Shaft` and `ThroughAll` only. It statically lowers the declaration to an erased four-arc removal Profile across the composition interval, participates in the same exact arrangement/set semantics, and emits no 3D Boolean tool. The semantic Hole stable ID remains the source for entry/exit loops and edges plus all cylindrical wall-face descendants. Entry and exit are derived from the retained host boundary, so `ThroughAll` remains correct when material at the hole center occupies only part of the composition's global axial range.

Use `inspect-compose --json` for the compact `shaftHoles` table and `inspect-selections --json` with `Source: Hole(Name)` plus `Target: HoleEntry`, `HoleExit`, or `HoleWall`. Stepped, blind, counterbored, countersunk, or non-`+Z` composed-host holes remain outside this bounded route.
