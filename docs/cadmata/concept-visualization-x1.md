# Cadmata concept visualization X1

Cadmata visualizes compiler intent, not merely final tessellation.

## Contract and fixture bridge

`cadmata-concept-viz-x1` is the browser artifact.  Each entity has a stable ID,
semantic kind and layer, optional source span and display geometry, explicit
parent/construction/materialized links, and compiler-published topology IDs.
Selections retain an ordered entity list and closed state.  The browser follows
only those links; it never guesses a BRep match from rendered triangles.

`POST /api/v1/documents/{documentId}/cadmata/fixtures/{fixtureId}` materializes
real compiler fixtures directly through the existing Profile, composition, and
semantic-hole materializers, adds the resulting body to the document, and returns
the matching artifact. Available fixture IDs are `direct-profile`,
`split-compose-chamfer`, `semantic-shaft-hole`, `ctc-01-x3`, and `ctc-01-x4`.

The bridge uses the compiler's `SemanticTopologyCorrespondence`.  In particular,
the shaft-hole route exposes the authored `hole:base.mount`, its axis and entry
circle, entry/exit loops and edges, and its wall face. Composition routes expose
Profile guides/segments plus arrangement/materialized descendants from the
authoritative section-stack plan. CTC-01 X4 additionally exposes each authored
composed-host shaft Hole as one `HoleFeature`; its erased four-arc lowering
Profiles are deliberately omitted from the author-facing scene.

## Viewport architecture

The scene remains split between DisplayIR material renderables and a dedicated
`CadmataCompilerOverlays` scene group. Cadmata overlays are keyed by stable ID;
selection resolution is memoizable and returns face/edge IDs without whole-scene
traversal. Existing BRep picking remains available. Overlay click handling stops
propagation, making source/semantic evidence win over material picks.

The compact inspector/tool region uses the published `machinalayout` package as
the declared layout dependency boundary; Three.js rendering remains regular
React Three Fiber rather than being forced through a layout library.

## Layers and interaction

The artifact supports independently visible material, BRep edges, concept points,
axes, regions, Profile guides, resolved Profile loops, compose regions,
selections, and diagnostics. The current compact viewport control exposes Profile
and semantic toggles; the inspector remains the source selection surface.

Selecting an entity traverses explicit parent/child/construction/materialized
links, highlights all published face/edge descendants, and shows label, stable
ID, kind, role, source, descendant totals, and diagnostic evidence. A missing
referenced entity produces `Cadmata.MissingDescendant`; it does not create a fake
highlight. Chain ordering is preserved in the artifact's `orderedEntityIds` for
the subsequent ordered-marker renderer.

## Analytic display facade

DisplayIR already retains analytic face family and parameters for planes,
cylinders, cones, spheres, and tori. Cadmata continues to use its bounded mesh
preview/fallback representation while preserving that analytic metadata per face.
This is an analytic display facade, not true GPU analytic-surface rendering.

Tessellation is a display carrier. Analytic geometry remains the source of
surface identity and normals.

The existing renderer keeps one mesh per face, so smooth normals cannot bridge
real BRep face boundaries. Cylindrical/conical faces may use the analytic preview
or bounded fallback mesh according to DisplayIR. DisplayIR publishes face-local
angular and axial domain hints for trimmed cylinders and cones, and the preview
tessellates only that bounded `(u,v)` domain. Exact trim/silhouette curve
rendering, analytic-normal shaders, and display LOD remain future work.

## Evidence and limits

The fixture bridge has been compiled against direct Profile extrusion,
split-compose, semantic shaft-hole, and CTC-01 X3/X4 sources. CTC X3 supplies the
expanded lobe/scaffold/Profile/Compose evidence emitted by the composition
materializer. The split-compose fixture currently visualizes the source-grounded
pre-finish composition correspondence; its EdgeFinish consumer is recorded by
the source artifact, while replacement chamfer topology needs a dedicated
post-finish correspondence producer before it can truthfully claim descendant
faces. This is intentionally surfaced as a current limitation rather than
heuristically mapped.

CTC X4 carries the four Ø35 semantic shaft holes through arrangement and the
authoritative section stack. Selecting a Hole follows its compiler-published
entry/exit loops, boundary edges, and all slab-split wall faces. The next
visualization pressure case remains post-EdgeFinish replacement correspondence;
replacement faces must not be inferred heuristically.
