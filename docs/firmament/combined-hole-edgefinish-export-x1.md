# COMBINED-HOLE-EDGEFINISH-EXPORT-X1

X1 admits one `Box` host, one `Modify` context, one or more top/+Z simple-shaft `ThroughAll` semantic Holes, and exactly one `EdgeFinish` selecting the host's `+Z` outer `Boundary` with an equal-distance `Chamfer`. Regions, other finish kinds, non-through or non-simple-shaft holes, multiple hosts, and mixed third feature families are outside this slice.

Export routes now have three dispositions: `Declined`, `Failed`, and `Succeeded`. A route declines when the complete document shape is not in its admission predicate; it fails only after it has claimed an admitted shape and finds invalid content. Export routes claim complete document shapes, not individual tokens. A narrow exporter must decline a mixed document it cannot materialize.

| Route | Admission | Declines |
| --- | --- | --- |
| `CombinedHoleEdgeFinish` | one Box + one Modify + semantic Holes + one admitted outer top chamfer | hole-only and finish-only documents |
| `AirChamfer` | its existing finish-only family | any document with a semantic Hole |
| `SemanticHole` | its existing Hole-only family | any document with an `EdgeFinish` |
| `ControlledSideHole` | its existing Region side-hole family | any document with an `EdgeFinish` |

The route order is no longer the combination policy: the dispatcher converts every legacy nullable `TryExportV2...` result into the explicit disposition contract, and stops only at a claimed failure or success.

## Construction lineage

The feature order is fixed:

`HostBRepPlan -> HoleChangedBRepPlan -> EdgeFinishChangedBRepPlan -> final BRep`.

The existing semantic Hole materializer first proves each Hole against the admitted host and publishes mouth, exit, and shaft-wall descendants. The exact top-boundary chamfer planner is then admitted against the post-Hole semantic context. X1 classifies every Hole/finish pair as `Disjoint`; a Hole approaching the inset outer boundary fails with `CombinedFeatureInteractionUnsupported`. The final bounded plan consumes those admitted intents into one shell and one body; it never STEP-round-trips between features or emits overlapping bodies.

The final correspondence retains each Hole's mouth loop, exit loop, and wall face, plus the finished boundary replacement face. Raw topology IDs are not source selections.

## Fixture and evidence

[`combined-hole-edgefinish-x1.firmament`](../../fixtures/FirmamentV2/Composite/combined-hole-edgefinish-x1.firmament) is the real Concept/Struct proof: an 80 x 50 x 25 Box, two 8.5 mm ThroughAll holes, and a 1.5 mm top outer chamfer. `aetheris build --json` reports `combined.route = CombinedHoleEdgeFinish`, ordered stages, `Disjoint`, plan ID, descendant counts, analytic volume, and STEP SHA-256.

For this fixture the Hole removal is `2 * pi * 4.25^2 * 25 = 2837.250865273282 mm^3`. The final plan's analytic volume is `96874.61608145387 mm^3`, less than both the `100000 mm^3` host and the `97162.74913472672 mm^3` post-Hole body, so the chamfer cannot erase or duplicate the Hole volume. STEP SHA-256 is `38fffde63140f2f845d2fd18e7d8eb2498217f235e7e879d717f4a9205139d02`; production-canonical STEP SHA-256 is `13e27980cc9c1769fbc39b2baf7cc6e126ed3ab0a7058773c005d3cf44c0c988`. STEP reimport reports one enclosed, orientation-consistent manifold body with 16 vertices, 26 edges, 12 faces, 10 planar surfaces, and 2 cylinders. Independent trimmed-face verification returns `96907.04859870145 mm^3` with its conservative `6149.914566321924` numerical bound.

Remaining limits are intentional: chamfering Hole mouths, Hole/finish shared vertices or edges, Hole-split finish chains, arbitrary feature order, other hole stacks, other finish kinds, and grammar unification are deferred. The next major milestone remains Firmament V2 authoring grammar unification, not basic backend feature composition.
