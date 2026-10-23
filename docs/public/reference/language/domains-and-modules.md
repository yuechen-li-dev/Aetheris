# Domains and modules

Firmament covers several engineering domains with one syntax. Each domain has its
own compiler front end and its own rules about what is admitted. Sharing syntax
does not make every operation legal everywhere: a field or statement a domain
does not own is rejected there.

## Choosing a domain: `schema`

An optional `schema Name` header selects a front end explicitly. Write it once,
before any other directive or declaration, preferably on its own line. Comments
before or after it are fine. Names are case-sensitive. An unknown name, a second
or late header, or source that does not match the selected domain rejects, and
the compiler does not retry with another front end. A Template specialization
stays in the domain it was admitted to.

| Schema | Guide |
| --- | --- |
| `Mechanical` | [Geometry](../../firmament/geometry.md), [Revolve](../../firmament/revolve.md), [gears](../../firmament/gears.md). Revolve is an operation here, not a schema. |
| `WireForm` | [Formed wire](../../firmament/wire-form.md) |
| `Sweep` | [Circular-section sweeps](../../firmament/sweep.md) |
| `SectionChain` | [Framed section chains](../../firmament/section-chains.md) |
| `Piping` | [Ports, connections and routing](../../firmament/piping.md) |
| `Structural` | [Nodes, straight members and weldments](../../firmament/structural.md) |
| `Sculpting` | [Immutable body and surfacing states](../../firmament/sculpting.md) |
| `PlasticShell` | [Molded shell manufacturing intent](../../firmament/plastic-shell.md) |
| `Analysis` | [Linear-elastic analysis](../../firmament/fea.md) |
| `Garment` | [Panels and stitches](../../experimental/garments/garments.md) (experimental) |

Assembly, Scene, SheetMetal and Drawing are not schemas; each has its own entry
form, described in its guide. Piping uses its documented Model/PipingSystem form,
with `schema Piping` optional. Do not invent `schema Assembly`, and do not wrap
another domain's declarations in a Model.

| Task | Start here |
| --- | --- |
| Parts, ports, constraints, occurrences | [Assemblies](../../firmament/assemblies.md), [components and sites](../../firmament/assembly-components-and-sites.md) |
| Spatial layout and presentation | [Scenes](../../firmament/scenes.md) |
| Sheet parts, regions and bends | [Sheet metal](../../firmament/sheet-metal.md) |
| Engineering annotation | [PMI](../../firmament/pmi.md) |
| Body states and bounded surface edits | [Sculpting](../../firmament/sculpting.md) |
| Multi-file projects | [Project snapshots](../../firmament/project-snapshots.md), [Inventory](../../firmament/inventory.md) |

## Body states

### BodyState: keep an engineering body

`BodyState` can reference the Model's admitted engineering body. It does not
convert that geometry into the old rectangular housing prototype.

**Complete file:**

```firmament
Model Sleeve {
  units: mm
  Cylinder stock { radius: 6mm height: 17mm }
  BodyState base { input: stock }
  output: base
}
```

The ordinary engineering compiler admits `stock`, including its modifications
and explicit PMI. The state retains that exact product, semantic correspondence
where available, and its source recipe for replay. `input` must name an admitted
body or final Compose; it cannot select an unrelated Record, Profile guide or
an intermediate stage of the same construction. Use separate Parts for separate
products, as in ordinary mechanical authoring. Primitive, profile/Compose,
Gear, Sweep and Revolve products retain their existing engineering lowerers.
When adding a state output to a file with an explicit `schema Mechanical`,
select `schema Sculpting` for the state-owning document; an explicit conflicting
selector is still an error.

The older `BodyState base { size: [...] Hole ... }` form remains a housing
construction. Holes are optional. Its named crown and mounting regions belong
to that construction; they are not inferred on arbitrary engineering bodies.
The existing surface-edit materializers still have explicit topology/support
requirements. An unsupported edit or history combination rejects before it can
replace earlier geometry. See the [operation boundaries](../../firmament/sculpting.md).

### Exact face edits and mesh sculpting

`ReplaceRegion` grafts a bounded patch onto an engineering-source
BodyState. Inspect the state for current `FaceN` and `EdgeN` handles, then bind
every patch side explicitly. The supported case is an outward, non-rational
bicubic Bezier graph over a planar four-line face. It retains all topology,
edge curves, vertices and neighboring supports. A replaced face handle becomes
stale; its named SurfacePatch is the successor. Raw STEP PMI that lacks a complete
editable projection rejects before editing. See the complete
[exact-face example](../../../../fixtures/Regression/GenericSculpting/exact-face.firmament).
Imported engineering sources and edits to PMI/interface-bearing faces remain
outside this native adapter's qualified contract.

Use `MeshState` for explicit conversion to an editable subdivision control cage.
It produces a mesh artifact, not an exact BodyState. There is no automatic
mesh fallback when an exact edit fails.

**Complete file — convert a box, soften its creases and edit its cage:**

```firmament
Model CageSculpt {
  units: mm
  Box stock { size: [20mm, 16mm, 8mm] }
  BodyState exact { input: stock }
  MeshState cage {
    input: exact
    cage: Box
    segments: 1
    maximumDeviation: 0.01mm
  }
  MeshState smooth {
    input: cage
    Crease { edges: [All]; mode: Smooth }
  }
  MeshState refined { input: smooth; Subdivide { levels: 2 } }
  MeshState edited {
    input: refined
    preserve: [v1]
    Move { vertices: [v0]; offset: [0mm, 0mm, 1mm] }
  }
  output: edited
}
```

`cage: Box` requires a pure axis-aligned box, and starts with sharp stock edges.
`cage: Sphere` requires a pure exact spherical support; it creates a welded
cube-sphere quad cage. `segments` is 1..16 per cube side (default 1 for Box,
4 for Sphere). Required `maximumDeviation` bounds the finite conversion's
radial error for Sphere, including output quantization; the native Box cage
retains its exact boundary. The same budget limits 32-bit coordinate
quantization for every selected mesh successor. It does not constrain an
explicit Move/Scale to stay near the original body. A tighter
sphere budget may require more segments. This is not arbitrary BRep retopology
or a certificate for the infinite subdivision limit surface.

Each successor has exactly one operation:

| Operation | Fields | Meaning |
| --- | --- | --- |
| `Subdivide` | `levels: 1` | Finite Catmull-Clark refinement, 1..4 levels per operation |
| `Move` | `vertices: [v0]`, `offset: [0mm, 0mm, 1mm]` | Move selected current cage vertices in world space |
| `Scale` | `vertices: [All]`, `factor: [1.5, 1, 0.75]`, `origin: [0mm, 0mm, 0mm]` | Explicit positive scale about a required origin; factors are dimensionless |
| `Crease` | `edges: [All]`, `mode: Sharp` or `Smooth` | Mark or unmark current cage edges as sharp |

Use `inspect --json` for current vertex/edge identities; edge handles are
derived from their endpoint identities. `[All]` is an explicit whole-cage
selection. Existing vertex IDs survive refinement; new edge/face points have
parent-derived IDs. Old edge handles are replaced by their child edges.
Optional `preserve: [v1]` requires those input vertices to retain their exact
coordinates and identities in that successor. A conflict rejects atomically.
There are at most 64 mesh states/operations and 24,576 cage faces.

The [sphere example](../../../../fixtures/Regression/GenericSculpting/sphere-cage.firmament)
uses `Scale` to create an ellipsoid-like subdivision mesh. Its original sphere
remains an unchanged exact BodyState. Mesh states carry their source body,
conversion evidence, typed construction recipe and edit history. Finite meshes
are checked for enclosure, directed winding and local edit foldover. Global
self-intersection is **not certified**; unsupported requirements are errors.

Export a mesh output with `aetheris mesh part.firmament --format glb` or
`--format stl`, or `build --out artifacts/local/part.glb`. GLB is binary glTF 2.0.
Each output has a `.sculpt.json` sidecar with the cage and replayable construction.
STL does not embed those semantics, so retain its sidecar. GLB uses the existing
millimeter/Z-up to meter/Y-up projection and finite, flat-shaded triangles.
Both formats use 32-bit coordinates; export reports quantization and rejects
precision collapse. `validate` reports `MeshBuilt`, `SubdivisionMesh` and
`stepExportAvailable: false`. A normal STEP build of that output rejects;
select the retained exact state as `output` when STEP is required. The existing
STEP-based `view`/`verify` commands explicitly reject mesh-only outputs.

## Modules: `include` and `Inventory`

Use `include bolt: Part = Step("vendor/bolt");` for a short typed resource
reference, or `include "parts/bolt";` when shared declarations are convenient. For
larger compositions, give Part, Subassembly, Assembly and Scene dependencies typed
names in an Inventory Record:

```firmament
Inventory Record Components {
  bracket: Part = Firmament("parts/bracket").Bracket
  drive: Subassembly = Firmament("assemblies/drive").Drive
  bearing: Part = Step("vendor/bearing")
  gearbox: Assembly = Step("vendor/gearbox")
}
```

This fragment requires the named resources. Locators normally omit the extension;
ambiguous matches reject. Place `Components.bracket` through a Part occurrence,
`Components.drive` through an Assembly occurrence, or a Scene entry through a
Scene occurrence. The catalog makes definitions available; occurrences determine
quantity and placement. Part and Subassembly entries may reference typed Template exports; supply their
named arguments at the occurrence. Definition-private names remain in the defining
module. Assembly and Scene entries currently import concrete exports.

Inventory follows lexical scope and retains imported file identity. Existing
untyped `include` remains the concise declaration-composition form. A typed
include follows the same reference rules as an Inventory member. STEP Assembly
imports retain product hierarchy and transforms; no implicit flattening occurs.
Optional `metadata:` selects an ordinary typed Static Record. An optional generated
`firmament.lock.toml` pins dependencies; existing locks are enforced without
automatic refresh. `InlineStep { from: Components.housing }` binds one STEP Part
for local annotation or surgery, with a lock-supplied or explicit source pin.
Use `geometryOnly: true` explicitly when excluding imported PMI. See
[Inventory Records](../../firmament/inventory.md) for complete projects, source snapshots, STEP
boundaries and extension resolution.

Assembly occurrence trees use `<Assembly>` / `<Part>` tags as well as brace
bodies. Tree nesting does not create a Mate. Inclusion is owned by the consuming
project/domain; `include` is **not** a universal mechanical preprocessor.

Standalone part compilation rejects an unresolved include explicitly, even if
the named file exists. Use a documented Assembly/project loader to resolve file
dependencies before part compilation. The shipped `Use Profile.Modifications`
import retains its existing library owner. Other unconsumed directives reject.

