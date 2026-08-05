# Composed-host Hole contracts (X5 foundation)

Hole end conditions are authored mechanical contracts. The compiler proves them; it does not reinterpret them.

Host traversal describes material reality. It never chooses whether a Hole is Blind or ThroughAll.

The production Construction Plane Box lane records immutable `HoleHostTraversalEvidence`: world mouth and axis, radius, ordered local material intervals, source provenance, footprint support, and an explicit traversal classification. The Box proof is analytic: a signed-permutation frame transforms the exact Box bounds, then verifies the complete circular mouth footprint against the local rectangle. It does not sample meshes, rays, or bounding boxes.

`ThroughAll` requires one contiguous material span entered at local zero and produces a Mouth, Exit, and shaft-wall descendants. `ShaftDepth` or `TotalDepth` with `Termination: DrillPoint` derives the cone tip length from the included angle, requires the tip strictly before the far material boundary, and records `RemainingWall = hostEnd - totalDepth`. A breach is `BlindHoleBreakthrough`; it is not converted to ThroughAll and the DrillPoint is not trimmed.

`aetheris inspect-selections <source> --json` includes a `holeContract` packet with declared condition and termination, depths, traversal classification and intervals, physical material span, remaining wall, end-condition facts, and contract diagnostics. The local-frame BRep plan owns this evidence beside its topology correspondence.

## Current admitted scope

This foundation is currently wired to the signed-permutation Construction Plane **Box** host route. The profile/Compose section-stack emitter already owns exact line/arc slab geometry, but it is not yet connected to the local-frame Hole materializer. A transverse Hole through a changing section stack needs a plan-owned multi-slab subtraction/topology route; treating its slab bounds as one Box would be an invalid proof. Consequently composed profile hosts are still rejected rather than guessed, and there are not yet composed-host Hole fixtures, STEP proofs, Cadmata interval rendering, or runtime `Require RemainingWall` guards.

The next implementation step is to derive `HoleHostTraversalEvidence` from `PrismaticSectionStackConstruction` with exact circle-vs-line/arc footprint containment per slab, classify partitions versus actual gaps, then build the corresponding multi-partition Hole BRep plan before enabling that source lane.
