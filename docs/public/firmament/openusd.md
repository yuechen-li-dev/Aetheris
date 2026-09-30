# Export assemblies to OpenUSD

Aetheris exports executable Firmament assemblies as readable USDA through the same shared-definition production display mesh used by its assembly viewer. STEP/BRep and Firmament Interface semantics remain engineering authority. USD is a downstream scene and kinematic visualization output; STEP remains the manufacturing output.

```powershell
dotnet run --project Aetheris.CLI -c Release -- asm export-usd `
  fixtures/Canonical/AssemblyInterfaces/two-link-arm-usd.firmament `
  artifacts/local/usd/arm.usda --state Shoulder=35 --state Elbow=65
```

The first argument is an executable `.firmament` or `.firmasm` assembly. The optional second argument is the `.usda` output; its default is `artifacts/local/usd/<source-name>.usda`. A semantics-only assembly must supply actual materializable part definitions before exporting. No USD SDK is required to write USDA.

`--state Joint=value` accepts finite degrees for Revolute and millimetres for Prismatic; omitted state is the compiler's default. Unknown names and duplicate state assignments fail. `--sample time:Joint=value,Joint=value` adds an evaluated pose at that USD time code; omitted joints use their defaults in each sample. Samples use 24 time codes per second. USD interpolates translations and quaternions between samples. Use dense samples when intermediate joint-constrained motion matters: interpolation of occurrence poses is not a substitute for the Aetheris kinematics evaluator.

```powershell
dotnet run --project Aetheris.CLI -c Release -- asm export-usd `
  fixtures/Canonical/AssemblyInterfaces/two-link-arm-usd.firmament `
  artifacts/local/usd/arm.usda `
  --sample 0:Shoulder=0,Elbow=0 `
  --sample 48:Shoulder=35,Elbow=65 `
  --sample 96:Shoulder=-20,Elbow=35
```

`--materials file.json` supplies downstream preview appearances keyed by the exact compiled definition identity. Each appearance has `red`, `green`, `blue`, `metallic`, and `roughness` in `[0,1]`. Unknown definitions fail. The preset is visual presentation, not an engineering material or density declaration. Without a preset, parts receive a neutral metallic preview material and display color. `--evidence file.json` writes the authoritative display data, poses, and joint frames for independent external verification. `--json` prints counts, file size, and measured mesh/serialization timings.

Analytic formed-wire definitions containing planes, cylinders and torus bends use the existing shared-boundary SurfaceMeshIR route. Unsupported trims fail rather than silently substituting a disconnected display mesh.

The API is `AssemblyUsdExporter.Export(compilation, options)`. `Serialize(ir, mesh, options, evaluatedState)` reuses a prepared mesh; its occurrence transforms must match the declared state. The exporter rejects mismatched states, invalid meshes, unresolved placement, and reflected transforms. It never drops a failed part or replaces a failed mesh with debug geometry.

## Scene contract

| Aetheris | USD |
|---|---|
| Assembly root and nested product groups | Nested `Xform` prims under `/Assembly` |
| Shared part definition | One mesh in abstract `/Definitions`; native instanceable references on occurrence `Geometry` children |
| Occurrence world placement | Local translation and quaternion derived relative to its product parent |
| Millimetres | `metersPerUnit = 0.001`; coordinates retain mm |
| Coordinate convention | Z-up, right-handed; no axis swap |
| Sharp production face normals | Vertex normals on separated face patches; `subdivisionScheme = "none"` |
| Preview appearance | `UsdPreviewSurface`, `MaterialBindingAPI`, and `primvars:displayColor` |
| Fixed / Revolute / Prismatic | `PhysicsFixedJoint` / `PhysicsRevoluteJoint` / `PhysicsPrismaticJoint` |
| Accepted joint zero frames | `physics:localPos0/1`, `physics:localRot0/1`; motion axis `Z` |
| Posed state and identities | Namespaced `aetheris:*` attributes, plus optional pose/state time samples |

Joint endpoints receive `PhysicsRigidBodyAPI` with `physics:kinematicEnabled = true`; the assembly root receives `PhysicsArticulationRootAPI`. The exported scene presents Aetheris-authored motion. It does not introduce a dynamics solver, collision decomposition, IK, USD import, or robot control. The accepted assembly IR does not currently supply per-occurrence density, mass, or principal inertia, so the exporter does not invent those properties. Limits are omitted because the Interface X0 contract has no authored limits. Legacy axis/seat-only Revolute declarations lack a deterministic angular zero and do not gain state-driven joints from this export.

Metre authoring is deferred: changing `Units: mm` to `Units: m` alone would not convert typed dimensions, template values, PMI, and geometry parsers consistently. This release keeps the accepted millimetre engineering convention and explicitly declares its physical USD scale.

## External verification and demo

On Windows, the qualification script installs the pinned NVIDIA-provided OpenUSD 25.08 distribution locally if needed, builds Aetheris, exports the witnesses, runs external USD validation, and captures the real `usdview` window and Hydra viewport. It creates no background service and keeps generated output under `artifacts/local/`.

```powershell
pwsh -File scripts/qualify-usd-export-x0.ps1
# Optional: capture a four-second motion clip; requires ffmpeg on PATH.
pwsh -File scripts/qualify-usd-export-x0.ps1 -CaptureMotion
```

ATLAS uses exact Aetheris pedestal, perforated housings, shared collars, and a two-finger gripper. The separate studio USD layer adds lights, camera, stage, and a reflective sphere; those are presentation-only. See [the milestone evidence report](../../release/USD-EXPORT-X0.md) for the pinned tool, source provenance, screenshots, physical-scale checks, and limits.

The refined industrial ATLAS witness is authored in `fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament`. Its tapered G1 housings use `SectionChainFile` definitions, alongside ordinary exact drums, rings, rounded panels and parallel jaws. Native shared instances retain the repeated fasteners. The assembly display exporter has a bounded structured tessellation lane for natural rectangular spline patches with simple planar caps: adjacent faces share sampled BRep edges, and sharp cap normals remain separate. Convex caps retain the centroid fan; concave single-loop caps use the existing simple-polygon triangulator over the same edge samples. General trimmed spline remeshing and caps with holes are outside that lane. The [single-cut guitar witness](../demos/guitar-surfacing-x0.md) exercises concave caps, carved G1 surfaces, formed strings, STEP, USD, and Cycles presentation.

After the solution build and installation above, reproduce its external validation and slide render with:

```powershell
pwsh -File scripts/qualify-industrial-atlas.ps1 -Render -Motion
```

`-Render` and `-Motion` require Blender with Cycles; the script accepts `-Blender <executable>`. Motion encoding also needs ffmpeg. The default output is `artifacts/local/usd-industrial/`. Engineering solids are modeled in Aetheris and exported both as STEP and USD. Blender imports the actual USD and supplies the final studio render. The printed maker mark, floor, chrome target, camera and lights are downstream presentation; the mark is not BRep engraving. See [the industrial styling evidence](../../release/USD-INDUSTRIAL-ATLAS-X0.md).
