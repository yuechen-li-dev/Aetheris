# Composed Profile chamfer integration M2

The composed-host route and the bare Profile route diverge at the authoritative plan boundary. `PrismaticSectionStackTopologyPlan` owns stock, through-shaft loops, counterbore mouth/shoulder/shaft intervals, semantic correspondence, and normalized PMI. M1's `ProfileBoundaryChamferPlanner` owns an outer-loop transition but intentionally has no inner-loop/cavity section representation.

M2 adds required pre-materialization admission. A semantic `On: Top` whole-loop target is bound to the Compose stock Profile, and a typed corridor spans `[Top - Distance, Top]`. Shaft circles are compared against the outer boundary by the conservative analytic condition `distance(center, outer boundary) > shaftRadius + chamferDistance`. A counterbore uses its larger entry radius over the entry interval and its shaft radius below the shoulder. Touching is rejected.

This leaves a deliberate boundary: a disjoint composed target reports `ProfileBoundaryChamferComposeTopologyPlanNotMaterialized` rather than modifying the emitted shell. The next implementation must extend the section-stack generator with a `Top - Distance` station, carry active inner loops across that interval, and generate the inset outer cap inside the same `PrismaticSectionStackTopologyPlan`; it must not patch a materialized BRep.

Admission records each cavity feature id, center, radius, interval, and classification. Shaft collisions report `ProfileBoundaryChamferIntersectsShaft`; counterbore entry collisions report `ProfileBoundaryChamferIntersectsCounterbore`.
