# DRAWING-NOTES-X1 release report

## Executive verdict

**Accepted.** An LLM can use Aetheris Drawing Notes to externalize the dimensional and reference structure of a dense engineering drawing before attempting CAD reconstruction. The PDF remains authority; the notebook is source-linked working memory, not CAD truth.

## Delivered capability

`Aetheris.Drawing` provides a shared typed model for PDF pages, page-space regions, annotations, dimensions, local datums, semantic classifications, relations, confidence, aliases, passes, and unresolved questions. The CLI and browser UI use that model. PDFium-backed rendering produces page images and exact source crops with page, bounds, source hash, and renderer metadata. JSON and Markdown exports are canonical and timestamp-free, with page, named region, and top-left PDF point bounds on every important note.

The coherent CLI family supports inspection, rendering, crops, project creation, region/note/dimension/relation/pass additions, validation, filtered export, UI assets, and the local browser UI. The UI supplies page thumbnails, fit/width/zoom, Shift-drag pan, drag-to-highlight, D/V/Q shortcuts, selected and related overlays, a relation tree, and Markdown preview.

No OCR, CAD, BRep, STEP, constraint solver, dimension inference, or datum inference is required. Native text extraction was explored only as dogfood evidence; its poor order confirms that it cannot be treated as geometry.

## iPhone 17 Pro Max dogfood

The proprietary PDF was used only as a user-supplied local input and was not added to the repository. Its recorded SHA-256 is `47bcbc8d562cda09aa96cc96db04df9246634175a19974077d16729db38996f4`. The visible metadata recorded in the notebook is title `iPhone 17 Pro Max Dimensional Drawings`, date `2025-09-09`, and cover plus sheets 1-4.

- Pages inspected: 2-5
- Agent passes: overall/rear geometry, then camera/sensor/material keepouts
- Regions: 23
- Annotations: 112
- Dimensions: 64
- Datums: 14
- Relationships: 40
- Unresolved questions: 6
- Markdown size: about 43 KB

Representative product-envelope notes are 77.98 mm width and 163.43 mm length from page 2 `MainFrontView`, and 8.75 mm thickness from page 2 `RightSideView`. Page 2 `DetailD` records separate local X=0 and Y=0 datums and distinct camera, flash, microphone, and sensor features. Pages 3-5 classify camera cones and control volumes as `SensorKeepout`, material/RF restrictions as `MaterialRestriction`, and the physical envelope as `ProductGeometry`.

## Representative relation graph

```text
RearCamera1
  -> center X: +14.37 mm
     -> DetailD.X0
  -> center Y: -14.37 mm
     -> DetailD.Y0
  -> rear-camera-1 cone
     -> RearCamera1
     -> camera/sensor keepout section on page 4
  -> related feature
     -> Detail D rear-camera layout on page 2
```

The second pass also preserves Camera 2, Camera 3, rear flash, and rear sensor as separate features rather than collapsing similar cone dimensions into one meaning.

## Qualification evidence

- The repository-owned synthetic PDF has one front view, one side view, two datums, a local detail, dimensions, a section, and a functional keepout. It is regenerable from its checked-in script and was visually inspected after rendering.
- Automated coverage exercises page count and dimensions, rendering/crop determinism, regions and nesting, annotations, parsing (including multiplicity before a diameter), datums/features/relations, confidence/questions, duplicate/conflict findings, source-hash mismatch, deterministic JSON/Markdown, filters, UI load/save, CLI behavior, passes, and coordinate persistence.
- The complete solution test run passed. `Aetheris.Drawing.Tests` passed 13 tests and `Aetheris.CLI.Tests` passed all 443 tests. The pre-existing FrictionLab assembly reports that it contains no discoverable tests.
- A current win-x64 framework-dependent package was restored and published, copied to a temporary directory outside the repository, and used there to inspect and render both the synthetic fixture and the supplied five-page PDF. The package carried its own PDFtoImage and PDFium binaries; no repository-relative runtime dependency was used.
- Browser smoke testing loaded a project, rendered a real thumbnail and crisp page, created a dimension by drag plus `D`, saved it, showed the source-space rectangle in Markdown preview, and highlighted the selection. No server was left running.
- A fresh agent created the first notebook without being given a dimension graph, then extended the same project for pages 3-5. Final validation reports a matching source hash and zero structural issues.
- A second clean-room agent received only the original PDF and exported Markdown. It recovered the 163.43 x 77.98 x 8.75 mm envelope, identified Detail D as the rear-camera layout authority, separated page-3 Camera Control keepouts from product geometry, enumerated all six unresolved items, and explained the Detail D local X/Y datums with source bounds. It reported materially less visual searching. Its one self-containment complaint—the missing bounds convention—was fixed by adding `top-left PDF points [x,y,width,height]` to every Markdown export.

## Failure modes and X2 guidance

- Crowded dimension stacks and overlapping linework still require deliberate zoomed crops.
- Rotated ordinate labels made positive/negative signs easy to transpose; high-resolution Detail D review caught and corrected one such error.
- Ambiguous leaders remain explicit questions instead of guessed attachment.
- Local datum interpretation is recordable but transforms are not solved.
- Section association is manual; similar camera/sensor cones need stable semantic labels and relations.
- Extracted PDF text is not geometrically ordered. Page 2 yielded mostly footer text despite dense visible content, while later pages produced misleading reading order.
- Confirmed corrections currently require artifact editing followed by validation because the CLI has add operations but no edit/remove command.
- A generic linear-distance type is absent, so some distance notes remain `Unknown` while their axis, interpretation, classification, and relations carry the meaning.

The next useful increment is a small, source-safe edit/remove CLI surface and focused diagnostics for unsupported enum/relation values—not OCR or automatic drawing inference.

## Downstream handoff verdict

The notebook is good enough to hand to a fresh CAD reconstruction agent for a bounded experiment: reconstruct the high-confidence external envelope and source-linked rear-camera centers first, while treating all keepouts as non-solid reference geometry and refusing the six unresolved areas. It is not sufficient for an exact phone model: the rear plateau planform, corner-profile continuity, forward-sensor partition, and complete 3D material-restriction union remain intentionally unresolved.
