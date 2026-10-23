# Editable mechanisms and assembly display meshes

The editable V8 is an authored Firmament project: reusable Parts, one typed
Cylinder Subassembly, a keyed assembly Pattern, and a static Scene. C# supplies
engineering dimensions and a small scalar rest configuration for each cylinder
from the existing analytical mechanism. The browser consumes compiler artifacts
and the bounded slider-crank evaluator.

## Read and edit the project

Start at [engine.firmasm](../../../fixtures/Canonical/EditableV8/engine.firmasm).
One Inventory entry imports [CylinderUnit](../../../fixtures/Canonical/EditableV8/assemblies/cylinder.firmament).
The engine's `Pattern Cylinders over cylinderConfigs` creates eight keyed units
and their installation/rod-journal relationships. The four-cylinder witness uses
the same definition with a smaller configuration Set.

- `parts/` owns engineering geometry and publishes mounting/bore/dimensional ports.
  The parameterized spring shares identical pitches and distinguishes the actually
  compressed geometry.
- `assemblies/cylinder.firmament` owns local Concept axes, Fixed stack interfaces,
  seating Mates and local pin/spring fits. The parent installs the complete unit
  through `Mount` and checks its published `RodBore` against the crankshaft's real
  published pin. There are no copied virtual journal-diameter datums.
- `design.firmament` owns checked dimension Records. `cylinder-config.firmament`
  defines the rest-state input contract. `rest-configurations.firmament` contains
  keyed immutable `with` derivations: bank orientation, station, rod/piston state,
  valve lift, spring pitch and actual journal endpoint.
- Crankshaft, crankcase and cam banks use scalar layout expressions. The project
  contains no per-Part matrix or pose tables.

Edit a Part, Concept layout or unit directly and inspect the consuming root with
Aetheris.CLI. The paired demo workflow refreshes dimension/rest input data from
`V8Spec`; it does not synthesize the product tree or geometry. Moving membership
changes still require matching motion bindings and reject if a Part is missing or
unbound. The [shared-datum and declarative motion proposal](../../development/architecture/FIRMAMENT-V8-RELATIONS-AND-MOTION-PROPOSAL.md)
describes the next refactoring pass. Its Motion/keyframe syntax and articulated
Subassembly extensions are proposed, not currently admitted language features.

## Build and revise

Run `demos/Aetheris.EditableV8/Run.ps1` from PowerShell. Prerequisites are .NET 10, Node/npm and Python 3. The script resolves the project from its own location and also works when invoked by absolute path from another working directory. `-NoServe` generates and validates without launching the foreground server. `-Output <directory>` selects an output directory.

Create a UTF-8 JSON specification, for example:

```json
{
  "Bore": 92,
  "Stroke": 84,
  "RodLength": 150,
  "BankAngle": 90,
  "CylinderSpacing": 110,
  "ValveLift": 8
}
```

Pass its path with `-Spec`. Property names are case sensitive. Omitted values use the defaults: bore 90 mm, stroke 88 mm, rod length 145 mm, bank angle 90°, cylinder spacing 110 mm, valve lift 9 mm. The script also generates a revised engine with stroke increased by 6 mm. Baseline stroke therefore cannot exceed 94 mm in this paired workflow.

The admitted scalar envelope is bore 80–100 mm, stroke 70–100 mm, rod length 135–170 mm, exactly 90° bank angle, spacing at least bore + 16 mm, and lift 1–10 mm. Passing these scalar bounds does not guarantee geometric clearance: the subsequent sweep can reject combinations with `engine-sweep-failed`. Nonfinite or out-of-range dimensions fail with `engine-spec-outside-admitted-envelope`.

The runner exports a single-cylinder stage A, a valve-equipped stage B, a four-cylinder bank stage C, and baseline/revised V8s. `<variant>/engine.firmasm` (or `bank.firmasm`, `stage-a.firmasm`, `stage-b.firmasm`) is an independently compilable project root with its complete module dependencies beside it. Shared definitions include piston crowns/skirts, rods, sleeves, crank webs, journals, heads, valves and WireForm springs. `<variant>.assembly.json` records actual interface fits, placement authority and world-space residuals. `<variant>.motion.json` records the input and derived dimensions, cylinder bank/phase membership, stable occurrence bindings and solved rest matrices. `<variant>.validation.json` reports the 1° sweep. `<variant>.samples.json` and `browser-parity.json` qualify the same evaluator used by the viewer.

## Interfaces and geometry

Concrete Parts publish their own mounting and bore ports. Local Concept axes and
`Interface<Fixed>` seating place each unit's Parts. The engine places each complete
unit once through its public mount. Dimensional-only `JournalFit`, `PinBoreFit`
and `SpringStemFit` check actual component endpoints without adding another
placement driver: 44 mm journals inside 44.4 mm rod bores, 16 mm pins inside
16.4 mm bores, and 8 mm stems inside the WireForm-derived coil clear diameter.
The engine origin retains the former first-main-journal anchor.

The spring's seating member is its real `Winding` endpoint. Firmament publishes
`Winding.Axis`, `Winding.Frame` and `Winding.ClearDiameter` directly from WireForm
AIR. Its starting tangent is not its coil axis. `<variant>.spring-validation.json`
sweeps all 16 springs for coaxiality and fixed-seat drift at 1 degree intervals.
Display deformation follows that compiler-derived axis, including a tilted axis.

These relationships qualify a rest configuration. They do not implement general
dynamic mates. The motion manifest binds the resulting compiler occurrence IDs
and solved transforms, and the existing prescribed mechanism drives motion.

`AssemblyDisplayMeshExporter.Export(AssemblyM1CompilationResult, DisplayTessellationOptions?)` emits `aetheris/assembly-display-mesh/1`: millimetre local meshes with positions, outward normals and triangle indices, shared definition IDs, and individually identified occurrences with world transforms and parent IDs. Matrices use the existing row-vector convention, with translation at indices 12–14. Three.js can consume the array directly because its column-major representation expresses the equivalent column-vector transform. Failed definitions, missing faces, invalid indices and unresolved transforms fail instead of being omitted.

The CLI exposes the same exporter:

```powershell
dotnet run --project Aetheris.CLI -c Release -- mesh artifacts/local/demos/editable-v8/baseline/engine.firmasm --format assembly-json --output artifacts/local/demos/editable-v8/cli.mesh.json --json
dotnet run --project Aetheris.CLI -c Release -- asm inspect artifacts/local/demos/editable-v8/baseline/engine.firmasm --json
```

Use a packaged `aetheris` executable with the same arguments outside a checkout. `mesh --format assembly-json` accepts executable `.firmament` and `.firmasm` assemblies; it preserves definition sharing and assembly occurrence identity. STEP export remains on the existing `asm export-ap242` path; inspect its `--help` for options.

## Prescribed motion

`Aetheris.Kernel.Core.Mechanisms.SliderCrank(r, L).Evaluate(shaftRadians, bankRadians, crankpinPhaseRadians)` returns the crankpin, wrist pin, bore axis, piston position and rod angle. Shaft axis is +Z; the bore axis in XY is `(sin(bank), cos(bank), 0)`. The exact closure is the crankpin projection onto that axis plus `sqrt(L² - perpendicularDistance²)`, with `L > r > 0`. It is not a sinusoidal piston approximation.

`PrescribedMotion.AngularRatio` supplies the cam's 1:2 ratio. `PrescribedMotion.ValveLift` takes phase, opening angle, duration (closing minus opening), lift and cycle duration; a periodic sin² pulse gives zero lift and slope at each endpoint. The reusable browser equivalent is `Aetheris.Kernel.Core/Mechanisms/Web/prescribed-motion.mjs`.

The V8 uses cross-plane crankpin phases 45°, 135°, 315°, 225°, banks at ±45°, paired rods offset ±8 mm, and firing order 1–5–7–3–6–8–4–2. Ignition is checked against each actual TDC. Each cylinder follows power, exhaust, intake and compression over 720°. Exhaust opens at 180° and intake at 360° after ignition, each for 180°, without overlap. Springs use exported WireForm coils and bounded axial display deformation about their compiler-derived winding axes. Timing collars represent the cam system; their idealized valve actuation is prescribed, without cam-contact analysis.

## Qualification boundary

Every baseline and revised cylinder is swept through 0–720° inclusive at 1° increments. Checks cover rod closure, piston-axis alignment, crank radius, ignition at TDC, parallel piston/head/valve clearance planes, a conservative rod envelope clipped against its sleeve, rod/case floor, swept crank-web envelope/floor, and axial rod/rod/web/support slabs. The report explicitly names this scope. It does not certify all-part interference, physical cam contact, spring stress, gas flow, combustion, torque, pressure or FEA. RPM controls visual playback only.

Browser geometry for engine parts is exclusively exported Aetheris geometry. The floor, lighting and UI are presentation elements. No browser hand placement or independent engine primitive reconstruction is required when changing the specification.


## Source and identity during the module migration

The refactor intentionally introduces hierarchical occurrence paths such as
`EditableV8.Cylinders.C1.Cylinder.Piston` and `EditableV8.Crankshaft.Crankpin1`. Motion binds
these full paths, not the final path segment, so an Assembly and one of its Parts
can share a descriptive name without ambiguity. Baseline and revised projects
keep the same hierarchy and occurrence IDs. Old flat IDs are not retained as a
second identity scheme.

The Scene is a static rest-pose projection. Its placement does not add mechanical
joints or carry the demo's runtime motion evaluator. For static interchange:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Canonical/EditableV8/engine.firmasm --json

dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm export-ap242 fixtures/Canonical/EditableV8/engine.firmasm --out artifacts/local/demos/editable-v8/engine.step --json

dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll scene export-glb fixtures/Canonical/EditableV8/scene.firmament artifacts/local/demos/editable-v8/engine.glb --json
```


## Declarative migration status

The V8's common Shaft, bank and cam frames now use derived DatumFrames. Cam
positions are expressed relative to a shared bank, with clocking restored at the
cam datum; the first-main-journal origin and existing engineering configuration
remain the qualification reference.

The compiler now retains articulated Subassembly graphs and admits typed Motion
clips through its ordinary Assembly path. See [syntax](../reference/language/assemblies-and-motion.md#shared-datums-and-articulated-motion).
The V8 has **not** switched to those clips yet: its SliderCrank closure, external
relation roles and axial spring baking still need compiler admission. Its
`CylinderRest` records and existing native/browser motion adapter remain active.
Do not infer full declarative V8 motion from the general rigid-joint witness.
