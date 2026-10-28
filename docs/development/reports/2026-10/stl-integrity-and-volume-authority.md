# STL integrity and volume authority

Date: 2026-10-28

## Reproduction and repair

Claude's CTC-01 report reproduced on the production CLI. The original STL had
22,108 triangles and 1,444 unmatched directed edges despite `watertight: true`.
The validator counted undirected edge uses and accepted positive total signed
volume; neither proves consistent winding.

Independent integration of the actual float32 STL records:

| Reference origin (mm) | Before (mm³) | After (mm³) |
| --- | ---: | ---: |
| (0, 0, 0) | 15,439,826.734 | 14,646,106.929 |
| (1000, 2000, 3000) | 17,317,273.436 | 14,646,106.929 |
| (-1000, -2000, -3000) | 13,562,380.032 | 14,646,106.929 |

The repaired artifact has zero unmatched directed edges. The multiset of
unoriented triangles is identical before/after, including stored coordinates;
only winding/normals change. These are triangle
approximation measurements, not a claim of exact nominal NIST volume.

Repairs stay at the existing owners:

- SurfaceMeshIR publishes cell winding from the resolved BRep support and
  analytic chart orientation once, before display, OBJ and STL consume it.
  Approximate 3D chord normals are insufficient for thin curved trim ears;
  chart winding preserves their shared-edge traversal. No mesh welding or
  guessed global sign flip repairs the construction document.
- TriangleMeshValidator requires opposite directed uses, reports orientation
  conflicts, and qualifies signed volume only after closed/consistent incidence,
  connectedness and nondegeneracy checks. Positive algebraic volume alone no
  longer labels a mesh outward/watertight. Nearby-anchor compensated integration
  reduces cancellation for parts far from world zero.
- The exact coaxial materializer now marks concave root fillets opposite their
  torus support. Imported versions already derived the correct sign, exposing a
  direct-construction versus import disagreement under the stronger validator.
- The legacy Box/cylinder subtraction owner now declares cavity wall normals
  inward toward the removed cylinder. This construction metadata error was
  exposed by strict checking; coordinates and supports are unchanged.
- BinaryStlExporter validates the actual float32 representation with equal stored
  positions identified. Collapse or nonfinite coordinates reject before opening
  the destination. The CLI returns structured failure. Reported mesh volume is
  computed from the stored coordinates; existing report fields remain compatible.

Checks do not prove absence of self-intersection or complete design fidelity.
SDF cannot substitute for checking the artifact's triangles.

## SDF evaluation and decision

Use exact admitted measures first; use bounded SDF integration as the default
**numerical engineering measurement** only when a qualified field represents the
final exported body. Keep mesh mass explicitly diagnostic. FirmamentMeasurements
already implements that policy for `measure`/assertions, including rejection of
stock-only or display-only CIR mirrors.

Real CLI probes show the present boundary:

- The authored Box witness selects ExactVerticalLineArcPrismSectionIntegration:
  exactly 480 mm³, zero uncertainty and no field-region work.

- Authored radius-10 mm sphere: both default and explicit `--sdf --error 50`
  select SdfAdaptiveOccupiedVolumeInterval. They visit 1,000,001 regions and stop
  with [4109.301567, 4267.172813] mm³, midpoint 4188.237190 and ±78.935623 mm³.
  `budgetExhausted: true` correctly discloses that ±50 was not achieved. The
  exact nominal sphere value 4188.790205 lies inside the interval. This existing
  exact-volume recognizer set does not admit the loopless sphere; the field is
  the available qualified measurement here.
- Imported CTC-01: geometry builds, but explicit SDF volume is Unavailable.
  No retained analytic CIR field is qualified to represent that imported body.
  A display mesh or approximate point-cloud field is not an independent remedy.

Do not globally redirect kernel mass properties to SDF now. Core has no general
trimmed-BRep-to-qualified-field lowering, and Continuum already depends on Core;
adding the inverse dependency would be the wrong ownership boundary. Existing
Core numerical boundary integration is distinct from assertion authority.

The smallest useful next steps are to extend **final material-operation CIR**
coverage in the existing lowerers, preserving transforms, holes, edits and
provenance, and improve bounded integration's uncertainty reduction before
making tighter default precision promises. Imported bodies need an independently
qualified inside/outside/interval capability for their trimmed solid. Reuse the
existing high-level measurement owner; do not add another volume validator or
construct a supposedly independent SDF from the same suspect triangles.

## Qualification

The focused Core mesh lane passed 22 tests; the focused production CLI lane
passed nine, and the Firmament mesh/assertion/rib lane passed 39. The first full
run isolated the direct-construction root-fillet sign defect above; its log is
retained as `full-tests-superseded.log`.

Final qualification:

- Release solution build: zero errors.
- Fast Core lane: 1,168 passed.
- Full solution gate: 6,304 passed, zero failed, two optional external PDF-renderer
  skips across twenty test assemblies. Full Core: 1,356 passed.
- Managed/WASM SDK rebuilt; all nineteen JavaScript SDK tests passed.
- Production CTC-01 STL byte audit: zero unmatched directed edges, unchanged
  unoriented triangle geometry, volume stable across three origins.
- `git diff --check` passed; task-owned .NET/test hosts exited.

**Verdict: Success.** The motivating artifact and false watertight certificate
are repaired, the regression gate passes, and the SDF decision is grounded in
actual coverage/uncertainty probes. Broader imported-body field coverage and
faster tight interval integration remain explicit future work.

Generated evidence is under ignored
`artifacts/local/stl-integrity/`: before/after STL and JSON, independent byte
integration, SDF probe JSON, build and test logs. New tests check a reversed face
with positive total volume, entire inward winding, far-origin conditioning,
float32 precision collapse without overwriting a prior artifact, CTC-01 directed
incidence, and the actual production CLI's serialized STL at multiple origins.
