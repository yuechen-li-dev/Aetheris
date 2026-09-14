# Difference Engine storage gate

DIFF-ENGINE-X0 is **incomplete**. The current executable builds and qualifies a decimal storage candidate, not an operating three-register Difference Engine. Its next gate is non-destructive digit transfer, including source-triggered clutch release and an output coupling that does not back-drive storage during reader return.

From the repository root, run the headless qualification:

```powershell
./demos/Aetheris.DifferenceEngine/Run.ps1 -Packaged
```

Omit `-Packaged` to skip the fresh CLI installation. `-Output <directory>` selects an output directory; the default is ignored `artifacts/local/demos/difference-engine/`. A successful command means the **storage gate checks** passed. It does not mean the full milestone passed. There is no complete-cycle viewer to launch yet.

The command generates ordinary multi-file Firmament, builds the storage assembly, exports shared meshes and AP242, exercises indexing, inspects and reimports the assembly through Aetheris.CLI, and runs the focused tests. The packaged option installs a fresh CLI in a temporary directory outside the checkout and builds a copied storage assembly and a mixed gear/shaft fixture there. It preserves that directory for inspection.

## Source and authority

[`StorageDesign.cs`](../../../demos/Aetheris.DifferenceEngine/StorageDesign.cs) owns the fixed print-oriented dimensions. It supplies ordinary template arguments to the reusable [part definitions](../../../fixtures/DifferenceEngine/parts/Storage.firmament), writes the `DecimalDigit` Subassembly, and composes two independent occurrences. Generated source remains under the output's `source/` directory; its author and templates are versioned. No child STEP is used as linkage.

Each module exposes a typed Gear drive port and semantic position ports. The storage qualification resolves the drive's `GearAir` and relative occurrence path through that public port, checks real cylindrical index-hole locations in the compiled plate, and creates separate rotary state for each occurrence. Definition geometry is shared; state is not.

[`IndexedRotaryStorage`](../../../Aetheris.Kernel.Core/Mechanisms/IndexedRotaryStorage.cs) is a low-level ideal joint under the existing Mechanisms namespace. It accepts forward angular displacement and pin lift, reports crossed index locations and rollover events, rejects a locked drive or misaligned pin insertion, and captures versioned occurrence-specific snapshots. The readout is absent between indexed positions. Setup accepts an initial index explicitly; drive does not accept a target digit or register sum. This primitive neither latches nor transmits carry and does not supply cycle sequencing.

The model assumes vertical stationary shafts and gravity-biased pins. The gear, display, indexing plate and sleeve rotate together through a modeled adhesive annulus. Retainer collars limit axial sleeve travel; their attachment, adhesive strength and purchased hardware fits are unqualified. The pin head rests on the upper guide at the indexed position. A 5 mm lift gives 1 mm clearance above the indexing plate. The runtime admits a maximum 5 mm lift and requires at least 4.5 mm before rotation.

## Inspect the evidence

The output contains:

| Artifact | Meaning |
|---|---|
| `storage-gate.step` | Whole storage assembly; 15 unique part definitions, 40 physical occurrences |
| `storage-gate.mesh.json` | Shared Aetheris meshes, stable occurrence IDs and solved rest transforms |
| `storage-manifest.json` | Hierarchy, source dependency hashes and inventory |
| `storage-qualification.json` | Actual index-hole alignment, independent states, events, and rejected reset candidate |
| `storage-design.json` | Typed nominal design dimensions |
| `hashes.json` | Deterministic source and artifact digests |
| `cli-reimport.json` | Product-structure reimport result |
| `packaged-run.json` | Optional package version, external working directory and STEP digest |

For a self-contained WebGL geometry inspection, use the checkout's existing Three.js dependency (restore `aetheris.client` dependencies first if absent):

```powershell
./demos/Aetheris.DifferenceEngine/BuildInspection.ps1
```

Open the generated `storage-inspection.html` directly in a browser; no server is needed. Top, underside and mesh-edge controls inspect the exported triangles and normals without generating replacement geometry. The second view isolates the indexing plate. This is a geometry inspector, not an operating mechanical simulation. Browser automation could not verify this local page because its URL policy rejected local-file navigation.

Planar/cylindrical assembly definitions now use the same shared-boundary `SurfaceMeshIR` construction as OBJ export. Other surface families retain the existing bounded display tessellator; each definition records `meshPipeline`. An IR failure on a planar/cylindrical definition fails export instead of silently falling back. `Run.ps1` exports the actual reimported indexing plate through `aetheris mesh --format obj`, checks watertightness and topology defects, and compares assembly meshes and OBJ bytes against the packaged CLI. The component is selected by the current import manifest and verified hash, so stale files from earlier runs cannot be mistaken for current geometry.

The earlier Matplotlib image showed suspicious depth-order artifacts and is superseded by this inspector; it is not accepted visual evidence. OBJ topology passes independently, although the plate's planar audit reports a bounded fallback with skinny cells, so no high-quality quad-layout claim is made.

## The next gate

The compiled equal-gear witness has ratio `-1`. A reader stroke of `-36°` advances its output `+36°`; returning that reader by `+36°` imposes `-36°` on the same permanent output mesh. Locking the destination would instead create a lock/drive conflict. Releasing an upstream drive clutch alone does not disconnect this output relationship.

The next implementation must provide and qualify a physical output disengagement or one-way coupling as well as the source-stop trip, input clutch, and reset actuation. The witness is a rejection of that simple candidate, **not** evidence that all possible rotor adders fail. No two-digit carry or complete engine is admitted on the strength of these storage tests.

The frozen future operating order is `Result <- FirstDifference`, Result carries, `FirstDifference <- SecondDifference`, FirstDifference carries, controlled return. Squares start at `0000 / 0001 / 0002`. Arithmetic verification, carry latches, overflow, sequence presets and the diagnostic machine viewer remain unimplemented. There is no arithmetic oracle in the storage primitive.

See the [release record](../../release/DIFF-ENGINE-X0.md) for scope, qualification and remaining limitations. Rotor/clutch and modular-adder inspiration is attributed to Andrew Carol's [A Difference Engine Built With LEGO Pieces](https://www.cs.princeton.edu/~chazelle/courses/BIB/babbage.pdf); the storage geometry here is independently authored.
