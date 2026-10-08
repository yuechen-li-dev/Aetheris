# Industrial ATLAS: reusable components and articulation

The canonical model is `fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament`.
Its root now assembles reusable base, upper arm, forearm, and jaw components through
published mounting frames. Joint covers and cover fasteners own their complete
local construction; keyed mounting sites instantiate them. The housing SectionChain
geometry remains unchanged. Materials and appearances are authored in the source.
Assembly GLB export now forwards those occurrence appearances to the existing
serializer, matching USD and the interactive display instead of emitting default grey.

The model retains 60 visible bodies and 24 shared geometry definitions. Five
top-level interfaces replace the former 59 individually seated relationships:
Shoulder, Elbow, WristMount, GripLeft, and GripRight. The wrist is fixed; the other
four interfaces admit motion. Gripper body and jaw components are direct articulated
occurrences, avoiding an unplaced organizational container.

## Corrected construction defects

| Feature | Previous construction | Current construction |
| --- | --- | --- |
| Cover standoffs | 15 mm shafts extended 3 mm into the cover plates | 12 mm shafts meet the underside; heads meet the outer face |
| Jaw roots | Began at X=13 mm, 17 mm inside the 30 mm body | Begin at the body's X=30 mm front face |
| Grip pads | Extruded upward from the jaws | Rotated inward and seated on the opposing jaw faces |

At zero jaw travel, the modeled pads have a 21 mm gap. Positive GripLeft and
GripRight states open their respective jaws outward. Pose evaluation changes
occurrence frames while reusing the same geometry definitions; the pads retain
their local relationship to the jaws.

`IndustrialAtlasModernizationTests` checks transformed native display bounds for
these contacts and clearances, and checks rigid child frames and jaw-to-body
relationships in two articulated poses. USD and GLB tests also exercise the real
display/export paths. Published frame origins use dimension-checked length
arithmetic, including `H / 2`, rather than duplicated height literals.

## Inspect and export

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm export-glb fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament artifacts/local/atlas.glb --state Shoulder=-65 --state Elbow=100 --state GripLeft=4 --state GripRight=4 --json
```

Helios copies these canonical modules with source hashes and generates its ATLAS
SVG from the native placed topology edges at that same presentation pose.

This is a nominal presentation assembly, not a manufacturing or robotics release.
The assertions qualify the specific cover and gripper repairs; they do not establish
global collision freedom. Curved drive/housing internals remain simplified. The
current exact solid-interference checker admits convex planar bodies and cannot
qualify all of this model's curved geometry.

Current native qualification: Release solution build succeeded; the fast core lane
passed 1,058 tests and the full serial solution lane passed 4,391 tests across
21 suites, with zero failures or skips. STEP export/reimport retained 24 definitions,
60 bodies and 36 repeated instances. Blender 5.2.2 imported the posed GLB with
60 mesh objects and 24 shared mesh datablocks, and passed normal/transform checks.
Generated reports, STEP/USD/GLB files and render evidence are under ignored
`artifacts/local/atlas-modernization/`.

The synchronized ten-module Helios example also passed the production Chromium
workflow: intact source, native Worker build, visible geometry, hierarchy selection
and inspector. It reported 24 definitions, 113 occurrences, zero diagnostics and
zero page errors. The measured Worker build was 18.6 seconds in this local run;
this is a workflow sample, not a benchmark.
The complete refreshed browser run passed eight workflows, including all five
examples and gallery/theme behavior; all 37 Helios unit tests also passed.
