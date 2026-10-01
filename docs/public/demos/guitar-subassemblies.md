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
├─ neck-assembly.firmament          neck, board, frets, inlays, nut and headstock
│  ├─ Neck.firmament                exact six-section neck and published ports
│  └─ tuners-assembly.firmament     keyed tuner posts, washers and buttons
├─ electronics-assembly.firmament   knobs, selector and decorative covers
│  └─ pickups-assembly.firmament    two complete pickup occurrences
│     └─ pickup.firmament          housing, coil functions and pole feature pattern
├─ bridge-assembly.firmament        bridge, stop tailpiece and keyed saddles
└─ strings-assembly.firmament       WireForm template and six formed routes
```

These are real reusable `Subassembly` definitions, not text fragments or imported
STEP packages. Tuners are a nested occurrence under Neck; Pickups nest under
Electronics. The root retains five readable placements. Each module owns its
local layout; moving a root occurrence moves its already-solved children.
Existing absolute coordinates now describe module-local layout. Replacing all
of those with new mounting ports is a separate refinement.

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
13 used source documents, each with a hash. Existing include checks and private
semantic boundaries remain in force. Source-row offsets still refer to the
expanded source; this change does not add per-file editor source maps.

Reproduce:

```powershell
python scripts/create-guitar-x0.py
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --json --profile
./scripts/qualify-guitar-x0.ps1 -OutDir artifacts/local/guitar-subassemblies -Viewport
```

The existing generator writes the modules reproducibly. Profile interpolation,
fret formulas and string-route calculations remain its current responsibilities;
Authoring D is not part of this reorganization. Downstream sunburst shading also
remains unchanged. Generated exports and evidence belong under artifacts/local.

## Qualification

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
