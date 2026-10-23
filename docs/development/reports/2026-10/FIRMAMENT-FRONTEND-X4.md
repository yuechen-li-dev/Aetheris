# Firmament Frontend X4 — Claude authoring retest repairs

Subsequent authorized engineering work now admits the unchanged d05 Fillet + Hole
source. Its earlier rejection disposition below is historical; see the
[Fillet + Hole qualification](FIRMAMENT-FILLET-HOLE.md). Other X4 geometry
baselines remain unchanged, and this report's original evidence is preserved.

Implementation base: `7525f37f` (Claude's corpus commit), 2026-10-15.
Scope: the 32 checks in `fixtures/Regression/FrontendX4`, with existing geometry
and legitimate compatibility retained. This is production frontend repair, not a
shadow admission milestone. **Accepted for the X4 authoring corpus and exercised
compatibility set:** all 32 checks pass through the real validate/build paths,
with the original geometry comparisons and diagnostic lines retained.

## Reproduction and repairs

The unchanged corpus initially reproduced **13/32 passes**: eleven geometry
guards and two diagnostics. No finding was dismissed as speculative without
running its source. The corpus README and original recorded geometry remain the
historical evidence.

| Cases | Observed cause | Repair / disposition |
| --- | --- | --- |
| a01 | Model `let` was unknown | Choose existing `Static`, including inferred scalar type. This is the sole changed design specimen; its reference and the chimera already use Static. |
| a03, c06 | Set coordinate checks used literal regexes and rejected expressions/arrays | Bind scalar references with the shared compile-time environment, evaluate typed coordinates through the existing AST numeric owner, and retain keyed Set identity. |
| a05, a06 | A blanket construction-consumer guard rejected any Static, including unused values | Transport bound AST value edits into Compose lowering; erase compile-time declarations after binding and retain their value metadata. |
| b01, b04 | Set Pattern expansion placed Hole declarations directly in Model scope | Resolve the unique material target and emit into its Modify scope; reject ambiguous ownership. |
| c03, c05 | Face spelling selected Concept-point interpretation | Bind planar aliases to the active host face. Spatial point references still use their existing owner and frame checks. |
| c04 | Direct mechanical Hole required a constructor despite an equivalent typed array | Use the existing typed AST vector binder for either form. |
| d01 | Generic EdgeFinish never reached the existing Kind consumer | Bind generic/Kind variants once, reject conflicts, and project the header/alias at AST spans. Close profile finish fields through their actual reads so extras cannot disappear. |
| d05 | Capability failure discarded its source span | Carry a typed kernel diagnostic span and map LF/expanded offsets back to the retained original declaration. Fillet + Hole remains rejected. |
| e01 | Pocket only bound Depth | Explicit ThroughAll lowers to existing Remove/ThroughCut; conflicting depth/floor policies reject. e02 remains an accidental through-depth error. |
| f01, f03 | Derivative missing-operand noise obscured profile errors; Segment location lost | Preserve causal profile diagnostics and locate rejected inline endpoints at original Segment declarations. Inline Segment endpoints remain unsupported. |
| f02, f04 | Explicit polygons required a separately named Set | Admit inline typed points through the existing explicit boundary checks and guide generation. No new polygon kernel. |
| g01 | A closed polygon guide had no Profile value for Revolve | Adapt that directly consumed closed polygon to its existing closed Profile before ordinary Revolve binding. |
| h02 | Model-level Source Pattern had no Modify target; sized Box had no Concept projection bounds | Wrap the Pattern for the sole body and derive projection bounds from the same established Box dimensions. |
| chimera | Combined occurrences of the issues above | Compile through the normal CLI and compare with its unchanged reference and recorded measurements. |
| all eleven guards | Existing admitted spellings | Retain their source and geometry oracles unchanged. |

The full-suite iteration exposed legacy readers sensitive to reprinted syntax.
The final transport changes only AST-owned value/header/wrapper spans, preserving
unrelated ProfileDelta anchors and Concept descriptors. The Expose reader now
uses existing AST field nodes, so semicolons cannot become part of a reference.
Two obsolete test expectations were reconciled with the deliberate new behavior:
bracket points are admitted, and missing paths report the causal binding error
rather than a derivative Compose operand error.

## Decisions and boundaries

The [language decision register](FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md)
records these decisions before the allowed design specimen change. `Static`
remains the Model-local value keyword; Feature-local `let` is unchanged.
`Polygon2 { points: [...] }` is the inline explicit form. Named vertices remain
available for stable identities under reordering. Generic finish variants retain
Kind aliases, and explicit ThroughAll retains finite-depth Pocket diagnostics.
Public syntax, boundary and geometry documentation is updated in place.

There is no new whole-document dispatch route, fatality allowlist, independent
field schema, macro system, or geometry operation. The new span transport uses
existing AST ownership; generated nodes without a matching original declaration
still receive a disclosed enclosing location rather than a fabricated precise
span. This does not claim universal expression consolidation or complete expansion
source maps.

The chamfer's existing wall splitting (98 faces / 22 cylindrical surfaces) is
unchanged and remains a separate topology optimization. No motion work is included.

## Reproduction

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
python fixtures/Regression/FrontendX4/run.py
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
```

`--record` was not used. `expected.json`, the reference sources and the chimera
are unchanged. Raw logs and per-run artifacts belong under ignored
`artifacts/local/`; the [compact evidence](../../../release/FIRMAMENT-FRONTEND-X4.evidence.json)
retains final counts, source hashes and geometry comparison results.

## Final qualification

| Gate | Final result |
| --- | --- |
| Release solution build | Passed; existing compiler/analyzer/WASM warnings remain. |
| Claude corpus | **32/32**, including all eleven unchanged guards and all four diagnostic cases. |
| Chimera | **98 faces, 240 edges, 144 vertices**, bounds `[-35,-30,0]` to `[35,30,6]`, volume approximately **20769.235 mm³**; surface families and volume match its unchanged baseline and recorded geometry. |
| Focused final tests | **93 passed**, including the corpus, rejection boundaries and repaired legacy consumers. |
| Core fast lane | **1,068 passed**; the final full lane also includes its slow corpus. |
| Full regression | **5,112 passed, zero failed, zero skipped**, across 20 executing test assemblies. FrictionLab's existing empty test project has no tests to execute. |
| Complete fixture inventory | **1,375 sources** classified, **368/368 canonical sources structurally represented**, **zero unresolved mismatches**. Standalone production admits 571; other rows retain their negative, capability, compatibility or consumer-required dispositions. |
| Existing corpus comparison | All **1,326 pre-existing source hashes** unchanged; no previously admitted standalone source regressed. Five former rejections now admit the repaired Top alias. |
| Frozen showcase STEP comparison | **26/26 byte-identical** to the X1 closeout evidence, with original source hashes checked first. Includes assembly, threaded, PMI, profile and sculpting examples. |
| Additional Top aliases | All five boss/pocket/hole sources produce **byte-identical STEP** to equivalent `+Z` sources and equal topology, bounds, surface families and numerical volume. |
| Runtime cleanup | No dotnet, testhost, MSBuild, VBCSCompiler or aetheris hosts remained in the final snapshot. |

The five additional admissions are `boss-hole`, `boss-hole-unique`,
`boss-stack-hole`, `boss-stack-hole-unique` and `pocket-hole` under
`Regression/LanguageBurnIn`. Their ordinary through-hole volumes also agree with
analytic stock/addition/removal calculations within 0.001%, a separately stated
check for the existing numerical mass integrator. This does **not** relax the
1e-9 reference comparisons or alter the corpus runner's geometry tolerances.

Three unusual negative-folder sources were rechecked against the final binary:
the singular FEA map rejects at solve with `thinwall-map-singular`, the misfiled
Rect2/TraceLoop source builds the correct six-face solid, and the tray source
admits geometry while reporting its intended DFM warning. Their dispositions
are explicit rather than inferred from directory names.

For complete corpus replay, collect production outcomes with
`frontend-shadow --corpus fixtures --production --output-dir artifacts/local/frontend-x4/corpus`
(the historical command name is developer instrumentation, not a separate
admission parser). Pass the report to `scripts/classify-firmament-frontend-x1.py`.
The evidence JSON's `downstreamDispositions` object supplies the three reviewed
source-hash-bound exceptions; their expected observations are recorded above. Recheck them with:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll fea fixtures/Invalid/FEA/ExperimentalShell/singular-map.firmament --out-dir artifacts/local/frontend-x4/singular-map --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Invalid/Pipeline/traceloop-non-loop.firmament --output artifacts/local/frontend-x4/misfiled-positive.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll sheetmetal inspect fixtures/Invalid/SheetMetal/tray-dfm-violation.firmament --json
```

Extract `downstreamDispositions` from the evidence JSON into an ignored JSON file
and pass that path as the classifier's `--downstream-evidence` argument after
rechecking the observations.

## Code-quality review and remaining scope

The review checked field ownership, severity, source spans, source transport,
parser dispatch, shared validation/build admission and process lifetime. The
coordinate literal regex checks and Expose field regex were removed. The only
added regex construction wraps an existing unchanged Profile-mode lookup so its
read participates in AST field consumption. No fatal-code allowlist, global
mutable state, new document dispatcher or product-specific validator was added.

The compiler still has domain adapters and bounded source serialization between
AST binding and established lowerers. This pass does not claim universal semantic
parity, general inline Segment endpoints, every cross-domain expression form, or
complete generated-source provenance. Those remain tracked in the original
language register. None of the reported X4 cases remains blocked, and this pass
requires no further language decision from the user.
