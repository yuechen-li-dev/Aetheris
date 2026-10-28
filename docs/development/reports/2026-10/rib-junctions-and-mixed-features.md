# Manual rib junctions and mixed stock features

Date: 2026-10-27

## Change

Manual `Rib<Rectangular>` features on direct Box top supports now form integrated
T/L/X and closed-loop junction networks. Zero-draft line/arc/circle networks
support unequal heights; drafted networks admit Flat-ended straight ribs of a
common height when exact affine boundary correspondence survives all section
events. Separate groove footprints may share the stock. Top-entry ThroughAll
shaft/counterbore/countersink holes and one top perimeter Fillet/Chamfer reuse the
existing engineering feature owners. Their clearance is proved before attachment.

`reinforced-mounting-plate.firmament` is the motivating production fixture: an
80 × 60 × 6 mm plate, crossing 4 mm ribs, 12 mm height and 2° side draft, two
stepped through holes, and a 1.2 mm top perimeter fillet.

## Ownership and planning

JudgmentEngine chooses among three bounded representations, recording eligibility,
utility and rejection reasons in build JSON `ribPlanning`:

- ExistingAnalyticPatch: preferred for previously admitted isolated geometry.
- PrismaticSectionArrangement: the shared profile arrangement/section-stack owners
  construct zero-draft networks at their exact height transitions.
- AffineDraftArrangement: shared 2D arrangements establish bottom/top boundary
  correspondence; pair/triple support-plane coincidences enumerate potential
  topology events. Every open event interval is checked before exact planar lofting.

A construction-owned planar interface attaches the additive boundary to the
existing stock. Auxiliary section-stack base material is discarded. Exact cap
roles distinguish the outer attachment from support islands; loop traversal is
not substituted for the section owner's explicit hole semantics. All retained
stock entity IDs, curves, surfaces and feature correspondence survive attachment.
There is no general 3D Boolean, mesh union, parser route, or source rewrite.

Additional CLI probes exposed an old composite-hole limit when stepped holes
were used without a perimeter finish. The existing combined stock builder now
has an explicit `None` finish, with no degenerate rim faces or fabricated finish
receipt. Manual features reuse this stock plan with or without a real finish;
previous standalone hole routes remain unchanged. Isolated and joined variants
are covered, with both counterbore and countersink present.

Qualification exposed and repaired two local issues: adding an explicit inner
boundary requires completing the original support's boundary-role evidence; and
line/arc ray endpoint ownership must use the same half-open Y convention,
including endpoint roundoff. The first full run caught a maker-mark polygon
regression from applying endpoint tolerance too broadly. The final correction
leaves polygon-only classification unchanged and disambiguates arc-containing
rays within the existing classification tolerance. Both engraving regressions
pass in the focused rerun. Strict export/import checks were retained. Importer
inner-overlap failures now identify the implicated loop IDs.

## Evidence

- Actual CLI validate: valid, Built, UnifiedMechanical.
- Actual CLI build: AffineDraftArrangement, five materialized feature receipts.
- STEP reimport/wireframe: 28 faces, 67 edges; 20 planes, seven cylinders, one cone.
  All 134 pcurves qualify; maximum reported residual 0.000000435 mm.
- Focused tests cover X/T/L junctions, unequal heights, drafted junctions, a crossed
  circular rib with support islands, noninteracting semicircular removal, all three
  hole variants with both perimeter finish kinds, redundant/collapsed intent,
  validate/build refusal agreement, exact isolated-geometry parity, and exact STEP
  and stable-name equality under junction declaration reordering.
- All six previous manual-rib/groove CLI STEP artifacts are byte-for-byte
  identical after rebuilding: manual-rib, arc-rib, plate-channels,
  plate-seal-groove, shaft-seal-groove and bore-seal-groove. SHA-256 comparisons
  are in `artifacts/local/rib-junctions/geometry-parity.json`; original files in
  `artifacts/local/path-features/` were retained. No baseline was refreshed.
- Four extra real CLI probes pass for isolated-hole, isolated-fillet,
  network-chamfer and network-hole combinations (`mixed-smoke.json`).

Generated evidence lives under ignored `artifacts/local/rib-junctions/`:
`validate.json`, `build.json`, `reinforced-mounting-plate.step`,
`reinforced-mounting-plate.svg` and its browser-rendered PNG. The preview is drawn
from the exported/reimported BRep by the CLI wireframe renderer.

Focused rerun: 43 passed, including the historical maker-mark witnesses.
Complete qualification:

- Release solution build: zero errors (existing analyzer/platform warnings).
- Fast Core lane: 1,164 passed, zero failed.
- Full solution lane: 6,298 passed, zero failed, two optional external PDF-renderer
  skips, twenty test assemblies. Full Core: 1,351 passed; Firmament: 3,341 passed.
  The FrictionLab support assembly contains no discoverable tests.
- Managed/WASM development SDK rebuilt; all nineteen JavaScript SDK tests passed.
- `git diff --check` passed. Task-owned .NET/test hosts exited; no persistent
  services or startup hooks were introduced.

Logs: `artifacts/local/rib-full-build.log`, `rib-fast.log`,
`rib-full-tests.log`, `rib-network-focused.log`, `rib-sdk-build.log`,
`rib-sdk-tests.log`. The initial superseded run remains as
`rib-full-tests-initial.log` for the regression evidence.

## Deliberate limits

Hosts remain direct Box +Z supports. Imported/Compose/other-face supports,
compound guides, arbitrary sections, intersecting removal features, holes through
ribs, varying-height drafted junctions, and topology-changing taper reject.
Groove-only arrangements retain the isolated builder's conservative envelope rule.
Completely buried named ribs reject instead of receiving imaginary independent
face identities. Junction face numbering is deterministic under declaration
reordering; topology changes do not promise persistent individual face numbers.

No AutoRib policy, base blending, mold-release certification, or composite-body
SDF/authoritative volume assertion capability is added. Numerical mass checks in
tests are independent qualification witnesses rather than a new assertion authority.

## Verdict

**Success within the documented manual Box-top boundary.** Useful manually
reinforced parts now combine qualified rib junctions, through-hole stacks,
perimeter finishes and separate grooves without changing the six previously
qualified STEP artifacts. Exact construction, source correspondence and strict
interchange checks remain authoritative. The full gate passed; AutoRib can be
considered separately against this manual construction foundation.
