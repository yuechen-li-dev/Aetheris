# DIFF-ENGINE-X0 — storage gate and assembly repairs

## Executive verdict

**Meaningful progression. The requested Difference Engine is not complete.** The real mixed gear/ordinary-part assembly path, ten-hole indexing-plate construction and gear display-mesh path were repaired. A reusable decimal storage candidate now builds as exact hierarchical CAD and has an ideal indexing primitive with occurrence-specific state and replay checks.

Gate B is not admitted. A concrete counterexample confirms that a reader connected to storage through a permanent output mesh cancels its transfer when it returns. Disconnecting only the input drive does not solve this. A physical output disengagement or one-way coupling, source-stop trip, clutch actuation and return linkage still need implementation and qualification. This is a specific missing mechanism, not a claim that a rotor adder is impossible.

No full machine, arithmetic adder, carry latch/chain, master cam schedule, overflow mechanism, quadratic presets, independent arithmetic oracle, browser evaluator, or interactive machine viewer is delivered. Gate A's **selected geometry and ideal joint checks** must not be read as acceptance of every physical provision or the entire milestone. The later gates were not replaced with numerical arithmetic or a precomputed animation.

## Brief capability audit

| Existing authority | Reused / finding |
|---|---|
| GEAR-LIB-X0 | Qualified m2, 20-tooth spur geometry and GearAir; transmission tooth count remains independent of the ten index positions. Ratchet/pawl compatibility is not a validated freewheel. |
| ASM-INTERFACE-X1 | Include, reusable Subassembly, Expose, local rest mates and shared definition execution. |
| ASM-GEAR-BRIDGE-X1 | Typed exposed Gear binding, relative occurrence path and actual ratio/center-distance validation. A mixed ordinary-part catalog bug was uncovered below that semantic boundary. |
| ENGINE-X0 / V8 | AssemblyDisplayMeshExporter, prescribed-ratio utility and artifact discipline. Rest placement and prescribed motion do not supply stateful clutch/carry operation. |
| Profile / Pattern | Circle2, exact ring profiles, ordinary extrusion, circular holes and ten-position Radial Pattern. Full-circle composition required the bounded adaptation described below. |
| Revolve / material offsets | Existing bounded geometry routes; not needed for this prismatic storage candidate and not extended. |
| WireForm | Existing spring and winding-datum support remains available. This candidate explicitly uses vertical, gravity-biased index pins; no spring-force claim is made. |
| Copeland / MachinaLayout.JS / TSPack | Read the current Copeland Ariadne/Dominatus consumer and persistence usage, MachinaLayout package surface and TSPack Flow workflow contract after the user's reuse guidance. These remain candidates for presentation/sequencing. No second HFSM, layout engine or workflow stack was added, and no other repository was modified. |

Andrew Carol's [A Difference Engine Built With LEGO Pieces](https://www.cs.princeton.edu/~chazelle/courses/BIB/babbage.pdf) provides modular rotor/clutch precedent. This work independently authors its storage geometry and does not copy LEGO proportions. The requested arithmetic convention remains authoritative: initialize squares as `0000 / 0001 / 0002`, transfer FirstDifference into Result and finish its carries, then transfer SecondDifference into FirstDifference and finish its carries, then return. This convention is documented only; the sequence evaluator is not implemented.

## Architecture and inventory

`StorageDesign` supplies typed millimetre dimensions to ordinary multi-file Firmament templates. The root composes two occurrences of `DecimalDigit`. The assembly compiler owns physical geometry, rest placement, public ports and occurrence identity. `StorageQualification` resolves the public Gear ports and their original GearAir rather than copying tooth parameters into a parallel gear definition. Actual index-hole cylinder centers establish the admitted index count and geometric alignment checks.

The primitive `IndexedRotaryStorage` lives under the existing Core Mechanisms namespace alongside `SliderCrank` and `PrescribedMotion`. It accepts angular drive displacement and axial pin lift. It stores unwrapped angle separately from the index readout, reports each crossed index and rollover, rejects lock/drive and pin-insertion conflicts, and saves versioned snapshots carrying occurrence and definition identity. It is not a scheduler, latch, carry service or adder. Its indexed setup constructor is explicit; ongoing drive cannot accept a digit or register answer. The two occurrences have independent state despite sharing geometry.

| Quantity | Storage witness |
|---|---:|
| Unique physical definitions | 15 |
| Physical occurrences | 40 |
| DecimalDigit occurrences / reusable module definitions | 2 / 1 |
| Gears / stationary shafts / indexing pins | 2 / 2 / 2 |
| Clutches / carry latches | 0 / 0 |
| Assembly depth, including root and leaf | 3 |
| Generated Firmament dependency files | 4 |
| Reimported AP242 occurrences, including two module occurrences | 42 |

The 20 physical members per digit are gear, sleeve, display, indexing plate, adhesive annulus, shaft, two retainer collars, two supports, index pin and head, two spacers, two tie-bolt shanks, two bolt-head envelopes and two nut envelopes. Fastener heads/shanks are separate simplified geometric definitions, not a claim of complete purchased fastener CAD.

## Qualification and evidence

| Check | Result / boundary |
|---|---|
| Mixed catalog geometry | Shaft remains a radius-4 mm, 40 mm cylinder alongside the spur gear; it no longer silently becomes another gear. Hierarchical reuse, deterministic STEP and reimport checked. |
| Indexing plate | Ten actual cylindrical holes on radius 26 mm, 4.4 mm diameter; radial pin clearance approximately 0.2 mm at each index. |
| Axial withdrawal | Pin ready tip Z=16 mm, plate top Z=20 mm, maximum lift 5 mm: 1 mm nominal withdrawn clearance. Runtime requires at least 4.5 mm lift. |
| Shaft/sleeve | Nominal 8 mm stationary shaft, 8.4 mm sleeve bore; 0.4 mm diameter clearance. Retainer gaps are 0.5 mm at each sleeve end. |
| Independent indexed storage | All ten nominal positions, source occurrence unchanged while the other advances; in-between readout absent. Generic 2-, 10- and 12-position definitions tested. This is not a three-/four-digit structural variant. |
| Drive and replay | Multiple index crossings, rollover events, irregular vs large increments, pause/serialized restore, invalid snapshot/definition/version, finite bounds and lock conflicts checked. |
| Gear meshes | Every flank has nonempty triangles and finite unit normals; straight extrusion flanks have no erroneous axial normals. Signed volume is between root- and addendum-cylinder bounds and agrees with cap area × thickness within 0.5%. |
| Plate mesh | The existing OBJ SurfaceMeshIR path succeeds: 50 analytic patches, watertight, connected, outward-oriented, zero cracks/nonmanifold edges/duplicate or zero-area triangles. Assembly export now reuses that path, with matching triangle count, winding/normal agreement and signed volume within 2% of the independently calculated plate less eleven bores. |
| Reset counterexample | Compiled equal-gear ratio -1: -36° reader stroke gives +36° destination motion; +36° reader return imposes -36° destination motion. Locking instead would conflict. No favorable clutch disconnection is invented. |
| Physical readiness | Selected nominal clearances only. Adhesive strength, retention forces, gravity-pin reliability, manufacturing tolerance, wear and full moving-contact coverage are unqualified. |
| Later acceptance tests | All 100/200 digit arithmetic combinations, carry cascades, register additions, presets through overflow, ablations of an operating machine, browser parity and fresh-agent complete-machine reuse are **not reached**. |

Geometric index checks use analytic rotations at ten exact indices and a `1e-7 mm` comparison tolerance. Assembly meshing uses angular tolerance π/16, chord tolerance 0.3 mm and 6–64 segments. Planar/cylindrical definitions use SurfaceMeshIR directly; other families retain the legacy tessellator's unchanged five-second budget. CLI OBJ uses its existing default policy (not the coarser assembly policy). These checks do not certify tooth contact. The OBJ planar audit explicitly reports two fallback caps with skinny cells; a high-quality quad layout is not claimed.

The original Matplotlib image showed suspicious apparent recesses despite geometric coverage checks; it is not accepted visual evidence. It is superseded by a self-contained Three.js inspection page using the exported triangles and normals, with top/underside/mesh-edge controls. Browser automation rejected local-file navigation under its URL policy, so the WebGL page's visual result remains unverified. No alternate browser route was used to bypass that restriction.

## Friction and reusable repairs

1. **Observation:** an annulus definition failed meshing with an involute LinearExtrusion face. **Incorrect initial hypothesis:** an ordinary annulus needed a new surface route. **Root cause:** the mixed declaration catalog was passed to the standalone build path, whose Gear profile selected the sibling gear for an ordinary template. **Repair:** remove sibling Gear declarations using the existing producer-owned source spans before ordinary template materialization. **Rejected workaround:** hand-building ordinary parts or dropping GearAir. **Limit:** this does not introduce Gear templates or a new definition compiler.
2. **Observation:** the now-real circular plate threw from ProfileArrangementBuilder.Trim when combined with drilled holes. **Repair:** adapt full circles to four exact circular arcs before existing arrangement operations and classify a full-circle loop analytically. **Rejected workaround:** manually polygonizing every authored circle. **Limit:** no general spline arrangement/intersection engine was added.
3. **Observation:** actual spur meshes rejected LinearExtrusion side surfaces. **Repair:** represent a non-rational B-spline extrusion exactly as a degree-1 ruled B-spline surface for the existing trimmed tessellator. **Rejected workaround:** omit flanks or generate browser gears. **Limit:** this adds the B-spline-directrix case; arbitrary other LinearExtrusion directrices remain subject to existing admission rules.
4. **Observation:** the initial mesh volume was below the solid gear-root lower bound; base-endpoint normals sometimes defaulted to +Z. **Repair:** rescale finite-difference tangents before their cross product so physical absolute-length tolerance does not turn a regular small tangent into a singular normal. **Evidence:** horizontal flank normals and closed-prism volume checks now pass. No tolerance budget was weakened.
5. **Observation:** a full-suite run contended with the new bounded mesh test. **Repair:** isolate the new meshing test collection from other xUnit collections while retaining the production five-second limit. The initial suite also found a release-document link before this report existed; the link is now populated. These were this run's failures, not pre-existing failures.
6. **Observation:** the user identified the existing cylindrical OBJ path after the disk image looked wrong. **Finding:** OBJ already uses shared-boundary SurfaceMeshIR; assembly JSON used the older display path. **Repair:** planar/cylindrical assembly definitions reuse SurfaceMeshIR, record the pipeline, fail visibly on rejected IR geometry, and preserve already-oriented IR normals without applying face sense twice. **Evidence:** annulus, indexing plate, gear and placement tests pass; actual plate OBJ is watertight. **Limit:** gear B-spline extrusions retain the explicit legacy route and cap layout quality remains limited by the existing bounded planar fallback.

## Preliminary make/buy plan

Quantities are for the **two-digit storage witness**, not an invented full-machine BOM.

| Family | Quantity | Proposal | Critical interface / unresolved issue |
|---|---:|---|---|
| Spur gear, display, index plate | 2 each | Make, candidate PA or PETG print | Bonded bore and 36° registration; shrinkage, tooth finish and wear unqualified |
| Rotating sleeve | 2 | Make or machine polymer/metal | 8.4 mm bore on 8 mm shaft; running fit and friction unqualified |
| Adhesive annulus | 2 | Bonding-process allowance | 0.15 mm radial gap; adhesive choice and torque capacity unqualified |
| Stationary shafts | 2 | Buy/cut metal stock | Alignment and support/retainer attachment unqualified |
| Retainer collars | 4 | Buy or make | Axial retention; no set-screw/thread detail or force validation |
| Support plates / spacers | 4 / 4 | Make, candidate printed polymer | Shaft/guide alignment and mounting stiffness unqualified |
| Index pins and heads | 2 each | Buy/machine metal candidate | Gravity bias, straightness, release actuation and wear unqualified |
| Tie bolts and nuts | 4 each | Buy, nominal M4 candidate | Thread geometry is an envelope only; access and tightening process unqualified |

No supplier availability, price, fabrication time or printer-independent fit is claimed. After the release/reset mechanism qualifies, the first fabrication experiment should be one adder/carry module. Ordering or fabricating the full engine is premature.

## Reproduction and handoff boundary

Base checkout: `e7134210f0f18ccd51fc7c1f8fb992e879b810b2`; this report describes the working-tree changes, not a newly created commit. Public instructions are in the [storage guide](../public/firmament/difference-engine.md).

```powershell
./demos/Aetheris.DifferenceEngine/Run.ps1 -Packaged
dotnet build Aetheris.slnx -c Release -m:1
dotnet test Aetheris.slnx -c Release --no-build -m:1 --filter 'Category!=SlowCorpus'
pwsh -NoProfile -File scripts/Test-CanonicalFixtures.ps1 -NoBuild
pwsh -NoProfile -File scripts/Test-RepositoryLayout.ps1
git diff --check
```

Default output: `artifacts/local/demos/difference-engine/`. Exact source, mesh, STEP, manifest, state/event evidence and hashes are included there; timings, package version, temporary paths and logs are separate. `index-plate.obj-report.json` records the real CLI topology and planar audit. `BuildInspection.ps1` generates `storage-inspection.html` using the existing client Three.js package; no server is required. STEP supplies only product geometry and structure; the primitive's definition/snapshots do not make the STEP an executable calculator.

Final local validation:

- Release solution build: 0 warnings, 0 errors (`release-build-final.log`).
- Serial solution tests, excluding `SlowCorpus`: **3,512 passed**, 0 failed across 18 test projects (`serial-regression-final.log`). The legacy-gated FrictionLab assembly contains no matching tests in this lane.
- Focused storage tests: 5 Core and 3 Firmament cases passed; the additional existing annulus/placement comparisons passed (6 Firmament mesh tests together).
- Fresh package `2.0.0-diffengine-x0.20260914023545` installed and executed outside the checkout. Repository/demo/package STEP and assembly meshes are byte-identical; repository/package plate OBJ is byte-identical. The imported assembly has 15 definitions and 42 occurrences including two modules.
- OBJ plate: 7,400 final triangles, 3,879 polygons including 3,374 quads, maximum reported boundary chord deviation 0.0438548879 mm. The assembly policy independently yields 8,680 plate triangles; its different tolerance policy is explicit.
- Repository layout guard and `git diff --check` passed. The guard reports 4,192 tracked files; new files were separately checked against the declared fixture/demo/public-doc/release-report locations.
- Inspection-page scripts pass `node --check`; browser rendering remains unverified due to the local-file URL rejection above.
- Separate-output deterministic regeneration: all 9 source/core artifact SHA-256 entries match (`determinism-report.json`).
- Canonical lane: **210 operations passed out of 213 source files**, including both new fixtures; the overall command exits 1. The first reported failure is the existing `Assembly/annular-pair.firmament` `LegacyExplicit` source-policy violation. Three existing include catalogs (`AssemblyInterfaces/digit-module.firmament`, `executable-cell.firmament`, `register.firmament`) are incorrectly dispatched as standalone roots by the qualification manifest and independently return `assembly-parse-missing-root`. All four source conditions are present at the base commit. The runner's stop-on-error report prints only the first accumulated error; the three direct CLI diagnostics are retained in `canonical-*.json`. This is not a clean canonical lane.

| Artifact | SHA-256 |
|---|---|
| `storage-gate.step` | `AE9131821FBF838873CBC514A44C9FE2C09785D0A85D9D4FEB584BB8879CCAD1` |
| `storage-gate.mesh.json` | `C620878E06D34EA5E9EAB02FE6BC798CB3F7D4B4467CB7177EDC6E49BEC85619` |
| `index-plate.obj` | `C5B70C26E412B3DB8B8BC1959D9D1E27F9572354BA3F0338403ACE9A05655D7A` |

No remote CI, deployment, account change, procurement, full-machine fresh-agent test or modification to another repository was performed.
