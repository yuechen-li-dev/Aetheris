# Geometry reasoning M3: bounded distance queries

`ClosestPointQuery.Between` is the public, domain-neutral point/curve/patch distance API. Every overload uses a `DistanceQueryPolicy`; convenience overloads record `DistanceQueryPolicy.Default` in the result. The default is 1e-6 model units (millimetres in current CAD consumers), 1e-12 relative, a 1e-10 parameter threshold, 96 local iterations, and 10,000 deterministic subdivision candidates. The absolute and relative defaults match the audited kernel `ToleranceContext`; minimization controls belong to this query rather than the general kernel comparison context.

The raw `ComputedDistance` is never rounded to zero. `WithinTolerance` says only that the raw distance is no larger than `max(LinearTolerance, RelativeTolerance * scale)`. `Coincident` requires structural authored identity (or a future certified zero argument). Aetheris never interprets a floating-point distance as exact coincidence without an explicit tolerance and evidence contract.

Point/line-segment and segment/segment use bounded analytic projections and carry `Certified` evidence. Equal stable authored geometry identities take a zero-evaluation `Structural` path. Other conics, B-splines, patches, and mixed pairs use deterministic whole-domain lattice search at two resolutions followed by bounded coordinate refinement. Stability supplies lower/upper bounds and `ToleranceBounded` evidence. Failure to stabilize, non-finite evaluation, or budget exhaustion yields `Unknown`, retaining diagnostics and the best interval where available. No random start is used.

The query is observational. Zero or near-zero distance identifies no crossing, tangency, contact order, trim, B-rep edge, collision response, or geometry motion. `ClearanceExpectation` only validates returned evidence.

Current limitation: generic nonlinear lower bounds are numerical stability enclosures, not formal interval enclosures. They therefore never receive `Certified` evidence. Rational/NURBS geometry remains unsupported by the authored geometry layer.

## Audit

Kernel.Core contains analytic projections and distance helpers in B-rep picking, planar domains, STEP conic parameter recovery, and primitive spatial queries. Continuum has optimized boundary projections and sampled boundary-offset maps. Surfacing Panel edges already expose bounded authored curves. These are domain-specific/internal and mix sampled, analytic, and local tolerance rules; they were intentionally left in place. M3 reuses the public authored curve/patch adapters and kernel tolerance scale, and dogfoods Panel edges, without turning the milestone into a repository-wide numerical refactor.
