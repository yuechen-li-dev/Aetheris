# Difference Engine showcase

A Babbage-inspired, three-register, four-digit assembly. Aetheris owns every mechanical surface, stable occurrence identity, shared mesh definition and AP242 assembly. The browser illustrates authored demonstration programs with prescribed motion.

## Run locally

From the repository root, with .NET 10, Node.js and Python on PATH:

```powershell
./demos/Aetheris.DifferenceEngine.Showcase/Run.ps1
```

This generates CAD, checks presentation, prepares the production assets and runs a foreground localhost server on port 8776. Open http://127.0.0.1:8776/. Stop the server with Ctrl+C. `-NoServe` only generates/checks the bundle. Three.js is reused from `aetheris.client/node_modules/three`; restore the client dependencies if absent. Nothing is deployed.

## Admitted configuration

The bounded configuration is `DigitPitch`, in millimetres, from 80 through 100 inclusive. Three registers and four digits remain fixed. Increasing pitch separates repeated digit modules, extends shafts and raises the frame, crown gantry and crank together. Gear module, tooth counts and register center distances stay compatible.

Save a JSON file such as `{ "DigitPitch": 90 }` under ignored `artifacts/local/`, then run:

```powershell
./demos/Aetheris.DifferenceEngine.Showcase/Run.ps1 -Spec artifacts/local/showcase-spec.json -Output artifacts/local/demos/showcase-variant -NoServe
```

For an already built executable (no rebuild required):

```powershell
dotnet demos/Aetheris.DifferenceEngine.Showcase/bin/Release/net10.0/Aetheris.DifferenceEngine.Showcase.dll artifacts/local/demos/showcase-variant artifacts/local/showcase-spec.json fixtures/DifferenceEngine
node demos/Aetheris.DifferenceEngine.Showcase/verify.mjs artifacts/local/demos/showcase-variant
./demos/Aetheris.DifferenceEngine.Showcase/Prepare-Viewer.ps1 -Output artifacts/local/demos/showcase-variant
```

The generator rejects values outside the admitted envelope. Do not edit generated Firmament: change the typed author in `ShowcaseDesign.cs` or the admitted JSON configuration. Source geometry templates are under `fixtures/DifferenceEngine/parts/`. Browser code only consumes exported meshes and occurrence IDs; its planes are text labels and the floor.

## Artifacts and authority

Outputs default to ignored `artifacts/local/demos/difference-engine-showcase/`:

- `source/`: ordinary Include/Subassembly/Expose Firmament hierarchy; all gear teeth come from the Gear Standard Library.
- `difference-engine.step`: complete AP242 rest assembly, without animation.
- `machine.mesh.json`: shared display meshes and stable occurrence hierarchy.
- `motion.json`: prescribed programs, actual gear-interface ratios, occurrence bindings and hierarchical explosion offsets.
- `receipts.json`: measured geometry counts, body metrics, gantry clearances and artifact fingerprints.
- `hashes.json`: deterministic source and artifact fingerprints.
- `assembly-inspection.json`: private full compiler inspection, including local paths; excluded from the site.
- `dist/`: self-contained static browser bundle, including STEP, local Three.js and its license.

The crown bars use three `Interface<Fixed>` mates to frame datum seats. Their bottom faces stay 10 mm above the register crowns; the supported shafts project 4 mm above the bars. This is checked against compiled instance body metrics on every generation. Meshed gears use `Interface<Gear>`; digit drive journals use `Interface<Revolute>`.

The browser evaluator is a pure function of program, step and phase. It does not add a state-machine stack. Reset/seek reconstruct transforms from CAD rest matrices, and explosion sums offsets along actual exported parent IDs. Ratchet and pawl animation illustrates carries; it does not qualify contact, clutch torque, transfer or manufacturing.

Read the [public guide](../../docs/public/demos/difference-engine-showcase.md), [gear documentation](../../docs/public/firmament/gears.md), and [assembly documentation](../../docs/public/firmament/assemblies.md). The earlier storage research remains in `demos/Aetheris.DifferenceEngine` and its historical release record.
