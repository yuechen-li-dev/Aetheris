# TEXT-REGION-NORMALIZE-X1

**Verdict: Meaningful progression.** The bundled Inter `D` and `X` now resolve
through Aetheris-owned non-zero-winding fill normalization into clean CAD
profiles. `CODEX` and `AETHERIS` both validate as exact vector profile sets.
Every `CODEX` region also passes the existing exact profile BRep extrusion
emitter. The ordinary Boss/Pocket section-stack path remains blocked on cubic
boundary arrangement and side-surface realization, so there is no engraved
threaded bolt or embossed plate to claim.

## Font asset and dependency

- Production font engine remains Copeland's `Machina.Typography.OpenFont` 1.0.0
  (`Typography.OpenFont` namespace). No parser or shaping replacement was added.
- Asset: `Aetheris.Kernel.Firmament/Resources/Inter-Regular.ttf`, SHA-256
  `FF80BD54C3E443EEFE82A9D282ACC213E441CD71FAE882F75EE1924E9E9119D5`.
  `otfinfo` identifies Inter Regular version `4.001;git-66647c0bb` with a
  `glyf` table and no CFF table. The file is licensed under SIL OFL 1.1, as
  recorded in `Resources/Inter-LICENSE.txt`.
- The TrueType source uses lines and quadratic Bézier segments. The X0 adapter
  degree elevates each quadratic exactly to a cubic, which the existing exact
  profile BRep emitter already supports. This retains the original polynomial
  curve and avoids introducing a second quadratic 3D realization path merely
  for the text milestone.

## Fill normalization

`GlyphRegionNormalizer` accepts the positioned lines and polynomial Béziers
from `PlanarTextProfiles`. The font's TrueType non-zero winding fill determines
material. The normalizer:

1. Builds fine chord pieces **only to find candidate intersections**. Their
   vertices never become output CAD edges.
2. Refines each candidate against the original polynomial curves, then splits
   the source curves at the resulting parameters with de Casteljau subdivision.
3. Evaluates winding on either side of every resulting fragment. Cubic ray
   crossings use monotonic intervals derived from the exact derivative; an
   internal edge between two filled cells is discarded.
4. Walks the remaining directed boundary graph, classifies positive-area
   outer loops and negative-area counters, and emits one `ResolvedProfile2D`
   per filled island. The profile validator remains authoritative.

No raster, polygon, or mesh path is used as production geometry authority.
The implementation is bounded to the bundled Inter TrueType line/quadratic
outlines, represented by exact lines/cubics after degree elevation. It is not
an arbitrary-font or arbitrary-curve planar Boolean API. Intersection search
and parameter refinement are numerical; tangencies and coincident cubic spans
outside the qualified glyphs have not been certified.

| Text | Glyphs | Raw contours | Resolved crossings | CAD regions | Counters |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CODEX` | 5 | 7 | 6 | 5 | 2 (`O`, `D`) |
| `AETHERIS` | 8 | 10 | 9 | 8 | 2 |

The D stroke, D counter, and exterior point classifications agree with the
independent oracle. Its normalized filled areas at 3 mm em height agree to
four decimal places for `D` and `X`. Increasing text height preserves region
topology and scales advance deterministically.

## Independent oracle

FontForge was not installed locally. For debugging only, an ignored local
environment under `artifacts/local/font-oracle/` used FontTools 4.65.0 to draw
Inter glyphs and Skia PathOps 0.9.2 `simplify(..., fix_winding=True)` to
remove overlaps and correct winding. The debug script is
`artifacts/local/font-oracle-check.py`. The oracle reports `D` as two raw and
two normalized contours, with filled area `743589.4166666667` font units²;
`X` is one raw and one normalized contour, with filled area
`607590.0809529623` font units². Aetheris matches those areas after the
2048-units-per-em scale. Neither package is a production dependency.

## Solid-feature boundary

The normalized `O` profile with a counter and all five `CODEX` regions pass
`ResolvedProfile2DValidator.Extrude`, which uses the existing exact profile
BRep planner. The separate `ProfileArrangementBuilder.Compose` path used by
Boss/Pocket currently has line/circular-arc intersection, splitting, point
classification, and reconstruction only. It now reports
`arrangement-rejected:bounded-cubic-section-unsupported` at that boundary
instead of throwing or silently omitting intersections. Beyond arrangement,
`PrismaticSectionStackEmitter.TryPlan` still constructs only line/arc edge
geometry and plane/cylinder side surfaces. A cubic extrusion needs the
existing exact polynomial swept-surface machinery adapted to this shared
section stack, with topology and STEP validation. Font-specific Boss/Pocket
logic would violate the semantic boundary.

The requested compact Firmament `Text` construct, schema, LX completion,
source-linked final faces, support-fit/depth checks, bolt and plate witnesses,
display renders, and STEP roundtrips are deferred until the shared solid
feature path can consume these ordinary profiles. The conceptual maker mark
source in `TEXT-PLANAR-X0.md` remains non-executable.

## Verification

- `dotnet test Aetheris.Kernel.Firmament.Tests -c Release --filter FullyQualifiedName~PlanarTextProfilesTests -m:1`:
  13 tests pass, including D/X normalization, both words, D fill points,
  independent oracle areas, and all CODEX region extrusions.
- `dotnet build Aetheris.slnx -c Release -m:1`: passed, 0 errors.
- Fast core lane: 1,000 passed. Full suite: all 1,677 Firmament tests passed;
  overall exit code 1 came from the same two CLI and five SheetMetal baseline
  failures recorded in `TEXT-PLANAR-X0.md`.
- `dotnet run --no-build --project Aetheris.CLI -c Release -- inspect
  fixtures/Thread/hexbolt-threaded.firmament --json`: parsed the existing
  threaded HexBolt fixture successfully. Its source and geometry were not
  altered by this milestone.
- No topology counts, manifold result, text STEP size, reimport result, or
  catalogue render can be reported for a bolt or plate that has not been built.

## Downstream status (BREP-BEZIER-SECTION-X0)

The later section-stack work now accepts valid cubic regions and constructs
exact B-spline-directrix translation walls. Generic cubic Boss/Pocket and an
`O` counter Boss are manifold and STEP-roundtrip. An `O` Pocket still exposes
the section stack's single-material-region-per-slab limit; CODEX engraving
and the plate witness remain unbuilt. See `BREP-BEZIER-SECTION-X0.md`. The
X1 normalization verdict above remains its historical result.

## Downstream status (BREP-MULTIREGION-SLAB-X0)

The later multi-region slab path now accepts O and D counter Pockets, two O counters, and a multi-island AETHERIS Boss with STEP roundtrips. This does not change X1's normalization verdict. CODEX still fails during section arrangement on a short X-glyph fragment, before the threaded-bolt engraving or Text source syntax; see `BREP-MULTIREGION-SLAB-X0.md`.
