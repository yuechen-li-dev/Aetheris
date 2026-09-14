# Reading engineering drawings with Drawing Notes

Aetheris Drawing Notes is scratch paper for an engineer or agent reading a dense PDF. The PDF remains authority. A note records an interpretation, its confidence, and the exact page-space rectangle that led to it; it is not CAD truth and is never silently promoted into geometry.

Drawing Notes is deliberately separate from BRep, STEP, Firmament lowering, OCR, and automatic dimension inference. It works when the reader can see a value and its leaders, highlight that source area, and state the relationship explicitly.

## Start a notebook

```powershell
aetheris drawing inspect drawing.pdf --json
aetheris drawing notes create drawing.pdf --out notes `
  --name "Fixture - Drawing Notes" `
  --title "Aetheris Drawing Notes Synthetic Fixture" `
  --sheets "SHEET 1 OF 1" `
  --json
```

The project contains `drawing-notes.json`, an initial `drawing-notes.md`, and local browser UI assets. It records the source SHA-256, page count and sizes, visible metadata supplied by the reader, and the packaged PDF renderer identity. It does not copy the PDF.

Coordinates are PDF page-space points (`1/72 inch`) with a top-left origin. A rectangle is `x,y,width,height`. This makes annotations independent of monitor resolution and zoom.

## Work locally

Create a named view, its local datum, a dimension, and the relationship between them:

```powershell
$project = "notes/drawing-notes.json"

aetheris drawing notes add-region $project `
  --id DetailD --name "Detail D" --page 1 `
  --bounds "465,60,180,200" --type Detail

aetheris drawing notes add-note $project `
  --id DetailD.Origin --label "Detail D local origin" `
  --category Datum --page 1 --bounds "485,80,25,160" `
  --region DetailD --confidence High

aetheris drawing notes add-dimension $project `
  --id Camera1.CenterX --label "Camera 1 center X" `
  --value "25.00 mm" --type Coordinate --axis X `
  --page 1 --bounds "510,95,80,20" --region DetailD `
  --class ProductGeometry --confidence High

aetheris drawing notes add-relation $project `
  --id Camera1.CenterX.FromDetailD `
  --from Camera1.CenterX --to DetailD.Origin `
  --kind ReferencedFrom
```

Use stable semantic labels such as `ProductBody`, `RearCamera1`, or `DetailD.Origin`. Preserve drawing wording in `--original-text`; put your interpretation in `--note` or `--interpretation`. Use `--aliases "REAR CAMERA 1,Camera 1"` for distinct source labels that name the same notebook feature.

Supported relation kinds are `Targets`, `ReferencedFrom`, `LocatedIn`, `DetailOf`, `SectionOf`, `KeepoutFor`, `CoordinateIn`, and `RelatedTo`. For example, use `KeepoutFor` rather than an open-ended synonym such as `AppliesTo`; the bounded vocabulary keeps graph exports consistent.

Do not force uncertainty into a known type. Use `Unknown`, `Low`, or `Unresolved`, and add a first-class `Question` note. A camera field-of-view cone or material exclusion region should be classified as `SensorKeepout`, `AccessoryKeepout`, or `MaterialRestriction`, not `ProductGeometry`.

## Highlight and crop

Render a crisp page or export pixels from the source PDF only:

```powershell
aetheris drawing render drawing.pdf --page 1 --out page-1.png --dpi 240
aetheris drawing crop drawing.pdf --page 1 `
  --bounds "465,60,180,200" --name DetailD `
  --out notes/crops/p01-detail-d.png --dpi 300
```

Every crop receives a `.crop.json` sidecar containing its page, source bounds, document hash, renderer, and DPI. The PNG is a rendering of the real source area, not an OCR redraw.

For interactive work, launch the local browser UI:

```powershell
aetheris drawing notes serve notes/drawing-notes.json `
  --source drawing.pdf --port 4178
```

The UI provides pages and regions on the left, a pan/zoom PDF rendering with overlays in the center, note and relation editing on the right, and a textual relationship tree. Drag a rectangle and press `D` for a dimension, `V` for a view, or `Q` for a question. The server checks the PDF hash on load and save; UI and CLI persist the same model.

## Export and hand off

```powershell
aetheris drawing notes validate notes/drawing-notes.json `
  --source drawing.pdf --json

aetheris drawing notes export notes/drawing-notes.json `
  --format markdown --out notes/drawing-notes.md
```

JSON retains the complete graph. Markdown is the compact LLM handoff. Both are byte-stable for the same project and contain no generated timestamp. Filters keep downstream context bounded:

```powershell
--page 3
--region DetailD
--class ProductGeometry
--unresolved
```

Validation reports likely duplicate source annotations without merging them. Conflicting meanings assigned to the same page rectangle remain explicit review findings. A changed PDF produces `drawing-source-hash-mismatch` rather than silently attaching old notes to a new revision.

The repository-owned qualification fixture is [`synthetic-engineering-drawing.pdf`](../../fixtures/DrawingNotes/synthetic-engineering-drawing.pdf). It includes a front and side view, two global datums, a detail-local datum, dimensions, a section, a functional keepout, and an unresolved question. Regenerate it with `fixtures/DrawingNotes/generate_synthetic.py`.

## Handoff discipline

Give a downstream CAD agent both the original PDF and exported Markdown. Ask it to consume high-confidence `ProductGeometry` notes first, keep local datums distinct, and revisit every low/unresolved item in the source. Do not ask it to model keepout cones as product solids. When a leader or section association is ambiguous, the correct notebook entry is a cited question, not an invented constraint.
