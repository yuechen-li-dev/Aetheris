# STEP-ASSEMBLY-X0 — AP242 product structure import

## Contract

`Step242AssemblyImporter.Import` reads AP242 product structure into shared product definitions and distinct occurrences. `Step242Importer.ImportBody` retains its strict single-body contract. An assembly is never silently reduced to its first body. The Cadmata STEP import endpoint returns an assembly display packet when the product-structure route succeeds; ordinary single-body files keep their existing response.

The admitted source hierarchy uses `PRODUCT`, `PRODUCT_DEFINITION_FORMATION`, `PRODUCT_DEFINITION`, `PRODUCT_DEFINITION_SHAPE`, `SHAPE_DEFINITION_REPRESENTATION`, `SHAPE_REPRESENTATION`, `NEXT_ASSEMBLY_USAGE_OCCURRENCE`, `CONTEXT_DEPENDENT_SHAPE_REPRESENTATION`, `REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION`, `ITEM_DEFINED_TRANSFORMATION`, and `AXIS2_PLACEMENT_3D`. Definition geometry uses the existing exact BRep importer, once per definition. Instances share that geometry and carry local rigid 4×4 transforms. World transforms compose child local placement with parent world placement in Aetheris row-vector order. A missing occurrence transform fails with the occurrence name and STEP entity number; non-rigid placements fail explicitly.

`Step242ProductStructure.Provenance` is `SourceAssembly` for explicit AP242 hierarchy and `RecoveredMultiBodyAssembly` for multiple rigid roots without useful hierarchy. The latter makes one synthetic definition and occurrence per rigid root. This inference does not claim source-authored hierarchy and never unions the bodies.

`aetheris analyze assembly <file.step> --json` reads this graph without writing a package. `aetheris asm import-step <file.step> --out <directory> --json` writes the reusable `.firmasm` package. Both report hierarchy, definitions, occurrences, shared instances, transforms, world bounds, and import timings.

When one authored product representation contains several independent rigid roots, import retains that product as a geometryless parent and creates one body definition per root. Each occurrence of the authored product receives child body occurrences, so repeated products still share their component geometry. A focused witness verifies two body definitions beneath each of two repeated product occurrences.

## Canonical self witness

Source: `fixtures/Regression/Assembly/bearing-module-family-with-legacy-placement.firmament`. The real CLI exported it with `asm export-ap242`, imported it with `asm import-step`, exported the resulting `.firmasm` again, and imported the second STEP. Generated evidence is under ignored `artifacts/local/step-assembly-x0/`.

| Measure | Source | First STEP import | Second STEP import |
| --- | ---: | ---: | ---: |
| Body definitions | 3 | 3 | 3 |
| Body occurrences | 9 | 9 | 9 |
| Subassembly occurrences | 2 | 2 | 2 |
| Non-root occurrences | 11 | 11 | 11 |
| Repeated body instances beyond first use | 6 | 6 | 6 |
| World bbox minimum (mm) | `[-40,-10,0]` | `[-40,-10,0]` | `[-40,-10,0]` |
| World bbox maximum (mm) | `[40,53,20]` | `[40,53,20]` | `[40,53,20]` |

The source has 12 IR instances including the root. The STEP importer represents the root through `RootDefinitionStableId` and lists its 11 descendants. Both `LeftModule` and `RightModule` retain their named children. The imported JSON report lists each definition, occurrence, parent, local transform, world transform, and bounding box. The source and imported world bounds agree exactly for this witness.

Comparing each descendant by its hierarchy name path found all 11 imported paths and a maximum world-matrix element delta of `0`. All three component definitions remained enclosed manifolds after reexport/reimport, with equal face, edge, and vertex counts and equal local bounding boxes. Each has 10 faces, 20 edges, and 12 vertices in this fixture.

One local run measured STEP parse `7.37 ms`, unique geometry definition import `136.86 ms`, assembly graph construction `4.07 ms`, and Cadmata display preparation `7.35 ms`. These are diagnostic wall times from one run, not performance guarantees. Geometry import ran for three unique definitions, not nine part occurrences.

## Cadmata and external witness

The Cadmata import endpoint was exercised through its HTTP API using an Aetheris-exported repeated-instance STEP. It returned one tessellated definition and two placed occurrences. A live browser launch with the canonical STEP showed the `Machine` product tree, both module instances and all nine visible part occurrences at their placed positions; the status read `Assembly ready: 11 occurrences, 3 definitions.` No assembly-import error appeared. Cadmata's existing assembly renderer consumes shared definition meshes and occurrence world transforms.

The viewer's leftover prototype fixture buttons were removed from the semantic inspector. The existing fixture API remains available to automated tests; ordinary users open their own STEP files through the import control.

The existing `testdata/step242/OCCT/as1.step` witness imports as 27 occurrences and 5 geometry definitions, of which 18 occurrences are parts. This is a bounded secondary compatibility witness, not a claim of universal STEP dialect support.

## Limits

- The Cadmata STEP assembly response is an inspectable scene packet. It is not yet persisted in `DocumentSession` for assembly editing, body-level picking, or document-level reexport. The `.firmasm` CLI package is the supported import/reexport route.
- Synthetic multibody identity uses rigid-root STEP entity identifiers because those files have no stronger occurrence identity. Explicit product and usage IDs take precedence when available.
- Other historical assembly encodings and unsupported non-rigid transforms require explicit diagnostics and further witnesses. No kinematics, mates, constraints, or USD lowering are part of X0.
