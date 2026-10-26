# Graph authoring compiler support

The graph usability pass adds compiler-owned support/termination presets and
EdgeFinish schema metadata, including inline completion in its owning block.
String/FaceSelector presets remain suggestions; they do not restrict binding or
promise admission on every host. Preset tests compile the documented choices.

Browser pocket creation exposed `Duplicate construction correspondence for BRep
face 12`: both a construction profile and a material feature were indexed as
primary owners. Section-stack emission now explicitly marks construction origins
as topology aliases when a more specific feature owns that entity. GeometrySourceMap
indexes the primary owner, retains every alias for source/semantic queries, and
rejects dangling aliases and unmarked conflicting owners. Hole and slot roles use
the same producer-owned rule. No geometry construction, surface, trim or dimension
was changed. The actual graph-generated exports retain expected enclosure, bounds,
topology and independently calculated through-hole volume.

Evidence: Release solution build passed; fast Core lane 1,110 passed; full solution
lane 6,039 passed, zero failed, two pre-existing external Drawing Notes renderer
tests skipped. Schema/palette lane 29 passed; graph/correspondence lane 27 passed.
Logs: `artifacts/local/graph-ux-final-build.log`, `graph-ux-final-fast.log`,
`graph-ux-final-full.log`, `graph-ux-schema.log`, `graph-ux-correspondence.log`.
The production WASM/AOT SDK and extracted Windows Helios bundle passed seven
actual graph-only workflows and STEP/GLB/PDF export qualification in HeliosCAD.
The companion report is HeliosCAD's
`docs/development/reports/2026-10/graph-authoring-usability.md`.

The SDK production publish disables node reuse and shared compilation to avoid
persistent compiler hosts. Current public language documentation explicitly notes
that angular Draft is not yet a V2 Modify feature; its bounded V1 executor remains
a separate admission/lowering gap. Verdict: Meaningful progression for the whole
UI request, with that remaining compiler boundary isolated.
