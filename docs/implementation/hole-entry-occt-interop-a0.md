# HOLE-ENTRY-OCCT-INTEROP-A0 — hole-entry STEP root cause

## Classification

This was an **Aetheris topology/materialization defect**, not a CAD Assistant or OCCT defect. The AP242 serializer faithfully wrote the malformed source BRep it received. Three errors existed in the countersink-specific rebuild:

1. the cone and cylinder seam edges used duplicate coincident vertices instead of the circular boundary vertices, and their coedges were not ordered as a continuous wire;
2. the inward cone and cylinder faces retained `SameSense=true`, although their analytic normals point away from the hole axis and therefore into the solid material;
3. the conical support surface did not contain its trim boundaries. The failing STEP placed a radius-3 cone at `z=6` with axis `-Z`, while the seam boundary was radius 4 at `z=6` and radius 3 at `z=5`.

The third error was the decisive CAD Assistant trigger. Wire and face-sense corrections were necessary but continued to stall until the support cone was constructed directly from the admitted hole profile: placement `(0,0,5)`, placement radius `3`, axis `+Z`, semi-angle `pi/4`.

## Reproduction

The retained original is `fixtures/FirmamentV2/Chamfer/evidence/hole-entry.step`, SHA-256 `F3B8AFD4BD55F388475D35751683A915287EF3CD6D2B42F569DB40D658911E14`. `tools/Open-In-CadAssistant.ps1` resolved `C:\Program Files\CAD Assistant\CADAssistant.exe` and launched the exact file twice. Both imports deterministically remained at 50%, `Loading: 1 / 2`. Cancelling changed the UI to `Aborting...` but did not finish within ten seconds. The earlier M6 run produced `OSD_Exception_ACCESS_VIOLATION`; that exception was not reproduced in this run, so only the 50% stall is classified as deterministic.

SolidWorks 2026 imported the original as a surface body and visibly showed cross-hole chord/seam artifacts. That independent result agreed with the STEP graph audit and ruled out an OCCT-only interpretation.

The known-good convex control is `cylinder-top-rim.step`, SHA-256 `AB37BBB068C86AEDFD39A1C998A221B722049E4183F705E57DC1F57EC686DE6C`; CAD Assistant opens and renders it.

## Source BRep and STEP audit

The original countersink body had V/E/F = 15/17/8. Four vertices duplicated the entry, transition, and shaft seam locations. Edge-to-face incidence still counted every edge twice, which is why both the binding validator and `aetheris analyze` called it enclosed-manifold. That assessment did not prove ordered wire closure or curve-on-surface compatibility.

The fixed source BRep has V/E/F = 11/17/8. Cone and cylinder seams now share the circular boundary vertices; every face loop is topologically continuous; the two void-wall face bindings have `SameSense=false`; and every cone boundary vertex satisfies the cone equation.

The original and fixed STEP files have identical coarse entity counts: 8 `ADVANCED_FACE`, 10 bounds/loops, 34 `ORIENTED_EDGE`, 17 `EDGE_CURVE`, 3 `CIRCLE`, 1 `CONICAL_SURFACE`, 1 `CYLINDRICAL_SURFACE`, 6 `PLANE`, 1 `CLOSED_SHELL`, and 1 `MANIFOLD_SOLID_BREP`. Counts alone therefore could not detect the defect.

In the fixed graph, the cone loop is ordered seam -> transition circle -> reversed seam -> entry circle and uses the same STEP vertex references at every junction. Its support is `CONICAL_SURFACE` at `(0,0,5)`, axis `+Z`, radius `3`, angle `0.785398163397448`; the cone and cylinder `ADVANCED_FACE` entities both use `.F.`.

Both artifacts still produce the same exact semantic volume, `8290.236017900337`, through `analytic-box-minus-z-hole`. This demonstrates why exact volume and self-reimport were insufficient proof: neither checked trim boundaries against their support surface.

## Controlled variants

| Variant | Single intended change | SHA-256 | Aetheris reimport | CAD Assistant |
|---|---|---|---|---|
| retained original | none | `F3B8AF...1E14` | succeeds, V=15 | deterministic 50% stall |
| shared seam vertices only | remove four duplicate vertices | `425CFB...133F` | succeeds, V=11 | not credited; loop order still invalid |
| shared vertices + ordered loops | continuous cone/cylinder wires | `8A2625...6B86` | succeeds | 50% stall |
| plus inward face sense | cone/cylinder `SameSense=false` | `433380...8F6D` | succeeds | 50% stall |
| plus profile-derived cone support | correct placement, radius, and axis | `48376D...09B9` | succeeds, V=11 | opens and renders correctly |

Only the original and final artifacts are retained; intermediate hashes document the bounded experiments without adding redundant binaries.

## Fix and regression coverage

`BrepBooleanBoxCylinderHoleBuilder.CreateSteppedCoaxialCountersinkBody` now:

- shares periodic seam endpoints with circle topology;
- emits continuous coedge order;
- constructs the support cone from transition center/radius and entry direction;
- marks internal analytic wall faces opposite-sense.

The source-BRep regression asserts ordered loop closure, inward face sense, and cone-boundary-on-support compatibility. The fixture pipeline regression asserts 11 reimported vertices and the exact cone placement/radius/axis and face sense for both canonical and offset/parameter variants.

## Independent readers and remaining risk

CAD Assistant is the available OCCT reader. No installed `DRAWEXE`, OCCT command-line sample, or other cheap second OCCT path was found, so no large harness was added. SolidWorks supplied the independent non-OCCT evidence for the original malformed artifact. The final artifact's successful CAD Assistant import is the decisive interoperability proof.

The fix is deliberately limited to the stepped coaxial countersink/hole-entry builder. Similar older periodic builders may also use coincident-but-distinct seam vertices; they were not generalized in this milestone. A future bounded validator should promote ordered wire closure and curve-on-surface checks into export preflight after the existing corpus is audited, rather than enabling it globally and breaking unrelated legacy constructions without classification.

## Validation record

- `dotnet restore Aetheris.slnx`: passed.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed with 0 errors; existing project/package warnings remain.
- Firmament filter `HoleEntry|Chamfer|Cone|Cylinder|Step|Interop|Occt`: 265 passed, 0 failed.
- CLI filter `HoleEntry|Chamfer|Step|Analyze|Interop`: 136 passed, 0 failed.
- `aetheris analyze` passed for original, fixed, and convex-control artifacts.
- `aetheris analyze volume` returned the same exact semantic volume for original and fixed, demonstrating that volume was not a structural validator.
- `dotnet run --project Aetheris.CLI -- --help`: passed.
- `git diff --check`: passed.
- CAD Assistant: original stalled at 50% twice; fixed artifact imported, reached `Loading: 2 / 2 Displaying: finished`, and rendered the box, annular conical entry, and cylindrical shaft.

## Final answer

It was our mistake. CAD Assistant was reacting to an invalid analytic face construction, especially a conical support surface that did not contain its own trim edges. With the source topology, face sense, and cone placement corrected, the exact analytic hole-entry STEP imports and renders in CAD Assistant without an importer-specific workaround.
