# GEOM-SOURCEMAP-X0 — compiler-owned geometry provenance

Verdict: **Meaningful progression**. Box and one simple through Hole carry construction-owned semantic identity and compiler source spans into the direct BRep display path. Extrude, Boss, and Pocket do not yet have qualified Web source maps, and Hole wall has no parser/binder-qualified source selector.

## Authority and propagation

The V2 parser records the Box construct span. Semantic Hole declarations already have parser spans. Materialization assigns stable output roles to BRep face/edge IDs; `SemanticTopologyCorrespondence` carries these roles and spans for one build. `GeometrySourceMap` indexes BRep face/edge to provenance and semantic key/source symbol back to descendants. It rejects duplicate BRep entity mappings and permits multiple entities under one semantic key. The Web runtime tessellates the **same BRep body**, copies correspondence onto face triangle ranges and true BRep edge polylines, and includes a build revision. Web SDK picking reads the owning range or edge record directly. The SDK rebuild replaces its indexes, and Helios clears selected topology after a successful rebuild.

The semantic key is a construction role such as `Body.face(+Z)` or `material:hole:Body.H:wall`; it is independent of BRep ordinal. The current map has one primary origin per entity. It does not claim contributor chains or provenance through STEP export/reimport. Unmapped imported/display topology remains `RuntimeOnly` with no selector or source range. `geometrySourceMap()` exposes a compact diagnostic dump. SDK reverse indexes support semantic key, source symbol, and entity lookup.

| Feature | Construction roles and BRep | Display pick | Selector |
| --- | --- | --- | --- |
| Box | Six named faces and construction edges; parser Box span | Qualified direct BRep patches and edges | `face(±X/±Y/±Z)` for faces; existing PMI binder accepts it. Edges withheld. |
| Simple through Hole | Wall face, entry/exit loops and edges; parser Hole span | Wall and true rim edges carry `Body.H` owner | Withheld: simple Hole grammar rejects advanced `Selection ... HoleWall` declaration. |
| Extrude | Existing profile construction roles in other paths | Not qualified for this Web route | Not claimed. |
| Boss | No qualified X0 path | Not qualified | Not claimed. |
| Pocket | No qualified X0 path | Not qualified | Not claimed. |

## Helios witness

The browser Box test picks top `+Z`, copies `face(+Z)`, navigates to the compiler Box span, and rebuilds a model containing that selector. It also picks a real BRep edge with no invented selector. The Hole browser test picks the inside wall as `Body.H`, navigates to the Hole declaration, selects that declaration to recover the Hole feature, and repeats the pick after a diameter edit. Hole Copy Selector remains disabled. This is the exact remaining blocker for the fresh-agent “reference the inside wall in code” task; no STEP recognition or fabricated selector is used.

## Evidence and limits

- `BoxRuntimeCorrespondenceTests` and `HoleRuntimeCorrespondenceTests`: eight focused native tests pass, including source spans, direct BRep mapping, size/diameter identity, duplicate-entity rejection, one-to-many index behavior, STEP reimport, top tessellation area, and the Hole grammar rejection.
- Web SDK selection tests: four pass; browser Box/Hole tests pass using the locally repackaged WASM SDK.
- Existing PMI CLI, FEA, and SheetMetal regression gates passed during this work; they were run before the final source-map index edits, which do not change their execution paths.
- No construction-overhead or pick-latency benchmark has been established. Index lookups are dictionary based; no measured performance claim is made.

Next blocker: give Hole wall a canonical Firmament selector with parser, binder, and semantic resolution round trip. Extend construction roles through the real Extrude/Boss/Pocket paths before claiming full X0 acceptance.
