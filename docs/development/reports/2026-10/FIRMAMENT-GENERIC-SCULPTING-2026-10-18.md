# Native exact face edits and explicit subdivision sculpting

## Scope and result

This pass provides a small working foundation for two distinct artifact types:
exact native-body face grafts, and explicitly converted editable subdivision
meshes. A failed exact operation never silently switches to a mesh. A mesh
successor never pretends to support STEP. **Success for the bounded foundation**:
the real CLI, serialized replay, exact STEP graft and both mesh exporters work,
with the qualification and remaining boundaries recorded below.

Source witnesses live in
[`fixtures/Regression/GenericSculpting`](../../../../fixtures/Regression/GenericSculpting/README.md).
Generated run evidence is ignored under `artifacts/local/generic-sculpting/`.
No canonical source or existing geometry golden is rewritten for this pass.

## Exact-edit ownership repair

The existing imported-face adapter contained a useful bounded support graft,
but coupled its successor to `HousingConstruction`, mounting-hole PMI and
housing interfaces. Engineering-source BodyStates already retained arbitrary
admitted native bodies and their source recipes; their edit owner could only
reject or reconstruct an unrelated housing.

`BoundedFaceGraft` now owns the shared geometry operation: retain topology,
vertices, edge curves, source face bindings and shell membership; replace one
support; reconstruct/validate pcurves; run export preflight. An edited support
invalidates cached Boolean-composition evidence, so that evidence is cleared.
The old imported-face API calls this same owner and retains its established
resolved-orientation convention and housing compatibility projection.

`EngineeringFaceRegionReplacer` provides a separate semantic adapter around
that shared geometry operation, without housing reconstruction. Its admitted
family is intentionally bounded:

- One native planar four-line face, no inner loop.
- One full-domain, non-rational bicubic Bezier patch.
- The original affine parameter grid is retained; controls displace outward
  along the original face normal. The selected material-side orientation is
  checked explicitly rather than inferring it from the misleading historical
  `ReversedOrientation` Boolean convention.
- Every patch side explicitly names its current `EdgeN` boundary, with retained
  endpoint/support geometry. The entire control hull fits the authored envelope.
- `mayModify`, current face selection and unaffected preservation contracts bind
  before emission. Neighboring supports, curves, vertices and topology remain
  unchanged. No nearest-face or bounding-box reconstruction is used.

The native source adopter exposes current face handles and authoritative face
associations. `inspect --json` also reports edge handles and endpoint positions.
After a graft, every alias covering the selected face is explicitly replaced by
the named SurfacePatch. A historical face alias cannot be reused as though the
old support were still current. The existing typed `ReplaceRegionOperation`
and ConstructionState serializer/replayer own the operation; there is no second
exact-edit history engine. File-based compilation retains an absolute source
path for subsequent relative-resource replay.

The exact witness starts with a 20 × 16 × 8 mm box. Its top center becomes
8.5625 mm after the graft. Tests verify that actual surface value, unchanged
neighbor supports and edge curves, retained topology, source correspondence,
explicit outward orientation, STEP reimport and identical STEP after typed
serialization/replay. It is not an unchanged box reported as a successful edit.

### Metadata admission

The source-level adapter rejects imported engineering sources, raw STEP PMI
without a complete editable projection, and edits to faces carrying PMI or an
assembly interface whose successor meaning is unqualified. Their predecessor
retains its complete geometry and metadata. Unaffected projected associations
are retained by exact face identity. It does not drop annotations, reuse housing
PMI, or claim that old dimensions remain valid on a changed face.

G1/G2 face grafts, arbitrary control nets, holes in the selected face and global
`NoSelfIntersection` proof remain explicit rejections. The historical imported
graft remains available through its own bounded C# contract; this pass does not
claim unrestricted imported-product editing.

## Explicit mesh representation

`MeshState` is owned by the existing Sculpting frontend and existing unified
AST/field-consumption infrastructure. Conversion takes an exact BodyState or
SculptState. A successor takes a MeshState. Mesh states never enter the BRep
state dictionary, and no exact operation implicitly converts them back.

The editable authority is a welded quad cage with stable vertex/face identities,
sharp edges and derivation provenance. It is not a smoothed copy of display
triangles. The initial constructors are:

| Constructor | Admission and conversion evidence |
| --- | --- |
| `Box` | Eight actual axis-aligned stock corners, six planar faces, one proven source plane per cage face. Original sharp stock edges are marked. The native cage retains the exact box boundary; output quantization must fit the budget. |
| `Sphere` | A common exact spherical support. A welded cube-sphere lattice places controls on that sphere. A conservative finite triangle-plane radial bound must fit the explicitly authored `maximumDeviation`. |

Segments are 1..16 per side. Sphere conversion is a finite approximation; its
budget is not a certificate for an infinite subdivision limit surface. Scaling
the resulting mesh produces an ellipsoid-like mesh, while the original exact
sphere remains unchanged. Arbitrary BRep retopology, imported sculpt cages and
automatic cage fitting are not implemented.

Successors contain exactly one typed operation: `Subdivide`, `Move`, `Scale` or
`Crease`. Subdivision implements finite Catmull-Clark face/edge/vertex rules,
including fixed corners, two-edge crease stencils and inherited crease child
edges. Existing vertex identities survive refinement; new points retain parent
identities, and child faces retain source-face derivation. Selected edits use
current handles, not ordering or proximity. Optional protected vertices must
retain their exact coordinates and identities.

There are hard budgets of 64 mesh states/operations, four refinement levels per
operation and 24,576 cage faces. Refinement checks its cumulative face budget
before allocation. Manifold admission checks edge incidence, connected vertex
fans, directed winding, enclosure, connectedness, finite range and nondegenerate
triangles. Move/Scale reject local face-triangle reversal relative to their
predecessor. These checks are not a global self-intersection certificate:
`selfIntersectionStatus` remains `NotCertified`.

`MeshConstruction` retains the existing exact ConstructionState as its source
authority and typed mesh operations under `aetheris.mesh-sculpt.v1`. Persistence
reuses the exact serializer, with a closed mesh envelope and operation
discriminators. Replay realizes the exact source through its existing owner,
rebuilds the cage and applies the typed operations. Cage snapshots are evidence,
not executable replay authority. No source macro or renderer-only deformation
path was introduced.

## Validation, exports and host boundaries

The shared `PrepareExport` owner preflights the selected mesh output for the
current 32-bit GLB/STL boundary. Precision overflow, topology collapse or
quantization beyond the inherited `maximumDeviation` budget rejects
in both validation and build. This check lives in the compiler/export owner,
not a CLI-specific validator. Sphere conversion also includes encoded-coordinate
radial deviation in its conversion bound. Explicit deformations are not required
to remain close to the original source body; the inherited export budget bounds
precision loss relative to the requested deformed cage. A valid mesh reports:

```text
validationLevel: MeshBuilt
artifactKind: SubdivisionMesh
stepExportAvailable: false
selfIntersectionStatus: NotCertified
```

GLB reuses `AssemblyGlbExporter` with the existing mm/Z-up → m/Y-up root
projection. It exports finite, flat-shaded triangles so sharp boundaries remain
visible. STL reuses `BinaryStlExporter`. Both have a `.sculpt.json` sidecar with
the cage, current edge handles, source state identity, replayable construction,
conversion evidence and float-quantization report. STL itself carries none of
those semantics. PMI/interfaces stay attached to the exact source; mesh
derivation is not an assertion that engineering dimensions apply to deformation.

`mesh --format stl|glb` and `build --out <file.stl|file.glb>` execute this path.
A default STEP build rejects a mesh-only output. The retained exact state can
be selected explicitly for STEP. STEP-based `view` and `verify` reject mesh
outputs with a useful format diagnostic. `inspect` exposes the new states;
editor analysis retains the existing compiler-context notice and does not
materialize meshes on keystrokes. Interactive Helios integration is not claimed.

## Qualification

The frozen-code Release build, focused suites and full regression gates are
run serially with .NET SDK 10.0.401, node reuse/shared compilation disabled and
process-local `DOTNET_PROCESSOR_COUNT=2`. No test category or deadline changes.

| Gate | Result |
| --- | --- |
| Release solution build | 0 errors; 30 existing dependency/nullable/WASM warnings |
| Focused generic/existing body-state/imported-face tests | 37 passed |
| Focused CLI tests | 15 passed |
| Fast Core lane (`Category!=SlowCorpus`) | 1,076 passed |
| Complete solution suite | 5,586 passed, 0 failed, 0 skipped; 20 nonempty test assemblies |
| Recent Structural/Sculpting/BodyState/PlasticShell STEP parity | 30/30 byte-identical to the prior qualified artifacts; baseline artifact SHA verified before comparison |
| Historical frontend geometry gate | 37/37 matched: 35 byte-identical STEP files and 2 expected rejections |
| Canonical owner/consumer gate | 369 records: 296 owner operations, 69 consumed modules, 4 expected external-body boundaries |
| Claude FrontendX4 | 32/32, including unchanged Chimera baseline geometry |
| SheetMetalSyntax | 26/26; formed STEP / flat parity true |
| SheetMetalSemantics | 29/29 |
| New source witnesses | All three validate/inspect; exact face builds and reimports through CLI analyze; both cage sources export actual GLB and STL with provenance sidecars |

The earlier complete run passed before the final precision-budget regression
was added; it is retained as `full-before-precision-budget.log`, not substituted
for the final gate.

The final complete run includes 1,209 Core, 2,905 Firmament, 600 CLI and 150
Modules tests. FrictionLab advertises no tests and is not counted as a passed
test assembly. Canonical qualification exercises the actual domain owner; an
assembly/scene inspection is not an independent mesh or STEP-hash comparison.
The recent 30-artifact and historical 37-record gates provide the explicit
byte-parity evidence. Their source fixtures and recorded expected hashes were
not changed. This is local qualification, not remote CI or interactive Helios
certification. Frozen Zig was not run because no legacy Zig compiler or build
infrastructure changed.

| New output | Vertices | Triangles | Artifact checks |
| --- | ---: | ---: | --- |
| Box cage, refined and edited | 98 | 192 | GLB and STL; connected, outward, watertight |
| Sphere cage, refined and scaled | 386 | 768 | GLB and STL; connected, outward, watertight |
| Exact face graft STEP | 8 | n/a | Reimport: 1 body, 1 shell, 6 faces, 12 edges; top-center value 8.5625 mm asserted independently in tests |

CLI tests parse the emitted GLB header/accessors/bounds and reconstruct the STL
triangles from its binary coordinates. They replay the actual persisted sidecar
construction and check state/geometry identity. Negative cases assert both
validate/build rejection and absence of artifacts. Protected vertices, stale
edges, unknown fields/statements, local foldover, cumulative refinement limits,
float overflow and excessive finite quantization all have regression evidence.
The original 20 × 16 × 8 mm box is retained unchanged; its sharp-cage refinement
retains volume 2,560 mm³. Removing creases and moving a selected vertex produces
a demonstrably different finite mesh.

Reproduce from the repository root using the freshly built CLI:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Regression/GenericSculpting/exact-face.firmament --out artifacts/local/generic-sculpting/exact-face.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll mesh fixtures/Regression/GenericSculpting/box-cage.firmament --format glb --output artifacts/local/generic-sculpting/box-cage.glb --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll mesh fixtures/Regression/GenericSculpting/sphere-cage.firmament --format stl --output artifacts/local/generic-sculpting/sphere-cage.stl --json
```

Final evidence includes `release-build.log`, `focused.log`, `cli-focused.log`,
`fast.log`, `full.log`, `test-summary.json`, `domain-parity.json`,
`historical/comparison.json`, `canonical/qualification.json`, the authoring-corpus
logs, `mesh-summary.json` and the actual new artifacts/sidecars. The local
`qualify-corpus.ps1` records the serial corpus commands and baseline comparisons.
All task .NET hosts exited after qualification; no background service, scheduled
task or persistent compiler host was created.

## Code-quality review and remaining work

Review covered representation boundaries, source ownership, closed field and
statement consumption, exact predecessor retention, material-side orientation,
PMI refusal, stale handles, protection contracts, topology, typed persistence,
finite allocation budgets, output precision, artifact paths and runtime hosts.
Whitespace formatting was limited to this pass's new C# files. No new regex
dispatch, source rewrite, diagnostic allowlist, parallel parser, background
service or runtime state-machine framework was added. Cage kinds and operations
are explicit deterministic choices, so ordinary dispatch does not use Judgment.

The next exact-edit prerequisite is complete editable PMI/import metadata
projection and qualification of additional face/support families. The next mesh
extension can add deliberately authored cages or another qualified constructor;
automatic arbitrary-BRep retopology needs a separate design and proof. General
global self-intersection certification and new editor tools are separate work.
None is silently substituted into this bounded pass.

No user decision is needed to use this foundation. The pass does not declare
general imported BRep editing, arbitrary retopology, complete mesh/PMI projection
or broader X2 closure.
