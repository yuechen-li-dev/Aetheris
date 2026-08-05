# Blind Drill clearance contract X1

Composed-host transverse `Hole<Shaft>` features with a `DrillPoint` use the
explicit `FullRadiusThroughTotalDepth` validation policy. The compiler proves
one full-radius cylindrical guard envelope from the declared Mouth through
`TotalDepth`, using the existing exact world-Z slab chord decomposition and
line/arc material-region rectangle containment query.

The guard envelope is not materialized. The authoritative cavity remains the
exact shaft cylinder, shaft-to-DrillPoint transition, conical DrillPoint wall,
and one Tip. Its analytic removed volume remains:

`pi * r^2 * ShaftDepth + (pi * r^2 * TipLength) / 3`.

Blind DrillPoint Holes require full shaft-radius clearance through TotalDepth.
The validation cylinder is a conservative guard envelope. The materialized
feature remains a cylinder plus a conical DrillPoint.

A cone-only fit is intentionally rejected by the semantic Hole generic. The
diagnostic identifies `FullRadiusTipClearanceFailed` or
`InnerVoidIntersection` and retains the slab, chord, construction-plane, and
feature provenance. If the taper itself is the intended design, author an
explicit tapered construction feature; if an exit is intended, declare a
ThroughAll Hole.

Section-stack slab changes are planning evidence, not cavity terminations. The
current narrow topology route continues to emit one unpartitioned cone face,
one Tip, no Exit, and no internal cap. Hyperbola support remains a kernel
capability, but is not part of blind-hole admission under this policy.

## Compose source bridge and inspection

The admitted source bridge is deliberately one continuation of the existing
`Profile`/`Compose` route, not a second composition language. A `Hole<Shaft>`
inside `Compose` that declares `From: <ConstructionPlane>`, `Center:
Point2(...)`, `ShaftDepth(...)` or `TotalDepth(...)`, and `Termination:
DrillPoint` is retained as a `PrismaticConstructionPlaneBlindDrillFeature`.
After the host stack has been normalized and planned it is lowered to
`AirConstructionPlaneHolePlacement`, proved by
`TransverseBlindDrillToolCorridor`, then inserted by
`SectionStackBlindDrillCavityPlanner`.

`aetheris inspect-selections <source> --json` publishes the policy, total
clearance-cylinder length, slab chord proofs, classification, semantic
descendants, and the no-cap plan provenance under `holeContract`. The emitted
cavity still contains no validation-cylinder geometry.

The current Mouth operation is intentionally narrower than the clearance
proof: the complete Mouth circle must fit in exactly one planar host-side
face partition. A Mouth which crosses an internal section-stack planning seam
is rejected as `SectionStackBlindDrillMouthCrossesHostPlanningPartition`.
This prevents an untrue single-face ownership claim while the explicit
multi-face circular-Mouth replacement operation remains future work.
