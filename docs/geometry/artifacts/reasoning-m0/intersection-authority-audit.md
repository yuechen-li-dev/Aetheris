# Bounded intersection-authority audit

Scope: `Aetheris.Kernel.Core`, `Aetheris.Kernel.Firmament`, and `Aetheris.Surfacing`; this is not a kernel-wide refactor.

| Path | Classification | Evidence |
|---|---|---|
| `TransverseConePlaneIntersection.IntersectWorldZ` | Bounded constructive / healthy | Accepts only signed-permutation transverse cones and world-Z sections, rejects the apex degeneracy, and returns the construction-known positive hyperbola branch. Its source documentation explicitly rejects a general surface/surface role. |
| Primitive Boolean `Intersect` | Bounded topology operation / healthy | Operates on already-authored BRep solids through the Boolean subsystem; it is not a numerical parametric-surface curve discovery route. |
| `RestrictedFieldGridSampler` -> marching squares -> contour stitching/snap | Legacy experiment but contained | `NumericalOnly` carries `NumericalOnlyNotExportable`; analytic line/circle snaps are candidate-only. `TieredTrimRepresentationBuilder` records `ExactStepExported=false` and `BRepTopologyEmitted=false`. |
| General planar/cylinder, planar/cone, and planar/torus source-surface capability table | Future risk explicitly deferred | Capability records distinguish exact, special-case, and deferred families. General conic/algebraic intersection is not admitted as topology authority. |

No code change was needed in these paths. The architecture rule is now explicit in `docs/geometry/reasoning.md` and the new public query does not call any BRep or trim materializer.
