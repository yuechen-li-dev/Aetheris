# SCENE-AUTHORING-X0

Date: 2026-10-04. Executive verdict: **Accepted**.

Firmament now authors a warehouse-scale spatial environment above ordinary
assemblies with explicit metre coordinates, declarative Room apertures,
shared engineering definitions and OpenUSD/GLB output. The warehouse also
packages through the existing PowerPoint 3D pipeline. Scene remains a distinct
spatial product, with the deliberate qualification limits recorded below.

## Design decisions

1. **Scene versus Assembly.** Scene owns a spatial occurrence product and an
   environment display representation. It never constructs an AssemblySource or
   AssemblyIr to stand in for the world. Each AssemblyFile compiles through the
   unchanged ordinary assembly pipeline and keeps its local mechanical authority.
   Scene placement composes transforms without introducing Interfaces or Mates.
2. **Scene-only constructs.** Scene, Room, Door, Window and Camera are bounded
   spatial/presentation authoring. Model/Part geometry and mechanical relations
   retain their existing owners. Pattern is shared compile-time authoring.
3. **Metres.** Every Scene coordinate has an m/mm suffix; the binder multiplies
   metres by 1000. Scene Set lengths normalize at the existing typed catalog
   boundary. Geometry templates and the kernel retain mm. Neither units nor
   display scale selects a different tolerance policy.
4. **Room boundary authority.** Each rectangular Room owns stable case-sensitive
   floor/ceiling/four-wall paths, reference frames, clear extents and opening
   names. Doors/windows own exact aperture dimensions under their wall path.
   Derived rectangular panels cover wall volume outside the apertures. These
   display/reference solids are independent of BRep/manufacturing authority.
5. **Shared instances.** One ordinary assembly compilation per unique file and
   one ordinary materialization per unique local Part specialization feed shared
   display definitions. Pattern groups are spatial containers. Assembly display
   preparation is shared within a build. Retained Scene sessions reuse the
   existing immutable STEP definition cache and independent Assembly sessions;
   cached topology is never handed back as mutable shared state.
6. **Rules tested.** PascalCase constructs/types, camelCase field labels and
   concrete occurrences/ports, lowercase linking words, preserved domain symbols
   and exact authored/generic/public identities.
7. **Canonical recommendation.** Keep the already accepted mixed convention.
   Scene makes the hierarchy easy to scan: `Room warehouse`, `Door loadingBay`,
   `on: warehouse.southWall`, `width: 1.5m`. Preserve engineering symbols and
   established public contracts. Keep legacy field aliases explicitly scoped to
   their schema owner and reject duplicate aliases.
8. **Deliberate limits.** No nested Scenes, BIM/IFC, arbitrary building systems,
   explicit Wall/Floor/Zone/Light authoring, glazing/door leaves, simulation,
   pathfinding or robot control. Room is axis-aligned. Scene FrameTransform
   declarations and Part source-port alignment are deferred. AssemblyFile is a
   file-context seam; virtual/browser resource compilation is not qualified.
   Whole-scene STEP and a combined USD physics articulation are unsupported.

## Capitalization dogfood

Reviewed [the accepted proposal](../public/firmament/casing-and-layout-proposal.md)
and [current style contract](../public/firmament/language-style.md), alongside
Concept, Interface, Assembly/Subassembly, Pattern, FrameTransform, Appearance,
Template and Function owners. The proposal had already been accepted on
2026-10-01; this milestone tests that decision rather than inventing another
language dialect.

The room witness compiles with both preferred field spelling and its explicit
PascalCase compatibility spelling. Tests compare byte-identical USD/GLB output.
Formatter tests verify comment preservation, idempotence and unchanged identity.
Compiler-owned schema entries, hover, Scene-body construct discovery, Room fields
and unit choices are consumed through the existing language-service API. No
editor-owned grammar or Helios tables were added.

CamelCase fields and occurrence names read more naturally because dimensions
and descriptions stop competing with the constructs. Lowercasing constructs
would flatten that visual hierarchy. Lowercasing domain roles is worse: the
real profile dogfood requires `Loop Outer`, not `Loop outer`; `World`, `Origin`,
axis choices and existing robot parameters/public members retain their spelling.
The bounded formatter preserves layout and specialization argument spelling,
avoiding accidental changes to definition keys or public identities.

New Scene boundaries prefer `floor`, `southWall`, etc. Older authored robot ports
and parameters are not renamed. Any future migration requires paired declaration
and use-site edits. No broad naming migration is part of X0.

Two development documents still described all-PascalCase as canonical. Their
active summaries now point at the accepted mixed convention. The new public
[Scene guide](../public/firmament/scenes.md) records actual qualified syntax.

## Warehouse witness and artifacts

Source: `fixtures/Canonical/Scene/warehouse.firmament`.

One 6 × 4 × 3 m Room, one 1.5 × 2.4 m loading Door, one 1.5 × 1 m Window,
a conveyor on shared supports, a storage structure, three occurrences of the
existing independent Aetheris industrial ATLAS robot on shared pedestals,
five shared package occurrences, finite preview looks and one Hero camera.
The robot geometry/source assembly is unchanged. The ATLAS non-affiliation
disclaimer is retained in source and in the presentation caption.

Inspection reports 3 assembly placements, 14 part placements, 44 shared display
definitions, 243 hierarchy nodes and 211 mesh occurrences. These are different
counts: hierarchy containers/opening metadata have no mesh. The outer bounds
are 6.2 × 4.2 × 3.2 m including the external 100 mm enclosure thickness.

All generated output remains under ignored `artifacts/local/scene/`: readable
USD, GLB, external validator reports, actual usdview captures, Blender import
evidence/preview and an embedded PowerPoint deck.

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll scene inspect fixtures/Canonical/Scene/warehouse.firmament --repeat 2 --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll scene export-usd fixtures/Canonical/Scene/warehouse.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll scene export-glb fixtures/Canonical/Scene/warehouse.firmament --json
```

The existing `scripts/capture-usd-export-x0.py` opens the actual usdview/Hydra
viewport with `/Scene/N_hero_0`. `scripts/render-scene-x0.py` independently
imports GLB into Blender, checks dimensions and shared mesh data, and renders
the authored camera with presentation-only lights. The existing
`presentation compile` pipeline packages GLB and PNG without slide compiler
changes. Desktop PowerPoint interior camera behavior, rotation/save/reopen
remain a manual follow-up; packaging and fallback checks are separate claims.
The reproducible deck input is `fixtures/Canonical/Scene/warehouse-presentation.json`:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python scripts/render-scene-x0.py -- artifacts/local/scene/warehouse.glb artifacts/local/scene
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll presentation compile fixtures/Canonical/Scene/warehouse-presentation.json artifacts/local/scene/warehouse.pptx --json
```

## Performance

Representative CLI process, Release build, one cold and one retained build:

| Phase | Cold | Retained |
|---|---:|---:|
| Bind/expand | 60.0 ms | 7.0 ms |
| Unique engineering definition compilation/reimport | 4810.2 ms | 455.6 ms |
| Spatial composition | 14.7 ms | 2.1 ms |
| Display preparation | 368.6 ms | 191.3 ms |
| Total CLI compilation | 5258.9 ms | 656.2 ms |

Final warehouse serialization: USD 53.0 ms / 2,587,292 bytes; GLB 81.3 ms /
871,284 bytes. The existing presentation compiler packages one slide/model in
57.2 ms. These export timings exclude engineering compilation; Blender rendering
is separate downstream presentation work.

30 unique engineering definitions: cold rebuilt 30; retained reused 30 and
rebuilt zero. This cache reimports fresh exact bodies and still resolves local
assemblies; reuse does not mean zero work. A warehouse regression changes both
Hero camera and a robot station, verifies all definitions are reused, and checks
that the earlier compiled scene's geometry/transforms are unchanged. The package
witness also checks placement and keyed count edits without geometry rebuilds.
Timing is machine/run-specific and not a performance guarantee.

## Qualification

- Focused Scene, existing USD/GLB and schema tests exercise the real producers.
- OpenUSD 25.08 `usdchecker`: success; real usdview capture uses Storm/Hydra.
- OpenUSD material resolution: 211 mesh occurrences resolve five bound preview
  materials/colors, including instance-proxy geometry. The plain usdview capture
  uses its default viewport lighting; the Blender image is the presentation fallback.
- Khronos glTF Validator: zero errors and warnings on the warehouse GLB.
- Blender 5.2.2: physical bounds, camera, 211 mesh occurrences sharing 44 mesh
  data blocks, positive transforms and a real fallback image.
- PowerPoint: existing pipeline embeds the actual GLB and PNG; desktop interaction
  remains unqualified. Package inspection finds one Model3D and one fallback
  picture; embedded media SHA-256 values match the final GLB/PNG exactly.
  Desktop PowerPoint is not installed on this host.
- Fast Core lane: 1005 passed. Final full build/test results follow below.

The first full run overlapped Blender rendering and hit a Core 10-second
tessellation budget (10372 ms). No tolerance, budget or kernel behavior was
changed. It also exposed the expected schema registry golden update and an
existing line-ending-sensitive profile test: LF-only replacement in a CRLF raw
source string omitted the second disjoint edit. That failure reproduced in
isolation. The test now normalizes fixture newlines before the replacement;
the intended geometry comparison and overlap assertion remain intact.

Final review also found that an unfinished occurrence definition could index
past the token list in editor analysis. A local reader guard now returns
`scene-syntax-invalid`; two malformed-source regressions exercise that path.

Final serial gate: **passed**. Release solution build completed with zero errors
and five existing WebAssembly SQLite varargs warnings. The required full serial
solution lane passed **4,285 tests across 20 test projects**, zero failures and
zero skipped tests; Core 1,138, Firmament 1,973 and CLI 461. The FrictionLab
project reports no discoverable tests. The fast Core lane previously passed
1,005 tests. Build/test logs are `artifacts/local/scene-final-build.log` and
`artifacts/local/scene-final-tests.log`.

```powershell
dotnet build Aetheris.slnx -c Release -m:1 --no-restore
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

The corpus file rewritten by the test lane was restored to its initial tracked
content. `git diff --check` passes. No engineering tolerance, timing budget,
robot source, assembly constraint or slide compiler was changed.

## Acceptance evidence map

| Criteria | Evidence |
|---|---|
| 1–2: distinct Scene above Assembly | `CompiledScene` owns spatial/display state; `AssemblyFile` invokes independent ordinary Assembly sessions; no world AssemblyIr is synthesized. |
| 3–4: metres and unchanged kernel mm | Room/placement binder requires explicit suffixes; m/mm equivalence and dimension tests exercise normalized values. |
| 5–8: Room/boundaries/Doors/Windows | `room.firmament` tests six owned references, bounds, aperture dimensions/sill and actual excluded wall volume. |
| 9–12: placed engineering objects, sharing and Pattern | Warehouse tests three existing robot assemblies; keyed package/pedestal occurrences share definitions and retain authored declaration spans. |
| 13–15: real warehouse, USD and compatibility | CLI witness inspection/exports, external usdchecker/usdview, and full serial solution lane. |
| 16–17: language service and formatter | Schema registry, root/body/field completion, hover/diagnostics, comment preservation and formatter idempotence regressions. |
| 18–19: casing dogfood and canonical recommendation | PascalCase compatibility and preferred fields produce byte-identical USD/GLB; recommendation and domain-symbol counterexample above. |
| 20: bounded spatial foundation | Qualified public Scene guide records explicit deferred constructs and excludes BIM/simulation/combined mechanical world authority. |

Additional GLB/PowerPoint evidence: Khronos validation, independent Blender import,
inspected rendered fallback, existing deck compilation and embedded-media equality.
These checks establish static asset/package behavior; they do not establish
desktop PowerPoint rotation or save/reopen behavior.
