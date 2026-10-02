# Guitar as a multi-file assembly

The body crown, pickups and bridge now use [Concept-directed Fixed seating](../firmament/datum-seating.md)
against the shared `hardware-layout.firmament` deck. The bridge's former 4mm gap is removed.

The pickup construction has since been consolidated into [one functional part](../firmament/functional-pickup.md), placed twice. The qualification table below records the earlier organization-only snapshot; current counts are 91 visible parts and 53 geometry definitions.

The preferred entry is [guitar.firmasm](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm).
It contains only Include declarations and five top-level subassembly occurrences.
The old guitar-x0.firmament entry is a small compatibility composition root.

```text
guitar.firmasm
├─ hardware-definitions.firmament   shared exact part templates and HardwareSite
├─ body-assembly.firmament          back, binding and carved top
│  └─ MahoganyBack / IvoryBinding / CarvedMaple.firmament  SectionChain sources
│     └─ body-outline.firmament  Shared boundary for nine Concept placements
├─ neck-assembly.firmament          neck, board, frets, inlays, nut and headstock
│  ├─ Neck.firmament                exact six-section neck and published ports
│  └─ tuners-assembly.firmament     local bass row and mirrored treble row
│     └─ tuner.firmament           complete washer/post/button component
├─ electronics-assembly.firmament   knobs, selector and decorative covers
│  ├─ knob.firmament               reusable complete control knob
│  ├─ selector.firmament           reusable complete pickup selector
│  └─ pickups-assembly.firmament    two complete pickup occurrences
│     └─ pickup.firmament          housing, coil functions and pole feature pattern
├─ bridge-assembly.firmament        bridge, stop tailpiece and keyed saddles
└─ strings-assembly.firmament       WireForm template and six formed routes
```

These are real reusable `Subassembly` definitions, not text fragments or imported
STEP packages. Tuners are a nested occurrence under Neck; Pickups nest under
Electronics. The root retains five readable placements. Each module owns its
local layout; moving a root occurrence moves its already-solved children.
Knobs, selector and tuners now publish component Mount frames. Tuners use
headstock-local linear/reflected site recipes and one shared component definition;
the group seats on the headstock front plus veneer thickness. See
[Components and mounting sites](../firmament/assembly-components-and-sites.md).
Remaining explicit control seat coordinates describe module-local layout.

Neck exposes only NutMount, HeelMount and HeadFront. Its NutWorld/HeadTilt frames
and Fixed VeneerSeat remain private to that definition. The parent cannot reach
private part ports through the neck boundary. The local fixed relationship is
recorded in the solved neck definition; it is not recreated as a root joint.

The compiler now publishes concrete authored part ports before reusable local
assembly solving. Template ports use the existing typed specialization frontend;
section ports use the existing SectionChain binder with materialization disabled.
Actual geometry still goes through M1 once per shared part definition. M1 replaces
early publication values with the materialized definition's ports without creating
duplicates. Declaration collection retains geometry templates and finite data from
all included modules, rather than stopping at the first subassembly declaration.

The immutable project-snapshot path resolves included modules and the four
SectionChain resources from supplied documents. Its dependency report includes
19 used source documents, each with a hash. Existing include checks and private
semantic boundaries remain in force. Source-row offsets still refer to the
expanded source; this change does not add per-file editor source maps.

Reproduce:

```powershell
python scripts/create-guitar-x0.py
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --json --profile
./scripts/qualify-guitar-x0.ps1 -OutDir artifacts/local/guitar-subassemblies -Viewport
```

The existing generator preserves the directly authored component and electronics/
tuner modules and writes the remaining generated modules reproducibly. Profile interpolation,
fret formulas and string-route calculations remain its current responsibilities;
Authoring D is not part of this reorganization. Downstream sunburst shading also
remains unchanged. Generated exports and evidence belong under artifacts/local.

## Component ownership refinement

The current model has 10 reusable assembly definitions, four named knobs, one
selector, and six occurrences of one tuner definition. The tuners form a 36mm
bass-side row and a reflected treble-side row, both seated on the headstock
veneer through its published frame. The treble row clocks the same tuner 180deg
to turn its button outward. See the [implemented site syntax](../firmament/assembly-components-and-sites.md).

The controls preserve their previous placements within numerical roundoff; all
52 other parts with unchanged identities, excluding updated strings, preserve
their world-transform matrices exactly. Surface, pickup and WireForm display
checks have no unmatched edges or inward normal triangles. The fresh Cycles hero
uses the exported 91 product meshes without changing their topology.

This dogfood case also exposed an AP242 shared-child bug: reconstructing local
transforms independently from posed world occurrences produced tiny numerical
differences, and STEP reimport expanded 197 bodies instead of 91. The exporter
now consumes the existing solved definition-local transforms. The corrected
reimport has 53 geometry definitions, 91 bodies, 38 subassembly occurrences,
129 total occurrences and hierarchy depth 6. Both the mounting-row fixture and
the guitar have regression assertions for exact part occurrence counts.

Current artifacts are under ignored `artifacts/local/component-authoring/`:
`guitar.step`, `guitar.usda`, `beauty/hero.png`, `beauty/guitar-studio.blend`,
`after.json`, `placement-check.json`, `mesh-check.json`, `generator-check.json`
and `step-reimport-final.log`. Earlier failing STEP evidence is retained under
explicit before-fix names/logs. Camera interaction was not remeasured in this
refinement; the original viewer evidence below remains historical.

Release solution build passed. The core fast lane passed 1,004 tests; 36 focused
foundation/site/guitar tests and 20 focused site/guitar/AP242 tests passed. The
final full solution gate, with projects and xUnit collections serialized, passed
4,108 tests with seven existing skips across 20 projects. The first default full
run had one unchanged core tessellation deadline failure and one old dependency
count assertion (16 versus the new 19 files). The count assertion was corrected;
the core test passed in isolation and both controlled full runs passed. No
tessellation budget or assertion was relaxed. See `full.log`,
`core-timeout-isolated.log`, `full-final-serial.log` and `test-summary.json` in
the artifact directory for the distinct results.

## Original modularization qualification (historical)

The modular witness was qualified through the real CLI, immutable project
snapshot, AP242 exporter/importer and OpenUSD viewer:

| Evidence | Result |
|---|---|
| Composition | 5 root groups, 7 reusable assembly definitions, 7 keyed patterns |
| Geometry | 55 shared exact part definitions, 107 visible parts |
| Display hierarchy | 164 nodes, including the 7 new groups |
| Baseline comparison | All position/normal/index buffers identical; all 107 world transforms identical |
| AP242 reimport | 107 bodies, hierarchy depth 5; source assembly hierarchy retained |
| Cold compilation | 10.455 s |
| Warm recompilation | 4.873 s |
| Display preparation, cold / warm | 0.489 / 0.392 s |
| OpenUSD | No composition errors; maximum world-transform error 1.23e-15 |
| 36 usdview camera rotations | 0 compile calls, 0 rebuilds, 0 stage changes; mean orbit/readback 26.18 ms |

Cold includes first-use JIT/library initialization. Warm recompiles the source in
the same process without a persistent model cache. Neither includes process
startup or dotnet build. Camera evidence is from the standalone exported USD
viewer, which has no compiler connection.

Release solution build passed, the core fast lane passed 1,005 tests, and 36
focused foundation/guitar/project tests passed. The default full solution gate
reported 4,034 passed and one unrelated posed-slider display timeout at its
six-second tessellation budget. That test passed in 0.78 s in isolation. A
controlled full solution gate with xUnit collections serialized passed all
4,035 tests; budgets and assertions were unchanged. This supports contention
as the explanation, without claiming the default gate was green.

Fresh exports and evidence are in `artifacts/local/guitar-subassemblies/`:
`guitar.step`, `guitar.usda`, `inspection.json`, `timings.json`,
`baseline-comparison.json`, `step-assembly-analysis.json`, `usd-validation.json`
and `viewport/viewer.json`. Test logs are in
`artifacts/local/guitar-foundation/subassembly-*.log`.

The existing Cycles hero images remain applicable because geometry buffers and
world placements are identical. No new beauty render was required for this
organization change; the fresh shaded viewport was visually checked. The
presentation witness is preserved while its authored structure is easier to
navigate. Remaining module-local coordinate layout and Python-generated profile
and route data are explicitly deferred refinements.
