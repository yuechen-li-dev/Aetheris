# SEMANTIC-CORRESPONDENCE-X1

## Executive verdict

**Meaningful progression.** A plain, single Box now carries construction-owned face identity from Firmament through canonical BRep tessellation to Web SDK picking and Helios selector copy/source navigation. The browser witness picked a top and side face, retained `Body.face(+Z)` after a size edit, and rebuilt a Datum reference to the picked face. Hole walls, source-addressable edges, and STEP reimport correspondence are still missing, so the full X1 acceptance criteria are not met.

## Identity inventory and authority

| Layer | Identity in this milestone | Authority and limit |
| --- | --- | --- |
| Firmament source / AIR | Box feature name and lowered primitive feature ID | Authored feature identity; a Box declaration source range is attached to the Web tree and face ranges. |
| BRep | `FaceId`, `EdgeId`, `BrepExtrudeConstructionTopology` | IDs are exact within a constructed body. They are not persistent selectors by themselves. The extrusion now captures the IDs it creates. |
| Semantic topology | `SemanticTopologyCorrespondence` and descendants | Existing Firmament correspondence type; Box descendants bind stable role IDs to captured BRep IDs. Other producers already use this type for selection/PMI, but no general Web bridge is asserted. |
| PMI / FEA | Existing axis face targets and semantic selection resolver | `face(+Z)` parses in the existing PMI path. This work does not change FEA selectors or PMI association. |
| STEP AP242 | Exported geometry and existing PMI references | STEP export still works and reimports. The imported `FaceId` and STEP entity numbers are not treated as the authored Box face identity. |
| Web display | Tessellation `FacePatch.FaceId`, triangle ranges | A patch joins to correspondence only by the same canonical BRep `FaceId`. Triangle indices and patch order only locate a range; they do not create selectors. |
| Forge / imported bodies / sheet metal | Existing host or import identities | No new source correspondence is claimed for these routes. Imported source-less geometry remains non-addressable in this Web path. |

## Actual chain and architecture choice

For the admitted single-Box route:

`Box Body` → lowered Box feature `Body` → `BrepPrimitives.CreateBoxWithTopology` → captured extrusion face IDs → `BoxConstructionCorrespondence` → direct canonical BRep tessellation → Web mesh face range → SDK `describeSelection` → Helios Inspector `Copy Selector` / `Go to Source`.

The Web runtime still produces AP242 STEP for export. It displays the already constructed BRep for this route because reimport assigns new runtime topology IDs and has no qualified source-to-import map. A direct in-process BRep reference is smaller and more reliable than encoding metadata into STEP or matching imported faces by centroid, area, or order. Other feature families continue through the existing STEP reimport display path. A sidecar keyed to STEP entities was not introduced because it would require deterministic export/import entity correspondence and validation that this milestone has not established.

The map lives in `FirmamentStepExportResult.RuntimeCorrespondence` beside `RuntimeBody`; both are excluded from JSON. Web mesh ranges carry the semantic ID, source selector, source reference, addressability, and build revision. A new successful rebuild creates new ranges; old selections are cleared in Helios. A changed Box size retains semantic role IDs; renaming the Box changes its semantic IDs. Helios offers **Reference Face in Source** for an admitted axis face; it inserts an existing `Pmi { Datum ... { Target: face(+Z) } }` construct. The generated Hole `On:` field uses a different grammar and does not accept this selector.

## Coverage

| Family | Body | Face | Edge | Web selector |
| --- | --- | --- | --- | --- |
| Single Box without modifying features | Feature ID and declaration source | Six construction-bound axis faces, `DerivedStable` | One captured top-front edge ID, no source syntax | Axis face selectors only |
| Box with Hole / Hole wall | Existing feature identity | Runtime display IDs; existing producer correspondence is not transported by this route | Runtime only | No trusted display selector |
| Boss / Pocket / Loft / Sweep / Revolve | Existing compiler identities where present | No Web correspondence qualified | No Web correspondence qualified | None claimed |
| Sheet metal | Existing kernel semantic identities | No Web correspondence qualified | No Web correspondence qualified | None claimed |
| Imported STEP / Forge | Host or import identity where available | Reimported display topology has no source mapping | Same | None claimed |
| Synthetic display-only face | Runtime entity | `RuntimeOnly` | Runtime only | No selector |

An edge is captured from construction, but `edge(TopFront)` is **not** emitted as Firmament syntax because the admitted parser has no corresponding edge selector. No Hole wall selector is invented. Face role names come from the Box profile's explicit ordered corners and the extrusion's ordered side construction; no post-build geometry search assigns them.

## Verification

- `dotnet test Aetheris.Kernel.Firmament.Tests --filter FullyQualifiedName~BoxRuntimeCorrespondenceTests --no-restore -v:q`: 4 passed. Covers six exact face bindings and their surface normals, size/rename identity behavior, existing PMI face syntax parsing and compilation, and STEP export/reimport of six faces.
- `node --test tests/selection.test.mjs` in the Web SDK: 2 passed. Covers mapped selection/reverse lookup and RuntimeOnly selector refusal.
- `npm run sdk:install`, `npm run build`, `npm test -- --run` in HeliosCAD: succeeded; 26 UI tests passed.
- `dotnet test Aetheris.CLI.Tests --filter FullyQualifiedName~FirmamentV2SemanticPmiStepPipelineTests --no-restore -v:q`: 2 passed. `dotnet test Aetheris.Kernel.Core.Tests --filter FullyQualifiedName~PmiModelTests --no-restore -v:q`: 5 passed. `dotnet test Aetheris.FEA.Tests --no-restore -v:q`: 43 passed. `dotnet test Aetheris.SheetMetal.Tests --no-restore -v:q`: 99 passed.
- `npx playwright test tests/box-correspondence.spec.ts --reporter=line` in HeliosCAD: passed in Chromium against the installed WASM package. It picked the top face, copied `face(+Z)`, navigated to the Box declaration, picked a side face, selected the source feature, rebuilt after a size edit, found `Body.face(+Z)` again, inserted a Datum reference from the Inspector, and rebuilt with zero diagnostics.
- An independent UI-only attempt first copied the face selector but pasted it into the generated Hole `On:` field, which is not a valid selector context. This exposed the missing affordance and motivated **Reference Face in Source**. In a fresh UI-only retest, that button inserted `Pmi { Datum SelectedFace1 { Target: face(+Z) } }`; Rebuild reached revision 3 with zero Problems and the tester saved the project. The tester reported that TOP view looked triangular despite selecting the correct `Body.face(+Z)` face; that visual observation is not yet diagnosed.

## Remaining acceptance gaps

The current selector proof covers existing `face(+Z)` PMI parsing/compilation and browser source insertion. It does not prove that every accepted selector context resolves to the same BRep face; PMI association needs a stronger end-to-end topology assertion. Body selection uses the authored feature ID, but there is no standalone canonical body selector text. Edge source syntax and Hole wall correspondence are absent. STEP reimport remains geometrically verified but semantically lossy. Full X1 acceptance therefore remains open.
