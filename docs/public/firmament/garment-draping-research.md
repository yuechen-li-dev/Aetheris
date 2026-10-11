# Garment draping research: improving the known-pattern baseline

Research date: 2026-10-10. This report compares current source and retained drapes
with primary research. It proposes experiments; it does not qualify a new solver
or a production garment. No runtime, material, source cut or acceptance threshold
was changed during this research turn.

Implementation follow-up (2026-10-11):
[panel meshing and settling experiment](garment-meshing-settling-experiment.md)
implements the mesh and convergence instrumentation, retains the failed drapes,
and isolates the sewing/contact initialization conflict. The measurements below
describe the original research baseline, not the new mesh.

## Findings from the actual artifacts

The [GarmentCode workflow](garmentcode.md) removed uncertainty about the reference
cut and curve interchange. A read-only audit of the two Anatolia controls gives
the following measurements. Both contain the same rest mesh: 1,234 vertices,
2,244 triangles and 3,474 unique triangle edges.

| Measurement | Original arrangement | Arrangement raised 100mm |
| --- | ---: | ---: |
| Maximum edge tensile strain | 42.351% | 50.823% |
| 95th percentile edge tensile strain | 3.939% | 1.451% |
| Number of edges above 15% | 23 | 11 |
| Maximum triangle principal tensile strain | 46.240% | 51.364% |
| Rest-area fraction above 15% principal tensile strain | 1.407% | 0.145% |

The raised control's worst link is vertices 1210/1214: 1.775mm rest length and
0.902mm extension. The original control's worst link is much longer, 31.646mm,
so short links are not a complete explanation of both failures. The improvement
in the bulk distribution also does not override the failed peak-strain gate.

Rest-mesh quality is poor in more than one location:

- Minimum edge length: 1.409mm; median: 41.434mm; maximum: 56.916mm.
- Minimum triangle angle: 1.790 degrees. 332 triangles (14.8%) have an angle
  below 5 degrees; 554 (24.7%) are below 10 degrees.
- Maximum/minimum area-lumped vertex mass ratio: 110.74.
- Under the current edge classifier: 208 warp links, 336 weft links and
  2,930 diagonal links (84.3%).

`GarmentPanelMesher` already calls the shared `PlanarMeshQuality.Improve` for
Delaunay edge flips. It then bisects overlong edges. That controls maximum edge
length, but does not provide shape refinement, a minimum angle or a controlled
minimum feature size. Merely adding another edge-flip pass is not the proposed fix.

`ClothCompiler3D` assigns warp/weft stiffness only to almost exactly axis-aligned
material edges; all other directions receive diagonal stiffness. These are
authoring compliances, not a calibrated continuum textile model. Improving
triangulation without checking this dependency could change the apparent fabric.

The current drape uses eight substeps and 24 structural iterations per frame,
one contact pass per iteration, 30 gravity-free sewing frames and a fixed number
of settling frames. The retained controls used 30 settling frames: 0.5 seconds
at 60Hz. There is no equilibrium stopping test in this path. Body contacts use
nearest-surface vertex and triangle-sample projections without friction or CCD.
The default bending model is already boundary-corrected vertex bending; it is
not necessary to reintroduce the earlier Better Bending foundation.

## Research that maps to these gaps

**GarmentCodeData, ECCV 2024.** Their pipeline uses a connected box mesh, matched
samples on paired sewing boundaries, constrained Delaunay panel meshing and
gravity-free initialization. It adds semantic body-part handling, temporary
half-space attachments and a static-equilibrium check. Collar guides are released
before the final drape. This supports treating dressing initialization as an
explicit stage rather than expecting unconstrained gravity to find the intended
placement. [Paper, sections 3.4 and appendix C](https://arxiv.org/html/2405.17609v2).
The pinned MIT wrapper also enables body/fabric friction and schedules attachment
release; its parameters are not portable fabric units for Aetheris.

**Quality triangulation.** Shape and size criteria are distinct. Delaunay
refinement inserts vertices to improve triangle shape; flips alone cannot always
repair a bad point distribution. Acute authored boundaries constrain achievable
angles, so a global minimum-angle promise would be incorrect.
[CGAL Mesh_2 documentation](https://doc.cgal.org/latest/Mesh_2/index.html).

**Physics-inspired Estimation of Optimal Cloth Mesh Resolution, SIGGRAPH 2025.**
Uses material properties and boundary conditions to choose spatially varying
resolution, with smooth transitions between fine and coarse regions. Useful
later for collar/seam/contact sizing maps. We first need a sound material model
and a quality mesher; globally shrinking `meshSize` is not an equivalent method.
[Author's project](https://wanghmin.github.io/publication/zhang-2025-pie/).

**Small Steps in Physics Simulation, SCA 2019.** Shows advantages of spending an
XPBD budget on smaller steps rather than many iterations in a larger step. We
already substep, but have not measured the appropriate split for sewn garments.
This is a cheap controlled convergence experiment, not a claimed cure for
invalid initialization. [Author's paper](https://mmacklin.com/smallsteps.pdf).

**Strain Based Dynamics, SCA 2014.** Controls material strain directions
independently of mesh edge directions and separates stretch from shear. It is a
particularly direct match to the present warp/weft/diagonal classification gap.
Adapting its constraints to XPBD still requires area/energy normalization and
verification; the original PBD stiffness parameters are not physical moduli.
[Author's paper](https://matthias-research.github.io/pages/publications/strainBasedDynamics.pdf).

**Automatic Digital Garment Initialization, SIGGRAPH 2024.** Treats panel
arrangement as its own phased selection/placement problem. Its hybrid classifier
and optimizer need not become a dependency: Firmament can supply the panel roles,
body landmarks and target frames explicitly, reducing ambiguity at authoring time.
That is our proposed adaptation, not a reproduction of their method.
[Author's project](https://wanghmin.github.io/publication/liu-2024-adg/).

**OGC, AVBD and C-IPC.** OGC's local contact and movement bounds are attractive
for Vulkan. Aetheris already has an OGC-inspired foundation, but its exhaustive
candidate admission stops at 128 vertices; the 1,234-vertex dress has offset
self-contact disabled. We need a conservative accelerated candidate search and
seam-aware exclusions before garment-scale use. Initial collision recovery is
distinct from preventing new collisions.
[OGC project](https://ankachan.github.io/Projects/OGC/).
AVBD addresses hard constraints and stiffness ratios, but the existing scalar
probe does not demonstrate a garment advantage over XPBD. Keep it as a later
comparison on identical energies and contacts. [AVBD project](https://graphics.cs.utah.edu/research/projects/avbd/).
C-IPC offers coupled strain limits, thickness and friction with strong contact
handling; use it as a reference for validation and possible offline comparison,
rather than adding its global solve to the current small milestone.
[C-IPC project](https://ipc-sim.github.io/C-IPC/).

**Better Bending, SIGGRAPH 2026.** Retain analytic, refinement and rotated-mesh
tests when changing meshing or materials; a convincing dress render cannot
establish convergence. The existing boundary-corrected bending implementation
does not qualify sharp folds, shaped rest states or every irregular mesh.
[Author's project](https://zhenchen-jay.github.io/publication/betterbending/).

## Recommended experiment order

Each experiment starts from the same retained cut, body and explicitly named
arrangement. Change one variable group at a time. Preserve failed runs and the
existing 15% peak strain, 10mm seam-gap and 5mm sampled-penetration limits.

1. **Quality panel meshing in the existing owner.** Keep analytic profiles and
   named spans authoritative. Use bounded shape refinement/interior point
   distribution plus size control. Improve curve sampling with arc-length and
   geometric-error criteria instead of unconditional minimum sample counts.
   Preserve authored endpoints, seam direction and ease. Compare triangle
   angles, area/mass ratios, vertex count, pattern area and actual drape residuals.
   Do not delete a legitimate short authored feature to improve a statistic.
   Target roughly 20-degree angles where the boundary permits, with explicit
   exceptions and failure diagnostics rather than an unconditional guarantee.
2. **Establish settling convergence.** Compare 30, 120 and 600 settling frames
   on the current owner before selecting a new default. Record residual and
   velocity histories. Then compare substep/iteration splits `(8,24)`, `(16,12)`,
   `(32,6)` and `(64,3)`: all have 192 structural passes per frame. Contact work
   and total wall time still need measurement; equal pass counts are not equal
   cost. A bounded equilibrium check should include seam/strain/contact residuals
   and motion, since high damping can make a wrong solution appear stationary.
3. **Explicit temporary fitting guides.** Use existing figure landmarks and
   collar edge identities to author soft half-space guides. Release them during
   the settling phase, then require an unsupported final equilibrium. Avoid
   permanently fixing collar vertices in world space. Retain guide identity,
   active duration, force/residual and release evidence. Drive the existing
   T-to-A pose transition only after the static control qualifies.
4. **Material-space triangle strain.** Compile rest inverses, area weights,
   grain directions and conflict-free rows once. Project warp, weft and shear
   through the existing XPBD schedule. Verify flat-patch stretch/shear, rotation
   of triangulation relative to grain, rigid-motion invariance and mesh refinement.
   Keep edge springs as a named experimental control; do not call their settings
   measured fabric constants. A strain limiter must report residuals and contact
   conflicts rather than silently clamping positions after contact.
5. **Friction and scalable self-contact.** Add controlled tangential response
   with static/sliding tests and moving-body-relative velocities. Do not use high
   friction to hold an incorrectly placed dress. Extend the existing offset
   narrowphase with conservative candidates and seam-aware neighborhoods; compare
   candidate coverage against exhaustive small cases. GPU bounds, reductions and
   projections belong in real `.v.ts`; retain CPU reference agreement and replay.

Shared seam sampling is a useful companion to meshing: derive paired stations
from both analytic lengths, ease and landmarks before triangulation. Keep
weighted constraints for unequal discretizations. Do not naively weld vertices
and replace the flat material charts with their 3D arrangement. If introducing
joined simulation topology, preserve per-triangle rest charts and explicit seam
behavior, including contact exclusions and bending across the join.

The first implementation milestone should cover steps 1-3 on this one dress.
Step 4 is the next material-model milestone; step 5 is needed before layered
outfits and demanding animation. These priorities are engineering inferences
from the measured failures, not demonstrated improvements from new simulations.

## Evidence, reproduction and licensing

Local audit outputs are ignored under `artifacts/local/garment/research/`:
`audit-garment-mesh.py`, `mesh-audit.json` and `mesh-audit.png`. The audit reads
retained `garment.json`; it does not step, remesh, pin or modify either garment.
It computes unique-edge strain, minimum triangle angles, area-lumped masses and
the singular values of each triangle's 3-by-2 deformation gradient. Principal
strain is an additional diagnostic, not a replacement acceptance metric.
The panel plot separates each material chart; chart locations are not drape
placements. Largest edge-strain differences from the runtime reports are below
0.00001 in strain, consistent with serialized float precision.
Analytic checks also passed: translating/rotating the rest mesh produces zero
edge and principal tensile strain within 1e-9; applying uniform scale 1.1 gives
0.1 for both. These validate the audit calculation, not the garment solver.

```powershell
& artifacts/local/garment/research/venv/Scripts/python.exe `
  artifacts/local/garment/research/audit-garment-mesh.py `
  artifacts/local/garment/garmentcode-dress/antonia-reference-sizing/garment.json `
  artifacts/local/garment/garmentcode-dress/anatolia-raised/garment.json `
  --output artifacts/local/garment/research/mesh-audit.json `
  --plot artifacts/local/garment/research/mesh-audit.png
```

The script uses the existing research venv's NumPy/Matplotlib. No packages or
tools were installed this turn. Artifact hashes are retained in `mesh-audit.json`:
original `24f0a427d2cbf270628ad4fdb1e556e8ecb1c0e6d85a81cfd976664039e4de8a`,
raised `55419de373c21bd0e1d2246e459755504a9350ab0b7486e177729dd0cda3dd87`.
GarmentCode source inspected is pinned to
`d449629979028123a5c4dc9e732a2ec19b7fce31`. No new simulator was executed;
the existing drapes remain failed and no new build/test qualification is claimed.

GarmentCode's MIT license does not cover every associated project. The separate
[NvidiaWarp-GarmentCode license](https://github.com/maria-korosteleva/NvidiaWarp-GarmentCode/blob/main/LICENSE.md)
contains a research/evaluation-only use limitation in section 3.3. Do not copy
that fork into the production runtime under the assumption that it is MIT.
This review adopted no fork code or new dependency. Paper-inspired changes
should remain in the existing Aetheris/Aurelian owners with attribution and
independent validation; any future source reuse needs that source's own license
and notices. Existing MIT pattern fixtures and notices remain unchanged.
