# Canonical Firmament V2 cookbook

Everything in this directory is current authoring guidance. Examples progress from small syntax landmarks through focused operations to practical integrations. Semicolons are optional where line/block structure is unambiguous; use them when they make dense declarations clearer.

| I want to… | Start here | Main concepts |
|---|---|---|
| create a box | `Basics/box.firmament` | `Model`, `Units`, named `Box`, `Assert` |
| create a cylinder | `Basics/cylinder.firmament` | direct `Cylinder` |
| create analytic primitives | `Primitives/` | `Frustum`, `RoundedBox`, `Sphere`, `Cone`, `Torus` |
| draw a readable local profile | `Profiles/concept-path-line-arc-profile.firmament` | `Concept Path`, line, arc, closed profile, extrusion |
| derive a semantic profile variant | `Profiles/profile-delta-recess-extrusion.firmament` | Table, `with`, `Template`, `ProfileDelta`, ordinary extrusion |
| add a rectangular boss | `Features/Boss/rectangular-boss.firmament` | first-class finite `Boss` |
| sweep a circular profile | `Features/Sweep/circular-planar-path-sweep.firmament` | bounded planar `Concept Path`, line/arc, constant circular `Sweep` |
| make wire geometry | `Features/Sweep/` | tangent open paths, analytic cylinders/tori, capped solid |
| add a boss and bore | `Features/Boss/circular-boss-through-hole.firmament` | circular `Boss`, through shaft `Hole` |
| machine a finite pocket | `Features/Pocket/rectangular-pocket.firmament` | finite depth and minimum floor |
| make a through or blind hole | `Features/Holes/through-hole.firmament`, `blind-hole.firmament` | shaft end conditions |
| add a counterbore or countersink | `Features/Holes/counterbore.firmament`, `countersink.firmament` | public hole variants |
| cut a slot/opening | `Features/Slots/straight-slot.firmament` | capsule slot, through opening |
| chamfer or fillet an edge | `Features/EdgeFinish/` | current `EdgeFinish` vocabulary |
| repeat features from data | `Patterns/record-array-hole-pattern.firmament` | Record array, finite `Pattern` |
| add semantic PMI | `PMI/multiple-hole-dimensions-with-chamfer.firmament` | Datum, tolerances, `DatumRefs` |
| use a typed product Template | `Templates/generic-mounting-plate.firmament` | value parameters, `Require`, Concept Struct output |
| build a Paperclip Template | `Templates/paperclip.firmament` | `Record`, `Static`, `with`, `Template<PaperclipPolicy>`, circular `Sweep` |
| use a CNC policy | `Templates/cnc-dfm-policy.firmament` | Record defaults, `with`, `Template<Policy>`, enforcement |
| make Sheet Metal | `SheetMetal/l-bracket-with-hole.firmament` | material, bend, planar hole, formed/flat output |
| apply a bounded mathematical sculpt | `Sculpting/sculpted-housing.firmament` | immutable `BodyState`, `OffsetRegion`, locality, preservation contracts |
| make a data-driven Sheet Metal tab | `SheetMetal/profile-delta-tab-family.firmament` | Table, `with`, reusable `ProfileDelta Tab` |
| run FEA | `FEA/cantilever.firmament` | material, `Fixed`, `Force`, four result families |
| analyze imported STEP | `FEA/inline-step-cantilever.firmament` | repository-stable `inlineSTEP`, face selection, FEA |
| author an assembly | `Assembly/bearing-module.firmament` | Interface, Mate, nested identity, tolerance stack |
| route a piping system | `Piping/pump-skid.firmament` | logical Connection, equipment-owned nozzle Interfaces/mates, scoped owner exemption, explicit/accepted Routes, strict foreign KeepOut clearance, fittings, BOM/Cut List |
| combine machining operations | `Integration/machined-mounting-block.firmament` | Boss, Pocket, holes, counterbores, chamfer, PMI |

`Pocket` never means through-all; use a supported `Hole` or `Slot` for through removal. General loft, helix, arbitrary Boolean authoring, and freeform surface features are not in Preview 3; see [`docs/public/reference/supported-features.md`](../../docs/public/reference/supported-features.md).

## Ownership

| Area | Primary test owner |
|---|---|
| Basics, Primitives, Profiles, Features, Patterns, Templates | `Aetheris.Kernel.Firmament.Tests` |
| PMI and STEP round-trip | `Aetheris.CLI.Tests` and `Aetheris.Kernel.Firmament.Tests` |
| Sheet Metal | `Aetheris.SheetMetal.Tests` |
| Sculpting | `Aetheris.Modules.Tests` and `Aetheris.CLI.Tests` |
| FEA and Materials | `Aetheris.FEA.Tests` |
| Assembly | `Aetheris.Kernel.Firmament.Tests` assembly suite |
| Piping | `PipingX3Tests` |
| Drawings | `Aetheris.CLI.Tests` drawing suite |

The compact action map is `qualification.json`; `coverage.json` is the public-operation guard. Generated outputs go to `artifacts/local/`, never beside these sources.
