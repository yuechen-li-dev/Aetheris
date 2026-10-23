# Claude combined-feature authoring follow-up

Date: 2026-10-16. Starting commit: `dfff36de`.

Verdict: **Meaningful progression.** The countersink/finish capability and
reported compiler diagnostic gaps are repaired. General multi-body Model
export remains unsupported; its previously silent body loss is now rejected.

## Findings and dispositions

| Report | Current reproduction | Disposition |
| --- | --- | --- |
| A second body breaks a hole-plus-finish Model | Confirmed. The combined route required a single Box in the whole document. More seriously, removing Modify accepted two independent Boxes and exported only the last one. | Independent output bodies now reject at the second declaration in canonical and retained lowercase V2. The diagnostic explains separate Parts in an Assembly and admitted Compose operations. General multi-body Model export remains unsupported. |
| Countersink cannot share Modify with EdgeFinish | Confirmed. The Hole stage materialized correctly, then the combined route excluded its stack kind. | Top/+Z ThroughAll countersinks now compose with a top-boundary chamfer or fillet through the existing final-plan owner. |
| Errors still point to 1:1 | Confirmed for the combined-stack capability rejection: the producer omitted its available Hole span. | The combined rejection now retains the Hole span; host, context and excess-finish rejection also retain the relevant available construct span. The body-cardinality diagnostic is source-located and classified as capability admission. |
| Hole completion is type-blind | Not reproduced in the current compiler/public SDK/WASM. Shaft, Counterbore and Countersink completion and bound-field inspection pass their variant checks. | Retain the previous shared-schema fix and additive `variants` metadata. A schema-driven Helios control must honor that metadata or use compiler completion. This pass does not claim qualification of the deployed Helios UI. |
| Every palette hole begins at the origin | Confirmed by the catalog; this is explicit source, not a compiler placement bug. | Preserve the default. Do not invent a center or silently separate overlapping holes. Face picking and editor placement remain a separate UI decision. |

## Geometry ownership

`CombinedTopBoundaryEdgeFinishThroughHoleBuilder` remains the analytic
materializer. A countersink adds a cone joined to a cylindrical shaft at one
shared throat circle. Its longitudinal seam is a generator with the correct
slant length. No annular counterbore shoulder is substituted for the cone.
The existing AIR Hole materializer still supplies the initial semantic and
host proof, and the same final plan admits the boundary finish.

Admission checks the full entry radius against the inset top face and other
hole entry circles. The derived cone depth must leave a positive shaft span.
Blind holes, side entry, relief reaching the opposite face, overlapping mouths
and contact with the finish band remain explicit rejections.

Source identities, entry/exit loops and cone/shaft wall correspondence survive
mixed shaft, counterbore and countersink stacks. Hole reports distinguish
cylindrical and conical faces. Explicit AP242 HoleDiameter PMI still refers to
the authored shaft diameter, not the countersink mouth diameter.

Volume qualification also found that the existing chamfer report used the
homothetic-frustum formula for unequal rectangular sides. Its report now
integrates `(width - 2t)(depth - 2t)` exactly. This corrects analytic report
values; it does not change the already emitted chamfer BRep or STEP geometry.

## Body admission and compatibility

The source check lives in the existing semantic binder, before a construction
adapter can discard a material declaration. Both modern and lowercase V2 use
the same cardinality diagnostic. A legacy `with` prototype referenced by a
derived record is compile-time input, not a second independent output request;
the existing derivation fixture remains admitted with its two bound records.

The combined feature route selects its actual Modify target instead of assuming
the first bound record is its host. It retains the one-Modify/one-finish family
boundary. No multi-body exporter, source rewriting pass or parser route is added.

The palette's Box and Cylinder entries now carry `requiresEmptyModel: true`.
Their old occupied-host tests had passed through the last-body omission. They
are now tested as empty-Model starters, with explicit rejection checks in
occupied Models. Feature palette entries remain qualified against an existing
Box. The old countersink-refusal test is replaced by positive chamfer and
fillet construction tests. No frozen geometry oracle was changed.

## Qualification

Focused tests passed: **181**. Coverage includes:

- 60°, 90° and 120° countersinks with chamfer and fillet;
- exact cone dimensions and surface families after STEP reimport;
- manifold enclosure and an independent refined boundary-volume estimate;
- mixed-hole identity/correspondence and explicit AP242 PMI;
- unsupported intersections, depth and entry direction with source diagnostics;
- independent second-body rejection and legitimate legacy record derivation;
- current variant-aware completion and semantic field projection.

The real browser/WASM harness passes both countersink finishes, current Hole
variant help, source-located combined rejection, second-body rejection with and
without Modify, and palette body requirements. Its existing seven-time Motion
proof still matches all 1,232 native matrix components exactly and retains the
last-valid graph and shared geometry.

Final qualification:

- Release solution build passed, with existing analyzer/interop warnings.
- Full serial solution suite: **5,229 passed across 20 assemblies, zero failures**.
- Core fast lane: **1,076 passed**. Focused lane: **181 passed**. SDK Node tests:
  **14 passed**.
- Claude authoring corpus: **32/32 passed**, without recording new references.
- Frozen STEP replay: **26/26 byte-identical artifacts**. All nine scoped
  authoring outcomes held.
- Real public-SDK/WASM proof passed both countersink finishes, three Hole
  variants, source-located failures and both second-body rejections. Motion
  parity and last-valid retention remained green.
- Six real CLI validate/build pairs produced their expected outcomes. Accepted
  fillet/countersink STEP has 12 faces: six planes, five cylinders and one cone.
  The entry-at-23.5mm witness, whose mouth stays inside the stock but intersects
  the finish band, rejects with a located interaction diagnostic. Rejected
  inputs emitted no STEP artifact.
- The preserved before-artifact for the two-body plain Model contains just
  the 8 × 6 × 4mm Extra Box, with bounds `[-4,-3,0]..[4,3,4]`; its requested
  60 × 40 × 10mm Base was absent. The same source now rejects at Extra on line 4.
- Diff checks passed. No task-owned dotnet, testhost or aetheris hosts remained
  at closeout.

Local evidence is under ignored `artifacts/local/claude-combined-feedback/`,
including `full.log`, `focused.log`, `browser.log`, `x4.log`, `cli.json`, the
original CLI failures, before/after geometry inspection and the frozen replay.
Reusable source specimens live under `fixtures/Regression/CombinedFeatures/`.

The code-quality pass kept cardinality in one shared semantic helper, retained
record-prototype dependencies, and kept countersink geometry in the existing
bounded builder. No regex dispatch, global source rewrite, duplicated field
schema or diagnostic-fatality allowlist was introduced.

## Remaining boundary

General multi-body Model authoring has not been implemented. Merely lifting
the old Box-count guard would silently omit another body in the current export
contract. Supporting that feature requires an explicit product/geometry result
that carries every body through display, source identity and interchange.
Independent Parts in an Assembly already have that ownership model.

Helios must consume the refreshed runtime and use the applicability/context
metadata. Its deployed asset selection, viewport failure and future face-pick
placement are not certified by the compiler/WASM qualification here.
