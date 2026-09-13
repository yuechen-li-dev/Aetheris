# Editable mechanisms and assembly display meshes

The editable V8 demo is a bounded consumer of ordinary Firmament templates, semantic datum frames, interfaces, the executable assembly pipeline, and an analytical slider-crank API. Geometry and mechanism dimensions originate in C# authoring and generated Firmament. The browser consumes the resulting artifacts.

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

The runner exports a single-cylinder stage A, a valve-equipped stage B, a four-cylinder bank stage C, and baseline/revised V8s. `<variant>.firmament` is an independently compilable dimensioned source artifact. Shared definitions include piston crowns/skirts, rods, sleeves, crank webs, journals, heads, valves and WireForm springs. `<variant>.assembly.json` records actual interface fits, placement authority and world-space residuals. `<variant>.motion.json` records the input and derived dimensions, cylinder bank/phase membership, stable occurrence bindings and solved rest matrices. `<variant>.validation.json` reports the 1° sweep. `<variant>.samples.json` and `browser-parity.json` qualify the same evaluator used by the viewer.

## Interfaces and geometry

Each physical occurrence exposes a datum frame. `RegisteredSeat` registers frames; `JournalBoreSeat` also checks a 44 mm journal inside a 44.4 mm rod bore; `WristPinSeat` checks a 16 mm pin inside a 16.4 mm bore. The crank main journal anchors the assembly. Rods register to crankpins, pins to rods, pistons to pins, sleeves to supports, and valve parts to their head/stem seats. Firmament solves the indexed rest assembly and validates the resulting materialized frames. The motion manifest uses these solved transforms, rather than exporting an unrelated placement list.

`SpringOnStem` constrains the actual `AxisCoil` winding axis to the valve-stem axis, registers its seating frame, and checks the stem diameter against the coil's clear diameter. Firmament exposes `Winding.Axis`, `Winding.Frame` and `Winding.ClearDiameter` directly from the WireForm compiler; a wire's starting tangent is not its coil axis. `<variant>.spring-validation.json` sweeps all 16 springs for coaxiality and fixed-seat drift at 1° intervals. Spring display deformation follows this solved axis, even when it is tilted relative to the source-local Z axis.

These interfaces qualify a rest configuration. They do not implement general dynamic mates or a constraint-based mechanism solver. The demo's typed author computes seat frames from mechanical dimensions, and the prescribed mechanism drives the subsequent motion.

`AssemblyDisplayMeshExporter.Export(AssemblyM1CompilationResult, DisplayTessellationOptions?)` emits `aetheris/assembly-display-mesh/1`: millimetre local meshes with positions, outward normals and triangle indices, shared definition IDs, and individually identified occurrences with world transforms and parent IDs. Matrices use the existing row-vector convention, with translation at indices 12–14. Three.js can consume the array directly because its column-major representation expresses the equivalent column-vector transform. Failed definitions, missing faces, invalid indices and unresolved transforms fail instead of being omitted.

The CLI exposes the same exporter:

```powershell
dotnet run --project Aetheris.CLI -c Release -- mesh artifacts/local/demos/editable-v8/baseline.firmament --format assembly-json --output artifacts/local/demos/editable-v8/cli.mesh.json --json
dotnet run --project Aetheris.CLI -c Release -- asm inspect artifacts/local/demos/editable-v8/baseline.firmament --json
```

Use a packaged `aetheris` executable with the same arguments outside a checkout. `mesh --format assembly-json` accepts executable `.firmament` and `.firmasm` assemblies; it preserves definition sharing and assembly occurrence identity. STEP export remains on the existing `asm export-ap242` path; inspect its `--help` for options.

## Prescribed motion

`Aetheris.Kernel.Core.Mechanisms.SliderCrank(r, L).Evaluate(shaftRadians, bankRadians, crankpinPhaseRadians)` returns the crankpin, wrist pin, bore axis, piston position and rod angle. Shaft axis is +Z; the bore axis in XY is `(sin(bank), cos(bank), 0)`. The exact closure is the crankpin projection onto that axis plus `sqrt(L² - perpendicularDistance²)`, with `L > r > 0`. It is not a sinusoidal piston approximation.

`PrescribedMotion.AngularRatio` supplies the cam's 1:2 ratio. `PrescribedMotion.ValveLift` takes phase, opening angle, duration (closing minus opening), lift and cycle duration; a periodic sin² pulse gives zero lift and slope at each endpoint. The reusable browser equivalent is `Aetheris.Kernel.Core/Mechanisms/Web/prescribed-motion.mjs`.

The V8 uses cross-plane crankpin phases 45°, 135°, 315°, 225°, banks at ±45°, paired rods offset ±8 mm, and firing order 1–5–7–3–6–8–4–2. Ignition is checked against each actual TDC. Each cylinder follows power, exhaust, intake and compression over 720°. Exhaust opens at 180° and intake at 360° after ignition, each for 180°, without overlap. Springs use exported WireForm coils and bounded axial display deformation about their compiler-derived winding axes. Timing collars represent the cam system; their idealized valve actuation is prescribed, without cam-contact analysis.

## Qualification boundary

Every baseline and revised cylinder is swept through 0–720° inclusive at 1° increments. Checks cover rod closure, piston-axis alignment, crank radius, ignition at TDC, parallel piston/head/valve clearance planes, a conservative rod envelope clipped against its sleeve, rod/case floor, swept crank-web envelope/floor, and axial rod/rod/web/support slabs. The report explicitly names this scope. It does not certify all-part interference, physical cam contact, spring stress, gas flow, combustion, torque, pressure or FEA. RPM controls visual playback only.

Browser geometry for engine parts is exclusively exported Aetheris geometry. The floor, lighting and UI are presentation elements. No browser hand placement or independent engine primitive reconstruction is required when changing the specification.
