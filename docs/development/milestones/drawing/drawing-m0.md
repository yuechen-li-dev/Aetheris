# Drawing M0B

Drawing M0B compiles Part or AssemblyIR products into deterministic DrawingIR, SVG/React preview, and native vector A4 PDF.

```powershell
aetheris drawing compile fixtures/DrawingM0B/machine-assembly-drawing.firmament --out-dir docs/development/milestones/drawing/artifacts/m0b --json
```

The canonical `Machine` fixture contains two occurrences of the reusable `BearingModule` subassembly. Drawing compilation uses AssemblyIR's resolved world transforms and projects each leaf Part body separately. Every projected primitive retains occurrence path, definition identity, and source edge identity; no Boolean flattening is performed.

Supported in M0B:

- Part and nested AssemblyIR sources through their real compile paths;
- flattened leaf-part BOM, aggregated by definition identity and deterministically ordered;
- orthographic/isometric manual views and manual semantic PMI assignment;
- `VisibleOnly` or `VisibleAndHidden` edge intervals, with deterministic face-mesh occlusion, transition splitting, and per-view evidence hashes;
- A4 portrait/landscape pages with stable A-D / 1-6 semantic zones;
- located views, annotations, notes, tables, BOM, and lower-right information block;
- typed `DrawingInfo` Static Records, `with` derivation, semantic-version Revision, and ISO `yyyy-MM-dd` Date;
- Inter text metrics for annotation layout and a searchable embedded Inter Type0 font in PDF;
- sibling React/SVG and native PDF renderers consuming the same DrawingIR.

The occlusion oracle is bounded by the existing deterministic display tessellator. Exact B-rep edges remain drawing authority, but a face patch the tessellator cannot materialize is reported in `unsupportedFaceSupports` and conservatively does not hide an edge. This is not a claim of complete ISO/ASME HLR. Section/detail/exploded views, automated PMI/view selection, and unbounded table pagination remain future work.

The BOM policy is explicit: include every AssemblyIR leaf `Part`, aggregate equal `DefinitionIdentity` values, and retain every occurrence path as evidence. Part number and revision cells remain `-` when the current AssemblyIR definition has no such metadata.
