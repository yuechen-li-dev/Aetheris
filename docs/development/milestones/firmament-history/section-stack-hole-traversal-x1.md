# SECTION-STACK-HOLE-TRAVERSAL-X1 evidence foundation

`PrismaticSectionStackConstruction` remains the sole composed-host model. Its normalized `PrismaticSectionSlab` holds the arrangement-derived material `PrismaticSectionRegion`, active Compose operations, and source provenance. Hole traversal consumes that model; it does not rebuild profiles or infer material from Cadmata or tessellation.

The new evidence records a planner partition for every relevant normalized slab, exact line/arc disk-containment classification, transition events, and separately collapsed physical material spans. Section-stack partitions are planning boundaries, not automatically material boundaries. Adjacent supported partitions become one physical span only when their local-axis bounds meet exactly; an unsupported partition or interval gap is never merged.

For the presently admitted axial signed-permutation mapping, a construction-plane local `+Z` axis is world `+Z` or `-Z`; the footprint is therefore a disk in the stack's native XY section. The disk proof classifies the centre using `ProfileArrangement2D` and takes the exact minimum distance to every bounded line or circular-arc boundary of the outer loop and inner void loops. Tangency is explicit; it is not accepted as containment. This rejects a centreline that fits while its circular footprint crosses an outer boundary or void.

The strict Hole end-condition contract now also consumes typed mouth diagnostics: a mouth inside material, a mouth miss, or a direction that begins in air cannot be repaired by relocating the feature.

## Deliberate boundary

Transverse `+X/+Y` planes are diagnosed and rejected by this foundation. A transverse shaft has a YZ/XZ footprint across section Z transitions, not an XY disk. Reusing the Box interval or pretending that footprint were an XY disk would claim an invalid proof. The current source front-ends are also disjoint: composition input cannot carry the V2 construction-plane semantic-hole AST, while the V2 hole materializer admits Box hosts only.

Consequently this change supplies the authoritative host-evidence and contract substrate, but does not yet materialize partitioned cylinder/cone topology, STEP, inspection packets, or Cadmata artifacts for composed transverse holes. Those require the next bounded implementation to add the actual transverse footprint sweep and a shared composition/V2 lowering bridge while preserving `LocalFrameHoleBRepPlan` ownership.
