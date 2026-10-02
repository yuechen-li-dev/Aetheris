# Static GLB presentation export

Aetheris exports GLB from the same compiled assembly display meshes used by USD.
BRep and STEP retain engineering authority. GLB carries downstream presentation
geometry for web, DCC, game assets, and presentations.

```powershell
dotnet run --project Aetheris.CLI -c Release -- asm export-glb `
  fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament `
  artifacts/local/presentation-3d-x0/atlas-display.glb `
  --materials fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/appearance.json `
  --state Shoulder=-75 --state Elbow=-140 --state GripLeft=10 --state GripRight=10 --json
```

The output defaults to ignored `artifacts/local/glb/`. `.firmasm` assemblies are
also supported. Definitions become shared indexed triangle meshes. Occurrences
retain their hierarchy and rigid local transforms. Separate face normals retain
sharp features. One root transform converts right-handed Z-up millimetres to
right-handed Y-up metres: `(x,y,z) → .001 × (x,z,-y)`.

Appearance JSON maps exact definition identities to `red`, `green`, `blue`,
`metallic`, and `roughness` values in `[0,1]`. RGB factors are linear, as in the
existing USD preview mapping. Optional `baseColorTexture` names a PNG/JPEG relative
to the appearance file, and `textureCoordinates` supplies two values per display
vertex in glTF UV convention. Images are physically embedded in GLB, so moving the
asset requires no external files. UV seams must already be represented by separate
display vertices. Invalid units, frames, normals, indices, hierarchy, materials,
and texture bindings fail explicitly.

The polished X0 witnesses use their existing Aetheris-generated USD/Blender
presentation scenes. `scripts/export-presentation-scene.py` preserves product
positions and polygons, embeds the ATLAS maker label, and bakes the guitar's
existing procedural sunburst/wood base color. It does not translate general
material graphs or alter guitar geometry. Procedural grain bump is omitted.

ATLAS is an independent Aetheris demo robot. Aetheris is not affiliated with,
endorsed by, or associated with Boston Dynamics. The shared name is coincidental.

The existing C# slide writer supports `Model3D(Source, Preview, X, Y, Width,
Height, Camera, Name)` through `DrawingPptxWriter.WriteModel3DDeck`. Placement is
in millimetres; `Model3DView` specifies the glTF centre in metres, presentation
scale, Euler rotation and camera distance/field of view. Each object requires a
PNG preview. The compiler physically embeds both assets and emits the Office
3D object with a static fallback.

```powershell
dotnet run --project Aetheris.CLI -c Release -- presentation compile `
  fixtures/Canonical/Presentation3D/interactive-3d-demo.json `
  artifacts/local/presentation-3d-x0/interactive-3d-demo.pptx --json
```

Paths in the JSON authoring input resolve relative to that input. After
compilation, the PPTX needs no external model or image files. Desktop PowerPoint
rotation and save/reopen qualification is separate from package/schema checks.

See [PRESENTATION-3D-X0](../../release/PRESENTATION-3D-X0.md) for artifact and
PowerPoint qualification status. No animation, glTF import, compression, or
game-engine integration is included.
