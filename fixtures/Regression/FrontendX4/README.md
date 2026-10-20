# FrontendX4: authoring retest corpus

This corpus covers what is still open after the X0–X2 frontend work. It is the "repeat all twelve
sources" acceptance step from the
[language decision register](../../../docs/development/FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md),
made concrete. Each case is the smallest file showing one remaining problem. Each must-build case
comes with a reference spelling that builds today, and the reference is the geometry oracle.

Recorded at `0a72e6f1`: **13 of 32 checks pass.** The 13 are the 11 guards plus two diagnostic
cases (e02, f01). Every `fix` and `design` case fails, and so does the chimera.

Principal-plane amendment (2026-10-19): f01's original source is retained and now
must build in XZ. Its geometry oracle is an explicit traced Construction Plane
with local axes +X/+Z and normal -Y. This admission change is recorded in the
language decision register; an XZ layout is no longer a required rejection.

## Run

```sh
dotnet build Aetheris.CLI -c Release
python fixtures/Regression/FrontendX4/run.py            # everything
python fixtures/Regression/FrontendX4/run.py --only a03,c03,chimera
```

Each case goes through both `validate --json` and `build`.

- **Expected `valid`:** both commands must succeed, and `inspect` + `verify` must match the
  reference built by the same CLI. That means equal face, edge and vertex counts, surface
  families, structural assessment and bounding box, and volume within 1e-9 relative. The case
  must also match the geometry recorded in `expected.json`.
- **Expected `invalid`:** both commands must fail, and the named `code` must be reported at the
  named source `line`.

## Rules for whoever fixes these

1. **Don't edit a case or the chimera to make it pass.** Fix the frontend. The exception is a
   case marked `design`: its syntax depends on a register decision. If you decide a different
   spelling, record the decision in the register and change the case, its reference and the
   chimera line in the same commit.
2. **Guards must keep passing**, with identical geometry. They cover today's admitted spellings
   of the same constructs.
3. **`--record` rewrites the recorded geometry.** Use it only when a geometry change is intended
   and explained in the commit, as the M4 report did for its hash changes.
4. **Original X4 scope was diagnostic-only for kernel gaps.** The explicitly authorized
   2026-10-15 follow-up now admits d05 (disjoint Box top-boundary Fillet + ThroughAll Hole).
   Its original source is unchanged; only that manifest expectation gains a geometry baseline.
   Touching/intersecting holes and unsupported selectors remain rejection tests. See the
   [capability report](../../../docs/release/FIRMAMENT-FILLET-HOLE.md).

## The chimera

`chimera.firmament` is one ordinary part: a 70 × 60 × 6 mm NEMA17 motor mount with an octagonal
outline, a through cable slot, a 22 mm pilot, four M3 motor holes, four M5 frame holes and a
0.5 mm top chamfer. It is written the way the docs say an author should be able to write it.
Every construct that does not build today is tagged `[Xn #k / Fm]` in the file.

`chimera.baseline.firmament` is the same part in syntax that builds today. The chimera is done
when it validates, builds, and matches the baseline:
- 98 faces, 240 edges, 144 vertices;
- bounding box [-35,-30,0]–[35,30,6];
- volume 20769.2350 mm³.

That volume agrees with the analytic value to 0.014%: the prism is 20797.46 mm³, and the chamfer
removes about 31 mm³.

| Chimera construct | Case |
|---|---|
| `Point2(Pitch / 2, …)` in a `Set` | a03 |
| `[x, y]` Set entries | c06 |
| `On: Top` in a Feature hole | c05 |
| `Static Thickness` in Model, consumed by `To:` | a05 |
| `Polygon2 { Points: [...] }` with arithmetic | f02, f04 |
| `Pocket … End: ThroughAll` | e01 |
| `On: face(+Z)`, `Center: [0mm, 0mm]` | c03, c04 |
| `EdgeFinish<Chamfer>` | d01 |
| Model-scope `Pattern … Over` | b01, b04 |

## Cases

| Id | Kind | Register | Finding | Today | Required |
|---|---|---|---|---|---|
| a01 | design | X2 #19 | F1 | `Unknown statement 'let'` (3,3) | `let` (or the decided keyword) in Model |
| a03 | fix | X2 #9 | F18 | `static-set-entry-type-mismatch` | Static arithmetic in Set entries |
| a05 | fix | X2 #9/#19 | F1 | `static-scalar-consumer-unsupported` | Model Static consumed by a Compose interval |
| a06 | fix | X2 #9 | — | the same error, with the Static **unused** | An unused declaration can't have an unsupported consumer |
| b01 | fix | X3 #12/#15 | F15 | `Unknown statement 'Hole'` at the Model line | Documented Model-scope Pattern with a Feature |
| b04 | fix | X3 #12 | F15 | same | Model-scope Pattern beside a Modify |
| c03 | fix | X2 #21 | F19 | `face(+Z)` and `Body.Top` switch to the Concept route; `Top` is refused as "only +Z" | All four planar-face spellings |
| c04 | fix | X3 #10 | F23 | `canonical-point2-invalid` | `Center: [x, y]` in Modify |
| c05 | fix | X2 #21 | F19 | "only +Z" for `On: Top` | `Top` in a Feature hole |
| c06 | fix | X3 #10 | F23 | `static-set-entry-type-mismatch` | `[x, y]` entries in `Set<Point2>` |
| d01 | fix | X3 #11 | F22 | `SemanticSourceNotFound` at (1,1), no message | `EdgeFinish<Chamfer>`, keeping `Kind:` as an alias |
| d05 | capability follow-up | #24 | F5 | Originally `CombinedFeatureSelectionLost` at (1,1) | Now builds the original source: 11 faces, 23 edges, 14 vertices; 4 fillet cylinders plus the hole wall |
| e01 | fix | X3 #13 | F21 | `pocket-missing-required-field` | `Pocket … End: ThroughAll` = `Remove … Role: ThroughCut` |
| e02 | diagnostic | X3 #13 | F21 | correct at (6,5) | Keep it |
| f01 | fix | X0 #5 | F9 | formerly rejected XZ layout | Admit and match the explicit oriented-plane reference |
| f02 | design | X3 #20 | F11 | `polygon2-variant-unsupported` | Inline `Points` list |
| f03 | diagnostic | X3 #20 | F11 | `ConceptPathProfileNotAdmittedByCompose` (1,1) | Endpoint diagnostic at the first Segment (5), or admit and add a reference |
| f04 | design | X3 #20 + X2 #9 | F11 F18 | `polygon2-variant-unsupported` | Inline `Points` with arithmetic |
| g01 | design | X3 #20 | F13 | `polygon2-variant-unsupported` | Revolve a `Polygon2` point list |
| h02 | fix | X3 #12/#15 | F15 | `Unknown statement 'Pattern'` (19,3) | The syntax.md Feature + Pattern grid, as taught |

**Guards** (must stay as they are):
- a02, a04: Model Static, Box arithmetic;
- b02, b03: Pattern in Modify, under Model and Struct roots;
- c01, c02: `+Z` under both roots;
- d02–d04: the three admitted EdgeFinish spellings;
- h00, h01: the Concept Grid pattern under both roots. Wrapping it in Model was F16; it is fixed
  now.

## Observations outside the frontend

- **The chimera's chamfer splits every through-feature wall at the chamfer plane.** Without the
  chamfer the part has 50 faces and 11 cylindrical surfaces. With it, it has 98 faces and 22
  cylindrical surfaces (76 cylindrical faces in `inspect`). The geometry is exact; the topology is
  heavier than it needs to be. This is not part of acceptance.
- **Binding-level diagnostics still default to (1,1) or the Model header** (a05, a06, c03, c04,
  d01, d05, e01, f02). Syntax-level ones already carry exact spans.

## Repair qualification (2026-10-15)

See [the repair and qualification report](../../../docs/release/FIRMAMENT-FRONTEND-X4.md).
The `Today` column above preserves Claude's original observations. The a01
design specimen now uses the decided `Static` spelling, as its reference and
the chimera already did. Other specimens and geometry goldens are unchanged.
The runner's checks and tolerances are unchanged; generated STEP and verify
evidence now default to ignored `artifacts/local/frontend-x4/` rather than the
system temporary directory. `--record` was not used for the repair.


## Fillet + Hole capability follow-up (2026-10-15)

The user authorized the engineering capability after X4 qualification. `d05` now
expects acceptance, with unchanged source. Every other recorded geometry value
remains unchanged. The numerical `verify` volume is a deterministic regression
fingerprint, not analytic volume authority. The combined plan's analytic volume
is checked independently against section integration and its emitted cylinder /
ellipse supports are checked through AP242 preflight and round trip.
