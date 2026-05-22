# CIR-BREP-T4: concrete emitted topology references in identity maps

## Purpose
CIR-BREP-T4 extends internal emitted topology identity metadata with concrete BRep topology references (face/loop/edge/coedge/vertex ids) so bounded stitch execution can locate emitted entities deterministically.

## Reference model
`EmittedTopologyIdentityEntry` now carries optional `TopologyReference` (`EmittedTopologyReference`) with:
- patch key
- local topology key
- face/loop/edge/coedge/vertex ids (nullable where not applicable)
- diagnostics

## Behavior
- Planar rectangle-with-inner-circle emission attaches topology references on inner circular trim identity entries.
- Planar patch-set entry identity maps preserve those references unchanged.
- Cylindrical retained wall emission attaches topology references for seam/top/bottom boundary entries.

## Stitch executor awareness
`SurfaceFamilyStitchExecutor` now distinguishes:
- missing concrete topology references (still deferred), versus
- topology references ready (diagnosed), but mutation/remap not implemented in T4.

No topology mutation/merge is performed in this milestone.

## SEM-A0 boundary
These references are internal emitted-topology diagnostics only.
They are not public topology names, selectors, or PMI references.

## Next step (T5)
Implement bounded cross-body topology remap + mutation path that consumes ready topology references and performs deterministic stitch operations behind explicit admissibility gates.
