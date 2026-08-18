# PREVIEW3-HARDEN-A2 — Firmament first-user and public-documentation hardening

> Development hardening report. This records reproduction and release evidence; it is not the user manual. The authoritative public Preview 3 documentation begins at [`docs/public`](../public/README.md).

## Outcome

A2 reached Success. The confirmed PMI loss is repaired at its combined materializer route and guarded at the build boundary by independent AP242 semantic reinspection. Sheet Metal's reported feature undercount was traced to Model-domain syntax being silently ignored, not to a generated hole omitted from the count; canonical syntax reports the feature, and wrong-domain authoring now fails with a corrective diagnostic. Public documentation, fixture-backed qualification, and a compact public/development boundary are in place.

No new geometry, PMI, Sheet Metal, FEA, Forge, or Cadmata feature family was introduced.

## Claude finding classification

| Finding | Reproduction and architectural result | Classification | Resolution |
|---|---|---|---|
| A — PMI + EdgeFinish | Confirmed. `CombinedHoleEdgeFinish` called `Step242Exporter.ExportBody(body)` without semantic PMI and returned empty inspection arrays. Validate admitted 3 supported records while build emitted 0. | ReleaseBlocker | The route now validates support, builds PMI from the same lowered holes, passes it to AP242, and reports record names. A global build/CompileSource parity gate uses `Step242SemanticPmiInspector`; mismatch fails with `firmament-v2-pmi-export-evidence-mismatch` before writing an artifact. |
| B — Counterbore PMI | Existing intended behavior works. `HoleDiameter` targets the counterbore's shaft `Diameter`; the distinct `CounterboreDiameter` has no separate Preview 3 PMI kind. The qualified profile-compose fixture validates, exports, reinspects, and deterministically reimports. The Claude literal used malformed/ambiguous syntax. | DocsFix | Documented the shaft meaning and retained the executable counterbore regression. No syntax was added. |
| C — Sheet Metal PMI target | Model `Datum` and `face(-Z)` are the wrong semantic domain. Sheet Metal manufacturing PMI uses `Manufacturing`, `DatumFeature`, and a named sheet region. The old error came from semantic-layout parsing and mentioned `<missing>`. | MustFix / DocsFix | Added `sheetmetal-pmi-domain-syntax` with the canonical form and documented cross-domain targets. |
| D — pattern holes + PMI | Pattern holes build through the composite hole materializer; PMI can coexist. Generated names such as `MountPattern_0` exist in reports, but no stable public per-instance selector/quantity-PMI authoring family is promised. | DocumentForPreview | Qualified the pattern fixture and documented the boundary; no instance-selection feature added. |
| E — Sheet Metal `features: 0` | Canonical `Hole Mount` lowers to one `SheetFeatureIr`, reports `features: 1`, and produces a third cylindrical surface beyond the two bend cylinders. `Hole<Shaft>` was ignored, so bend cylinders/circles were mistaken for hole evidence. | MustFix | Model-domain `Hole<Shaft>` now fails with `sheetmetal-hole-domain-syntax`; canonical count/topology are regression-tested. |
| F — FEA witness | Reproduced with a new A36 source: 100 × 30 × 15 mm, 500 N, `[16,2,2]`; result `25.0619 µm`, beam-theory sanity value about `24.7 µm`, converged with sub-nanonewton equilibrium residual. | DocsFix (positive witness) | Preserved as bounded public credibility evidence without general solver claims. |
| G — Forge interop | Protocol tests and the checked-in public request produce STEP AP242, flat STEP, and SVG. Protocol logic is language-neutral; release NativeAOT qualification remains `win-x64`. | DocsFix (positive witness) | Public guide reduces the model to list/describe/invoke, links all four tiny clients, and states the RID boundary. |

Claude's proposed build-result diff command remains DeferredPostPreview3 because it is a new CLI capability. The parity gate and structured evidence cover the release-safety requirement without broadening the CLI family.

## Controlled PMI matrix

The minimized fixtures were run through real `aetheris validate`, `build --json`, raw STEP inspection, semantic PMI inspection, STEP reimport, and deterministic reruns:

| Case | Validate supported PMI | Build evidence | Raw/reinspection result |
|---|---:|---:|---|
| hole + PMI (`box-hole-pmi`) | 2 | 1 datum + 1 diameter | 1 datum + 1 diameter |
| hole + EdgeFinish (`box-hole-chamfer`) | 0 | none expected | one reimportable manifold body, hole + chamfer |
| two holes + PMI + EdgeFinish (`box-holes-pmi-chamfer`) | 3 | 1 datum + 2 diameters | 1 datum + 2 diameters; two cylinders; deterministic STEP hash `B029BF44A31D90217FC7450AF69EA9C2FF7C7E155F2249A26DFF3BD85D253966` |

The parity comparison is semantic-record-level. It does not assume one PMI record equals one STEP entity.

## Truthfulness and diagnostics audit

- Successful V2 file builds and in-memory Forge/assembly compilation now reparse requested PMI, reject deferred export, reinspect emitted AP242, and require exact supported datum/diameter counts.
- PMI evidence now uses the authored PMI record name rather than falling back to the target hole name.
- Successful validation no longer exposes parser trace events (`parser-invoked`, `parse-succeeded`, `*-parsed`) as warnings; the clean controlled fixture reports an empty diagnostic array and zero warning count.
- Canonical Sheet Metal summary semantics are explicit: `features` is lowered cuts/openings; bends are separate. Wrong Model hole syntax and wrong Model PMI syntax fail before lowering.
- Existing Template binder, material lookup, inlineSTEP, FEA selection/constitutive, Forge request, and deferred PMI tests cover the other audited silent-success priorities. No new silent omission was reproduced on qualified public fixtures.

## Public documentation boundary

`docs/public` now contains the authoritative landing page, Getting Started, Firmament overview/syntax/geometry/Templates/materials/PMI/Sheet Metal/FEA/STEP import, Forge interop, Cadmata/PMI, CLI/targets/diagnostics, and one Preview 3 support matrix. Root README is a short product landing page. `docs/development/README.md` labels historical and internal documentation without reorganizing or rewriting the archive.

Every public workflow links to a checked-in source fixture. Relative-link qualification rejects broken or escaping links. Terms and platform claims use the Preview 3 release manifest: Windows x64, NativeAOT Forge Host only on `win-x64`, and no Linux/macOS release-binary implication.

## Fresh-user and AI-author dogfood

The automated public-only path performs: first STEP build, typed Template/pattern build, catalog-material FEA, PMI + EdgeFinish AP242 build/reinspection, Sheet Metal formed/flat STEP and SVG, native FEA, inlineSTEP FEA, and the public Forge request. No test reads a milestone report or implementation document.

An AI-style authoring pass using only the public syntax/target/material pages produced three new first-attempt sources under `fixtures/PublicDogfood`: a native Model, a Sheet Metal bracket, and the A36 FEA cantilever. All three parsed and ran on first attempt, with zero syntax guesses after selecting the correct domain page and no hidden documentation lookup. The deliberately wrong Sheet Metal Model hole and datum forms recovered in one diagnostic cycle via the new named errors.

## Regression coverage

- combined two-hole + EdgeFinish + three-record PMI, deterministic AP242 and semantic reinspection
- CLI validate/build PMI evidence parity and authored record names
- working counterbore shaft-diameter PMI through composed/chamfered geometry
- canonical Sheet Metal hole feature count plus independent surface-family evidence
- loud Model-hole and Model-PMI syntax failures in Sheet Metal
- public Markdown relative-link integrity
- fixture-backed Model, Template, material, PMI, Sheet Metal, native/imported FEA examples
- checked-in Forge Host Protocol request and all artifact kinds

## Remaining Preview 3 limitations

- Per-instance selectors and quantity PMI for generated patterns are not a public family.
- There is no separate counterbore-diameter PMI record kind; `HoleDiameter` means shaft diameter.
- Sheet Metal uses semantic regions and manufacturing PMI forms, not universal Model face selectors.
- inlineSTEP and Sheet Metal reconstruction remain bounded; arbitrary imported containment is rejected.
- Production FEA is linear elastic isotropic only.
- Forge Host NativeAOT and the release bundle are qualified on Windows x64 only.
- A result-diff CLI command remains post-Preview 3.

## Validation

- Release build: pass, 0 warnings and 0 errors.
- Full serial .NET suite (`Category!=SlowCorpus`): 2,970 passed, 0 failed. The legacy FrictionLab project has no tests in this default filter and remains explicitly opt-in under repository policy.
- Focused public/diagnostic qualification: 15/15 passed before the full run; the same tests are included in the CLI total.
- Cadmata TSPack: policy check exit 0 (21 multi-version dependency and 114 acknowledged/blocked lifecycle-script notices), typecheck pass, 81/81 tests, production build pass, lint pass.
- Forge four-language witness: Python, Go, Rust, and TypeScript produced byte-identical STEP/flat STEP/SVG hashes (`114cd7c0...`, `88c43737...`, `1657e3bb...`).
- Real CLI validate/build/analyze matrix, A36 FEA witness, relative public-link qualification, and deterministic PMI rerun: pass.
- `git diff --check`: pass; line-ending conversion notices only.
