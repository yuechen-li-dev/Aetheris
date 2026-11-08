# Single-edge finishes in the Helios viewport

The viewport already picked individual BRep edges, but the Graph finish actions
selected an entire face boundary. Plain profile-extrusion display correspondence
also dropped its construction-owned cap edges. The new workflow preserves one
owner per cap edge and exports a compiler-owned body / support / single-segment
finish binding. It never turns a display edge number into source syntax.

Choose Edges in the viewport toolbar, select an edge, and choose Fillet selected
edge or Chamfer selected edge. The transaction locks the target to that edge,
shows only its size controls, previews automatically in gold over the accepted
solid, and commits one undoable source edit on Apply. Cancel and refused sizes
retain the original source. The existing graph actions still finish face rims.

The existing geometry planners are unchanged. Qualified source bindings cover
outer profile cap edges and the Box +X/+Z face pair owned by the existing
localized route. Longitudinal, imported, split/ambiguous and other unqualified
edges remain disabled. Unsupported histories, curved targets and degenerating
sizes are still checked by the real compiler. Finite profile fillets retain
their existing endpoint clearance; arbitrary-body finishing is not implemented
by this UI change. Placed nonidentity occurrences cannot supply these bindings.

The runtime builds the edge-binding index once per mesh, using construction
correspondence rather than nearest-face recognition. SDK selection preserves
that metadata. Missing occurrence transforms use the existing identity default,
for both face placement and edge finishing.

Native regression checks build 0.5 mm fillets/chamfers on both extrusion caps
of the XZ Simple Bracket. They verify closed oriented bodies, STEP reimport,
volume agreement within the mass evaluator's reported error bounds, one
cylindrical fillet surface, split-edge refusal and the localized Box route.
All 3,516 Firmament tests pass, including the combined-history refusal.
All 19 focused selection / Box / recess regressions pass. All 251 Helios unit tests
and six SDK selection tests pass.

Both new bracket UI workflows pass in the default renderer and forced WebGL:
select a real edge, preview, refuse zero size, cancel, apply, save source and
STEP, validate via the native CLI at Built level, undo, then redo. Artifacts
are under `HeliosCAD/artifacts/local/graph-authoring/bracket-single-edge-*`.
Logs are under each repository's `artifacts/local/single-edge-ui/` directory.

Published baselines are Helios `7c3ed3472f71647c5220cdcef00ce1756a8c8e21` and
Aetheris `575e7ec22ff716ebaf503e16f6b53589b9a5f29f`. This change remains local to
the owning repositories; the Site publishes corresponding complete patches.

## Concrete remaining kernel blocker

The already-holed oriented bracket is not admitted by the existing geometry
route. Adding `EdgeFinish EdgeFillet { Face: Top Target:
section.Outer.Vertex2_Vertex3 Kind: Fillet Radius: 0.5mm }` to the picked-face
hole fixture fails at ParsedNotBuilt with
`compose-oriented-feature-not-admitted:EdgeFinish:EdgeFillet`, explaining that
the feature needs another support adapter. The reproduction is
`artifacts/local/single-edge-ui/bracket-hole-plus-edge.firmament` and its JSON
validation output. No fallback removes the hole or silently finishes a rim.
This UI change removes the selection/transaction blocker; combining a finish
with these holes still requires a permanent kernel adapter and qualification
of cavity clearance and trimmed-face preservation. Generalizing that geometry
pipeline is beyond this bounded UI wiring change.

Production qualification also exposed a form-loading race: Preview was enabled
and two competing status messages appeared while field controls were still
being prepared. The form now presents one status and enables Preview only when
those controls are ready. Box before/after checks exposed an existing stock-frame
bug: the localized AIR body was exported in its centered kernel frame, shifting
authored Box stock down by half its height. Both single-edge routes now use the
existing FirmamentStockFrame authority and primitive translation helper before
preflight and export. The source Box remains at Z=0..Height through the finish.
Native tests compare the original and finished bounds; the CLI tests assert the
actual chamfer/fillet contact points in that authored frame. All 19 targeted CLI
pipeline tests pass, including the existing junction/trihedral checks. The final
full runs pass all 3,516 Firmament tests and all 760 CLI tests after this fix.

## Production qualification

The final production AOT SDK and frontend builds pass. The decoded native WASM
is 60,394,592 bytes, SHA-256
`fad6d0906b82aec76a5df2ee3828f864534d387fc29dede77aa5ce22405432b8`.

All 22 production graph workflows pass with the optimized AOT runtime required.
They cover the existing hole/recess authoring paths and the six new single-edge
workflows: bracket fillet/chamfer, Box fillet/chamfer with unchanged before/after
stock bounds, disabled longitudinal-edge actions, and refusal of an edge finish
on the already-holed oriented bracket with source preservation and explanation.

All six single-edge workflows also pass against the production bundle with
forced WebGL. The final Helios unit run passes all 251 tests. Both repositories
pass `git diff --check`.
