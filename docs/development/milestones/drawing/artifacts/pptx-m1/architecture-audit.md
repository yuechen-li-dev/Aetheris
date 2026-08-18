# Drawing M0B architecture audit and PPTX backend design

## Existing authority retained

`DrawingIr` already normalizes physical page size/orientation, content rectangle, views/viewports/scales, projected visible/silhouette/hidden point sequences, semantic anchors, assigned PMI, annotation candidates and selected placement, Design/BOM tables, perimeter zones, DrawingInfo, located notes, Inter hierarchy, provenance, collision evidence, and measured source/projection/layout/PDF performance. `DrawingSvgRenderer` and `DrawingVectorPdfWriter` consume this data without choosing layout.

The PPTX backend is a sibling renderer. It receives the completed DrawingIR and makes no view, annotation, table, zone, or metadata ordering decision.

## Toolchain choice

The production backend uses direct deterministic OPC/Open XML generation with `ZipArchive`. This avoids Office installation, commercial runtime licenses, cloud authentication, and a large production dependency. Low-level package construction is confined to `DrawingPptxWriter`. `DocumentFormat.OpenXml` is test-only and validates all canonical packages against the schema.

## Shape mapping

| DrawingIR element | Native PowerPoint result | Stable group/name |
|---|---|---|
| visible/silhouette segment | line | `View.<view>.Edge.<stable-id>.<segment>` |
| hidden segment | dashed line | same edge identity |
| curve point sequence | editable line approximation | containing View geometry group |
| annotation leader/dimension | lines with arrowheads | `PMI.<identity>` group |
| annotation engineering display | Inter text box | `PMI.<identity>.Text` |
| datum/feature-control body | bordered text box | containing PMI group |
| zones | border lines and text | `Page.<n>.Zones`, `Zone.*` |
| DrawingInfo | bordered rules/text | `Metadata.DrawingInfo` |
| Design/BOM | DrawingML native table | `Table.Design.*`, `Table.BOM.*` |
| review | highlight, leader, callout text | `Review.<id>.Callout` |

View geometry and each PMI annotation are separate groups. A dimension can be selected/moved without ungrouping its view. Tables remain directly cell-editable.

## Determinism

Millimetres map once at 36,000 EMU/mm. Package entry order and timestamps, document properties, relationship IDs, slide order, shape traversal, table order, Review order, and semantic object names are stable. `determinism.json` records byte-identical repeat hashes.
