# P4-01A2 — Display transport X0

**Verdict: Success for Scene and resolved-material transport in both products.**

The remaining artifact/edge qualifications described in this A2 snapshot are
superseded by [P4-01A3](P4-01A3-CIR-SHADER-BINDING-CLOSEOUT.md), which binds normal
product Auto fields and carries Assembly BRep edges. The original transport
evidence and timings below remain historical.

Normal Cadmata and Helios loading now reaches the existing Scene compiler and
renders its owned environment, placements, materials and authored cameras.
Before this change, both products refused the house Scene; the guitar's 99
authored appearance bindings were lost before Cadmata rendering.

## Owners and transport

`DisplayProjection` projects already compiled Scene and already resolved
Appearance values. It reuses `SceneExport.Project` for environment geometry and
validated hidden-boundary filtering. Output carries existing definition and
occurrence identities, world transforms, millimetre bounds, boundary metadata,
named camera poses/look-at/FOV, and resolved colour, roughness, metallic, opacity
and emissive values. Assembly display uses the same material conversion. Neither
UI interprets Appearance source or reconstructs Rooms, openings or glazing.

Definitions retain optional CIR admission and immutable geometry revisions;
occurrences retain placement and resolved material override. Telos consumes
material through its existing adapter and retains geometry buffers when
definition ID and geometry revision agree across new transport arrays. The
native retained-Scene test proves that Room-finish, placement and camera edits
rebuild zero engineering definitions and preserve geometry revisions. This is
not a measured product GPU reuse claim for those edits.

The SDK adds `projectDocuments` for Scene dependencies to compile/session source updates and accepts
the existing Scene display schema. The Web runtime invokes
`FirmamentSceneSession.CompileProject`; explicit immutable project resources
are the only dependency source for that path. Missing resources produce
`scene-assembly-file-missing`, even if a matching native file exists. Scene tree
source references use compiler-owned spans in supplied documents. Scene has no
synthetic STEP export; the runtime reports `step-export-unavailable`.

Cadmata's existing display endpoint dispatches Part, Assembly and Scene source.
Its canonical `display` payload comes from shared compiler/exporter output.
Legacy face-patch transport remains for compatibility, derived from that same
geometry rather than a second tessellation. External STEP definition provenance
selects the existing bounded mesh route. Existing unmeshed imported faces retain
zero-triangle ranges and explicit per-definition diagnostics; they are never
filled with invented geometry. Authored geometry still fails on invalid patches.
Imported STEP topology, semantic and PMI services remain their existing owners.

Helios carries each queued revision's matching resource snapshot and offers a
multi-file loader plus root-document selection. The loader currently uses unique
sibling filenames; the SDK supports project-relative paths. This is not a full
dependency editor or directory-import workflow. Both products offer authored
Scene cameras and an explicit Fit view option.

## Actual product evidence

Edge/WebGPU at DPR 2 loaded normal sources through product APIs and file input.
No constructed display packet was injected. Screenshots were visually inspected.

| Product / witness | Definitions / occurrences | Resolved materials | Cameras | Observed result |
|---|---:|---:|---:|---|
| Cadmata cylinder | 1 / 1 | 0 | 0 | Part loading works; qualified CIR metadata, explicit mesh fallback |
| Cadmata guitar | 56 / 171 | 99 | 0 | Authored assembly colours reach the viewport |
| Cadmata house | 94 / 254 | 161, including 3 translucent | 3 | Room/environment, furniture and glazing render; Hero camera works |
| Cadmata factory | 124 / 2,562 | 1,492, including 2 translucent | 5 | Factory environment and authored equipment palette render |
| Cadmata robot | 24 / 64 | 0 | 0 | Existing Assembly renders; source has no authored appearance bindings |
| Helios cylinder | 1 / 1 | Default | 0 | Worker build, zero diagnostics, mesh rendering |
| Helios house | 94 / 254 | Authored palette visible | 3 | Multi-file Worker compile, zero diagnostics, Hero interior view |

The final Helios development/non-AOT house build took 52.6 seconds.
This is not release-performance acceptance. Native retained composition and
browser Worker execution are separate measurements.

Opacity uses bounded mesh blending: opaque meshes draw first, translucent meshes
draw after fields with depth writes disabled and object-level back-to-front
ordering. This covers the current simple panes; it does not qualify OIT,
intersecting transparency, refraction or advanced physical glass.

## Limits and reproduction

Qualified CIR metadata has no production-bound WGSL artifact yet. Automatic
field dispatch, product CIR picking proxies and complete Assembly BRep-edge
transport remain unqualified. Emissive values are transported, but the existing
mesh shader does not apply them; emission appearance is not qualified. The shared
host remains the renderer; this
milestone does not expand rendering research or repair P4-01B threaded faces.

See the [P4-01A rerun](P4-01A-DISPLAY-PROJECTION-CLOSEOUT.md) for gates, commands,
captures and the exact next artifact-binding boundary. Raw output stays under
ignored `artifacts/local/display-projection-closeout/`.
