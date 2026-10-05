# Scenes

`Model` / Part describes an engineered object. `Assembly` describes objects and
their mechanical or semantic relationships. `Scene` describes independent
objects coexisting in a spatial environment. Scene placement does not create a
Mate or Interface. Engineering geometry still belongs to its part or assembly.

```firmament
Scene Factory {
  units: m;
  Room warehouse { size: [6m, 4m, 3m]; thickness: 100mm; }
  Door loadingBay { on: warehouse.southWall; width: 1.5m; height: 2.4m; along: .4m; }
  Window officeGlass { on: warehouse.eastWall; width: 1.5m; height: 1m; sill: 1.2m; along: 1m; }
  <Assembly robot = AssemblyFile<"robot.firmament">>
    Placement { from: Origin; to: World; translateLocal: [1m, 2m, 0mm]; }
  </Assembly>
  Camera hero { position: [5.6m, .25m, 2.65m]; lookAt: [2.5m, 2.1m, .8m]; fov: 65deg; }
}
```

`AssemblyFile` references an independently compiled `.firmament` Assembly source;
it requires file/project directory context. X0 does not merge its declarations
into the Scene or import its mechanical constraints into the Scene root.
Individual Part occurrences use ordinary typed Template specializations declared
before the Scene, for example `<Part package = Package<R:100mm,H:200mm>>`.
Those templates retain their existing **mm** geometry authoring contract.

## Units and room coordinates

`units: m` selects scene-scale authoring; `units: mm` also works. Every Scene
length and coordinate still requires an explicit `m` or `mm` suffix. Binding
normalizes `1m` to `1000mm`; changing `units` never rescales explicit literals.
Kernel geometry and tolerances remain in mm. Scene Set lengths are converted
at the existing typed pattern catalog boundary before occurrence expansion.

A Room occupies `[0,width] × [0,depth] × [0,height]`, optionally translated by
`at: [x,y,z]`. Its clear interior dimensions are `size`. Default enclosure
thickness is `100mm`, placed outside the clear interior. X0 rooms are rectangular
and axis-aligned; dimensions and thickness must be positive.

Every Room publishes case-sensitive `floor`, `ceiling`, `northWall`, `southWall`,
`eastWall`, and `westWall` references. Their exact boundary frames, dimensions and
owned openings are available to inspection. These are Scene boundary references,
independent of BRep face or triangle IDs. Wall coordinates use local X for
`along`, local Y for vertical height. Along increases in world +X on north/south
walls and world +Y on east/west walls, always measured from the minimum corner.
These right-handed frames are coordinate charts for `along` and height; their
third axis is not an inward/outward wall-normal classification.

Door bottom is the floor. Window bottom is its `sill`. Openings must fit inside
the selected wall and cannot overlap. They lower to actual missing wall volume
by a deterministic rectangular partition. No authored subtraction is required.
Windows are apertures in X0; glazing and door leaves are not synthesized.
The semantic hierarchy is Room → boundary → opening, even when the boundary's
display consists of multiple panels. Panel names are derived display structure;
the named boundary and opening retain authority.

## Placement, sharing and presentation

Scene `Placement` reuses the existing `translateLocal`, `rotateLocal`, `normal`,
`up`, and frame composition implementation. `to` may name `World` or a Room
boundary. `from: Origin` is available on Parts and Assemblies; Assemblies may
also name an exact published DatumFrame. Rotation retains the existing form
`rotateLocal: { axis: Z; angle: 90deg; }`. There are no authored matrices.
Named Scene `FrameTransform` declarations and Part source-port alignment are
deferred; Scene rejects unsupported declarations with a diagnostic.

The existing `Pattern name over keyedSet` frontend expands Parts and Assemblies;
X0 adds no Scene loop language. Pattern containers are spatial groups, not
engineered assemblies. Names, pattern keys, declaration spans, definition
identities and placement authority survive inspection and USD metadata.

Each unique AssemblyFile compiles once per build. Repeated occurrences share its
part display definitions. Local Part specializations use the existing immutable
STEP definition cache. `FirmamentSceneSession` keeps independent retained
Assembly sessions. Camera, placement and Scene Set edits do not invalidate
unchanged geometry declarations. Cache hits still reimport fresh exact bodies;
they do not expose cached mutable topology.

`Camera` is presentation state: a unit-bearing `position` and `lookAt`, plus a
vertical `fov` in degrees. Export uses Z-up camera orientation. Cameras never
modify engineering geometry. Scene `Appearance` definitions and occurrence
`appearance` overrides reuse the existing finite preview material values.
Physical material/density selection remains in engineering definitions.

## CLI and exports

```powershell
aetheris validate fixtures/Canonical/Scene/warehouse.firmament --json
aetheris inspect fixtures/Canonical/Scene/warehouse.firmament --repeat 2 --json
aetheris scene export-usd fixtures/Canonical/Scene/warehouse.firmament --json
aetheris scene export-glb fixtures/Canonical/Scene/warehouse.firmament --json
```

Default output is ignored `artifacts/local/scene/`. USD preserves spatial
hierarchy, shared instances, transforms, named opening metadata, cameras and
appearance overrides. GLB uses the existing display exporter and the established
mm/Z-up to m/Y-up boundary; material variants share geometry buffers. GLB is a
static presentation asset. Assembly kinematics and STEP authority remain with
the source assembly; X0 Scene USD does not export a combined physics articulation.

The existing PowerPoint 3D pipeline can embed the generated GLB and a rendered
fallback PNG. Desktop rotation/save/reopen is a separate manual qualification.
Whole-scene manufacturing STEP is deliberately unsupported.

Completion, hover and source diagnostics use compiler-owned schemas. Scene
formatting safely prefers camelCase field labels while preserving layout,
comments, generic specialization spelling and authored identities. It is
deterministic and idempotent; it does not rename public ports or pattern keys.
Project-snapshot language analysis reads Scene syntax; compiling AssemblyFile
resources from a virtual/browser project is deferred.

Nested Scenes, BIM/IFC, general walls, zones, lights, logistics, collision
avoidance, path planning, physics and robotics control are outside X0.
