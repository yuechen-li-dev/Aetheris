# Semantic Profile shared-corner composition M3

## Architecture and JudgmentEngine verdict

M3 adds first-class ownership for the vertex shared by two directed Profile
edges. `SemanticCornerProfileIr` names the corner, both adjacent owners, the
authored operation, the bounded local frame, and provenance. It lowers above
`ResolvedProfile2D` / `PlanarContour2`:

```text
EdgeA suffix --\
                SemanticCorner operation -> exact replacement chain
EdgeB prefix --/                           -> repaired edge carriers
```

The evidence-based JudgmentEngine verdict is **Partial**. AIR edge finishing
uses `JudgmentEngine` where several bounded route or analytic-surface families
can be admissible. Its deterministic admissibility, scoring, tie-breaking, and
rejection machinery is reusable for a future recovered/underspecified Profile
corner. A fully authored Profile chamfer, cutback, taper, or corner notch has
one declared topology and exact setbacks, however. M3 therefore applies hard
constraints and deterministic construction directly; it does not manufacture
a heuristic choice. This follows the AIR lesson that construction history
should emit final topology directly, while retaining JudgmentEngine for a
genuine competing-strategy layer.

## Passing authoring syntax

```firmament
Rect2 PlateBase { Center: [0mm, 0mm]; Size: [100mm, 60mm] }
EdgeProfile PlateBase.Bottom {
    Tab MountTab { CenteredAt: 50mm; Width: 12mm; Extension: 6mm; Side: Right }
}
CornerProfile PlateBase.BottomRight {
    Chamfer CableClearance { SetbackA: 8mm; SetbackB: 5mm; }
}
CornerProfile PlateBase.TopLeft {
    NotchCorner LocatingStep { SetbackA: 7mm; SetbackB: 4mm; }
}
Profile Plate From PlateBase
Extrude Solid { Profile: Plate; From: 0mm; To: 3mm }
```

At each corner, `u` points away from the vertex along EdgeA in reverse owner
direction and `v` points away along EdgeB in owner direction. No orthogonality
is assumed. `Chamfer`, `Cutback`, and `Taper` insert one exact line;
`NotchCorner` inserts the two-line material-removal step through `u+v`.

The resolver removes the requested suffix/prefix intervals before M2 carrier
generation. A fragment entering either interval rejects before contour/BRep
construction with
`semantic-corner-edge-fragment-conflict:<corner>:<fragment>:owner=<edge>`.
Generated zero-length carriers are omitted. Stable paths such as
`PlateBase.BottomRight.CableClearance` identify semantics independently of
curve ordinals.

Sheet Metal consumes the same resolver for region-profile `OuterStart` and
`OuterEnd` corners. Its manufacturing bend-corner relief policy remains a
separate concern. Corner and operation paths are exposed in both formed and
`Flat.*` inspection.

## CTC-03 dogfood and honest status

The source boundary audit identified and authored ten deterministic operations:
four front/rear wall end steps, two left-wall 25.4 mm end tapers, and four
front/rear mounting-flange 12.7 mm endpoint chamfers. The compiler now owns all
adjacent-edge trimming and carrier repair; the Firmament source states only the
engineering corner and setbacks. No source STEP is read during construction.

| metric | Profile M2 | Profile M3 |
|---|---:|---:|
| formed source→generated RMS | 10.614040 mm | 8.500190 mm |
| formed source→generated p95 | 19.071261 mm | 12.478269 mm |
| formed source→generated max | 52.816066 mm | 52.816066 mm |
| formed generated→source RMS | 6.750457 mm | 3.607855 mm |
| formed generated→source p95 | 8.310053 mm | 8.940881 mm |
| formed generated→source max | 19.071261 mm | 12.735724 mm |
| flat width / height residual | 0.002447 / 0.007820 mm | 0.002447 / 0.007820 mm |
| flat contour RMS | 12.038486 mm | 2.191367 mm |
| flat contour p95 / max | 19.047460 / 19.047460 mm | 4.775200 / 4.775200 mm |

All seven bends and all 17 openings remain passes. CTC-03 remains
`NeedsReview`, not Complete. The unchanged formed maximum and remaining local
residual are dominated by the right-wall service-attachment outline: it has a
two-level, persistent stepped/tapered edge program between its endpoint
chamfers and the partial 45-degree flange. That is no longer a cross-edge
ownership problem and should not be forced into a corner primitive.

A bounded nearest-vertex local audit after M3 (including the expected nominal
thickness/reference-skin displacement) reports: FrontWall 0.95/0.95/0.95 mm,
RearWall 0.96/0.96/0.96 mm, LeftWall 1.78/2.13/2.13 mm,
FrontMountingFlange 0.95/0.95/0.95 mm, and RearMountingFlange
0.95/0.95/0.95 mm RMS/p95/max. RightWall remains 64.78/115.48/115.48 mm in
that deliberately coarse vertex-only audit because its persistent interior
edge program is still missing. The later recognized-import recovery M1 source
unfold confirms the geometry behind this statement but corrects its design
interpretation: the wall is an ordinary outer carrier with two 63.5 mm local
cutback runs, two 15.24 mm diagonal transitions, two R12.7 outer corners, and
the existing partial service-flange attachment. It does not justify a general
persistent edge-program language.

One measured run reported parse 27.6 ms, semantic resolve 17.3 ms, formed lower
75.1 ms, exact authored-flat lower 62.6 ms, and flatten 8.8 ms (about 191 ms
total). A 10,000-call caller-side micro-measurement averaged about 18 µs per
single deterministic corner resolution; Judgment cost is not applicable.
Two independent CTC runs produced flat hash
`615aa6c549c55654a0bdafc9da0e07f4e3f983970f2f7acda6f5e4c305a41364`.

The executable non-Sheet-Metal fixture is
`fixtures/FirmamentV2/Canonical/valid/profile-shared-corners-plate.firmament`.
