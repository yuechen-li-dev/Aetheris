# GEOM-OFFSET-X1 — bounded local material add/remove

## Executive verdict

**Meaningful progression.** Aetheris now expresses the Prism subset of common bounded local material addition/removal without exposing public arbitrary Boolean operands. The exact curved-tool matrix is not accepted: Cylinder cross-holes/curved-target cuts and spherical addition remain blocked on missing bounded analytic topology builders.

## Operation matrix

| Tool | Add | Remove | Host/support | Termination | Qualified |
|---|---|---|---|---|---|
| Prism | connected profile pad | groove, channel, finite/through edge notch | active Compose body; whole Top or bounded `Span<Plane>`; `+Z` frame | Add `Height`; Remove `Depth` or `ThroughAll` | Yes |
| Cylinder | — | — | exact curved-target intersections unavailable | — | No; typed rejection |
| Sphere | — | — | exact cap union unavailable | — | No; typed rejection |

## Semantic and safety result

`PrismaticMaterialOffsetFeature` is the typed AIR boundary. It retains Add/Remove, Prism, explicit target, support, source Profile, direction, termination, extent, stable ID, and six-coordinate authorized region. Materialization reuses the exact Profile/Compose section-stack path; the author never supplies an arbitrary body operand.

The qualified tests demonstrate positive Add/Remove, an edge-crossing through notch, exact line/circular-arc profile bounds, exact planar carriers, single-body STEP reimport, analytic volume, deterministic AIR/STEP, stable result-wall descendants, and fail-closed rejection for zero extent, missing target, unsupported tool family/profile curve, no intersection/disconnected Add, splitting Remove, and complete removal. Failure occurs before output artifact construction.

Outside the authorized profile extrusion bounds, section-stack source geometry is preserved. The source Profile provides stable derived wall/cap identity through existing profile segment provenance; the offset itself has stable identity `offset:<target>.<name>`.

## Relation to named features

- `Boss` remains preferred for boss intent and preserves boss-specific identity.
- `Pocket` remains preferred for an enclosed finite pocket and enforces minimum floor policy.
- `Hole` and `Slot` remain preferred for their typed engineering variants and topology descendants.
- `ProfileDelta` changes a 2D boundary before solid construction; Offset changes an existing 3D construction state.
- `OffsetRegion` displaces an existing Sculpt surface region and is not a material-offset synonym.

## Deferred boundaries

The existing Boolean facade proves selected box/cylinder/sphere cases but is not a general exact kernel. Its cylinder-root path is world-Z/coaxial and cannot manufacture a perpendicular cylinder-cylinder intersection. It has a bounded box/sphere pit builder but no connected exact spherical-cap union builder. Promoting either family now would discard semantic tool intent early or require a faceted/general-Boolean fallback, both forbidden by this milestone.

Curved-support Hole is therefore deferred to `GEOM-HOLE-HOST-X1`. The BenchCAD `topup_ball_knob_axial_hole` witness was not rerun because the required sphere-target Cylinder removal is not qualified; the historical 0.9409 score is not acceptance evidence.

## Qualification evidence

- Release solution build: zero warnings and zero errors.
- Full solution tests: 3,469 passed, including the focused Offset suite at 14/14. (`Aetheris.FrictionLab.Tests` currently discovers no tests.)
- Repository and freshly packaged CLI produced byte-identical groove STEP (`4C30A48E85BE1DA8A3F006F36181880A7EA0CC32BD497E628BBF697B3AE33B58`).
- Groove STEP reimport: one enclosed-manifold body, 17 planar faces, and bounds `[-20,-15,0]` to `[20,15,10]`. The independent Add-plus-Remove volume witness is exactly `12000 + 240 - 168 = 12072 mm³`.
- Eight Prism invalid fixtures produced their typed diagnostics and no STEP artifact, including zero height/depth, invalid axis, non-intersection, tangency, disconnected Add, splitting Remove, and complete removal.
- Canonical Offset fixtures all build. The repository-wide qualification script still exits nonzero after its fixture actions because the pre-existing `Assembly/annular-pair.firmament` uses compatibility-only `LegacyExplicit` placement, which the canonical-content guard rejects. GEOM-OFFSET-X1 does not rewrite that unrelated assembly fixture.
- Public-document dialect/link checks passed; the repository layout guard inspected 4,115 tracked files successfully; `git diff --check` passed.

The Prism implementation intentionally uses the existing Compose section-stack construction authority rather than routing through Sculpt `ConstructionState`. This retains transactional plan-first materialization, ordered semantic operations, exact source provenance, and deterministic replay for this bounded lane without introducing a second construction executor. Cylinder/Sphere need new exact topology builders before they can share an equivalent contract.
