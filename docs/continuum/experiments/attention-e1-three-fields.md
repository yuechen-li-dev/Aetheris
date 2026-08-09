# AETHERIS-CONTINUUM-ATTENTION-E1

Status: completed CPU experiment. Result classification: **negative / classical-equivalent**.

## Direct answer

No. On this anisotropic, two-material scalar problem, explicitly representing Geometry, Material, and Authority did not let Aetheris choose a better sparse inverse-interaction graph than a control selected from local `K` coefficients under the same graph/application budget. The all-field graph needed 35 iterations versus 34 for the coefficient-only graph at `16^3`, and 72 versus 63 at `32^3`. The best isolated ablation, Material-only, saved one iteration at `16^3`; that information was already explicit in the strong off-axis bonds of `K`, disappeared when combined with the other fields, and did not establish a semantic-information advantage.

The three-field view remains useful architecture and vocabulary for keeping exact geometry, constitutive direction, and confidence/policy separate. In this solve it reduces operationally to strength-of-connection and interface/subdomain information that AMG or domain-decomposition machinery can use at least as directly. “Field-aware sparse interaction” is a more accurate term than attention, but even that mechanism did not clear the classical control gate in E1.

## Oct source review (source-derived)

Reviewed directly under `oct/Experiments/ContinuumComputabilityBoundary`:

- `REPORT.md`, especially M14–M20
- `M14/continuum_computability_boundary_m14_probe.oct`
- `M15/continuum_computability_boundary_m15_probe.oct`
- `M16/continuum_computability_boundary_m16_probe.oct`
- `M17/REPORT.md` and `M17/continuum_computability_boundary_m17_probe.oct`
- M18 report/probe
- `M19/REPORT.md` and `M19/continuum_computability_boundary_m19_probe.oct`
- `M20/REPORT.md` and `M20/continuum_computability_boundary_m20_probe.oct`
- M27, M28, M31, and M34 mechanics reports and their valid probes

The machine-readable review is [oct-three-field-review.json](../artifacts/attention-e1/oct-three-field-review.json).

### Geometry Field

In Oct, geometry was a fixed `6x6` Cartesian carrier around a circle, not CAD feature identity or topology. Early geometry was fractional occupancy/coverage. M15 added a cell-centred SDF with negative-inside convention and `abs(SDF) <= dx` narrow band. Central differences of SDF produced unit normals; perpendicular vectors produced tangents. Arrays stored coverage/SDF and orientation components/strength at each lattice cell. Derived edge fields lived on implicit four-neighbor adjacency.

The downstream use was narrow: orientation-weighted one-step diffusion-like transfer, then a local tangent/normal frame for later constitutive probes. The source supports that explicit SDF proximity/orientation was not equivalent to occupancy and measurably changed the same downstream step. It does not support a scalable solver conclusion.

### Material Field

Material meant a separate interior orientation/direction carrier. M17 used constant horizontal or vertical `Ox`, `Oy`, `Strength` arrays. M19 compared a constant field, a left/right region-tagged field, and two seeded directions propagated by deterministic four-neighbor averaging for exactly eight sweeps. It was not a stiffness tensor, material database identity, or equilibrium solve.

The transported field reduced the artificial hard-tag seam in the tiny fixture and organized interior direction more smoothly. That supports a separate material carrier in the old representation. The fixed-pass seed transport remained a hand-designed organizer, not a validated constitutive law.

### Authority Field

Authority did **not** mean load direction, boundary-condition ownership, source ownership, or causal propagation. It meant local confidence/ownership when geometry-derived and material-derived carriers competed.

- Geometry confidence came from SDF proximity, with a floor.
- Material confidence came from explicit material strength and later an artificial heterogeneous map (left half `0.90`, right half `0.25`).
- M17 used winner-takes-most outside a tie zone and confidence-weighted blending inside it.
- M20 stored `GeometryConfidence` and `MaterialConfidence` arrays independently from material direction. It compared downstream-only scaling, authority-modulated material usage, and authority feedback into fixed-pass transport. The report preferred local usage modulation without hidden transport feedback.
- M27/M28 later found whole-response scalar authority too blunt; coupling-specific modulation preserved local constitutive semantics better.
- M31 explicitly kept authority out of iteration policy.

The supported result is that an explicit confidence policy changed local composition and that narrow coupling-specific participation was cleaner than blanket attenuation. The maps were synthetic, and no Oct result showed that authority improves a PDE preconditioner.

### Translation to current Aetheris

| Oct meaning (fact) | Potential Aetheris analogue (new inference) |
|---|---|
| sampled coverage, SDF proximity, SDF normal/tangent | exact BRep/CIR boundary identity and normals; Cut-cell occupancy/interface metadata where discretization needs it |
| cell arrays on a fixed lattice | fields projected onto Continuum cells/faces/cut entities |
| material orientation array | constitutive tensor, principal axes, material/region identity |
| confidence deciding geometry-vs-material carrier participation | explicit policy/confidence for ambiguous or approximate carriers; possibly BC/feature ownership only if separately defined and justified |
| fixed four-neighbor edge field | assembled operator faces plus bounded experimental inverse-interaction graph |

Exact BRep + CIR + Cut cells make circle sampling, approximate boundary identity, sampled normals, and coverage/SDF as the primary CAD truth obsolete. They do not make field separation obsolete. Authority must not be silently redefined as boundary-condition direction merely because modern Continuum has richer BC semantics.

## E1 hypothesis and falsification

After the Oct review, the tested hypothesis was:

> At an identical eight-interaction-per-unknown aggregate budget, an SPD sparse approximate-inverse interaction graph selected with explicit interface geometry, material identity/principal direction, and Oct-style geometry/material confidence converges faster than the same graph selected only from local `K` coefficients.

It was falsified if the all-field graph failed to beat the coefficient-only graph robustly after setup/application cost, or if the useful feature was already a direct strength of connection in `K`.

## PDE and discretization

The unit cube uses homogeneous Dirichlet boundary values and a cell-centred regular grid. Two material regions are separated by the slanted plane

```text
x + 0.35 y = 0.675.
```

The scalar operator is `-div(A(x) grad u)`. Its SPD directional-energy discretization contains:

- unit isotropic axial bonds in `x`, `y`, and `z`;
- one principal-direction bond carrying `anisotropyRatio - 1`;
- material scaling `1` or the requested contrast;
- harmonic effective coefficients on bonds crossing the material interface;
- explicit positive boundary energy for missing Dirichlet bonds.

For the rotated case, the requested 30-degree direction uses the lattice-representable `(2,1,0)` bond (26.565 degrees). The 45-degree case uses `(1,1,0)`. Thus the rotated operator contains a real off-axis interaction; it is not merely a change in coordinate-axis coefficients. `K` remains symmetric positive definite.

The discrete manufactured solution is

```text
u = x(1-x)y(1-y)z(1-z) [1 + 0.15 sin(2 pi x) cos(pi y)]
f = K u.
```

Using independently assembled `K u` gives an exact discrete reference and isolates solver/preconditioner error. Every converged method reached relative residual below `1e-8`; primary relative solution errors were `2.8e-9` to `1.4e-8` for the reported controls.

## Graph and equal-budget methodology

Candidate edges include the local `3x3x3` neighborhood, distance-two axes, and off-axis `(2,1)`/`(1,2)` directions. Selection is deterministic. The coefficient-only score uses direct normalized bond strength when `K_ij` exists and otherwise a cheap normalized local path strength derived only from `K`.

Every matched graph has exactly `4N` undirected edges: eight interactions per unknown in aggregate, with a hard local degree bound of 12. Every graph uses the same storage and the same SPD application:

```text
P = D^-1/2 (I + 0.8 S_G) D^-1/2
```

where `S_G` is symmetric degree-normalized positive weighted adjacency. Its spectrum lies in `[-1,1]`, so the preconditioner is SPD. At `16^3`, every matched graph has 16,384 undirected edges, 110,592 estimated FLOPs/application, and 753,664 bytes of deterministic storage. This controls graph density, formula, FLOPs, and memory; only edge choice and optional raw edge factors differ.

Field scores were deliberately hand-designed and non-learned:

- Geometry favors interface-tangential candidates according to SDF-plane distance and normal.
- Material favors same-material candidates aligned to the principal axis and penalizes interface crossing.
- Authority favors neighbors with similar geometry-versus-material ownership ratio, derived from the Oct-style confidence pair.
- combinations multiply the included bounded factors before deterministic global ranking.

The selection-only control uses field-selected topology but coefficient-only weights. It also took 35 iterations, versus 35 for field selection+weighting. E1 therefore found no field-aware weighting benefit; the small changes came from topology.

## Classical controls and primary matched table (`16^3`)

Times are medians of three Release solves on the local machine. Setup is one-time and kept separate.

| Method | Interactions/unknown | Preconditioner FLOPs | Memory bytes | Setup ms | Iterations | Solve ms | Final relative residual |
|---|---:|---:|---:|---:|---:|---:|---:|
| CG | 0 | 0 | 0 | 0 | 232 | 15.96 | 7.62e-9 |
| Jacobi-PCG | diagonal | 4,096 | 32,768 | 0 | 48 | 3.43 | 6.77e-9 |
| coefficient-only graph | 8 | 110,592 | 753,664 | ~300 | 34 | 3.45 | 7.53e-9 |
| E0 compact symmetric | 5.625 boundary-averaged | 81,408 | 559,104 | ~1.7 | 43 | 4.12 | 7.61e-9 |
| all three fields | 8 | 110,592 | 753,664 | ~300 | 35 | 2.5–3.4 | 9.34e-9 |
| geometric two-level Richardson-8 | coarse path | 71,680 | 81,408 | <1 | 30 | 4.07 | 9.16e-9 |

Wall-time differences among identical graph kernels at this small size are cache/timing noise; iteration count and identical deterministic application cost are the fair graph comparison. Setup is dominated by the deliberately explicit candidate construction/sort. At `32^3`, both graph setups were about 20 seconds in this research implementation, while the conventional two-level setup was about 4 ms. That makes the setup-cost failure decisive rather than hidden.

The setup-work audit counts 70,812 candidates at `16^3` and 610,108 at `32^3`. Approximate numeric scoring cost is 3.19/5.66 million FLOPs at `16^3` for coefficient/all-field selection and 27.45/48.81 million at `32^3`; sorting adds roughly 1.14 million and 11.73 million comparisons respectively (reported separately because comparisons are not FLOPs). These are intentionally conservative research-code estimates, not hardware instruction counts.

No incomplete-factorization/SSOR baseline was added because no simple existing dependency/path was present. Jacobi, the operator-consistent equal-budget graph, E0 compact control, and conventional two-level path avoid a deliberately weak comparison.

## Ablations (`16^3`, primary contrast 100, anisotropy 16, rotated direction)

| Fields | Iterations | Read |
|---|---:|---|
| none / coefficient-only | 34 | strongest equal-budget control |
| Geometry | 35 | worse |
| Material | 33 | one-iteration isolated improvement |
| Authority | 34 | tie |
| Geometry + Material | 37 | worse |
| Geometry + Authority | 36 | worse |
| Material + Authority | 34 | tie |
| Geometry + Material + Authority | 35 | worse |

The three-field vocabulary is not additive in this experiment. Material-only is the sole primary numerical win, and it is a one-iteration effect using a principal bond already explicit in `K`. Authority-only has no directional carrier and appropriately does not improve the primary case. Geometry plus Material actively spends budget away from the strongest operator bonds.

## Sweeps

### Material contrast

Coefficient-only versus all-fields iterations:

| Contrast | Coefficient-only | All fields |
|---:|---:|---:|
| 1 | 31 | 40 |
| 10 | 34 | 37 |
| 100 | 34 | 35 |
| 1000 | 34 | 37 |

The field graph loses throughout the contrast sweep. Exact material-interface semantics did not improve on harmonic bond coefficients.

### Anisotropy

The anisotropy sweep at aligned orientation gave coefficient/all-field counts of `52/57` (1), `57/55` (4), `68/65` (16), and `77/75` (64). There is a small high-anisotropy selection signal, but it does not transfer to the rotated primary case after `K` exposes its off-axis bond.

### Orientation

Coefficient/all-field counts were `68/65` at 0 degrees, `34/35` at the `(2,1)` rotated case, and `43/46` at 45 degrees. The method therefore fails the orientation robustness test: its wins are grid-aligned and reverse under real rotated bonds.

### Authority configurations

Uniform, opposed, localized, and asymmetric Oct-style material-confidence maps produced coefficient/all-field iterations `34/35`, `34/35`, `34/33`, and `34/35`. Only the localized artificial map saved one iteration. That is not a robust authority effect and does not justify promoting confidence to solver physics.

### Scaling

At `16^3`, coefficient-only/all-field/two-level took `34/35/30` iterations. At `32^3`, they took `63/72/47`, with solve times about `39/51/38 ms`. The field method was clearly losing, so `64^3` was correctly gated off rather than spending a large sweep on a failed variant.

## Error modes

One unit correction `e <- e - P K e` gave:

| Mode | Coefficient-only | All fields | Better |
|---|---:|---:|---|
| low global | 0.9151 | 0.9137 | effectively tied |
| anisotropy-aligned | 0.6663 | 0.6835 | coefficient |
| interface-localized | 0.4801 | 0.6115 | coefficient, materially |
| high grid | 0.5896 | 0.4220 | fields |

Field selection shifts budget toward high-frequency smoothing but damages the important interface-localized mode. That explains why plausible-looking interface/material selections do not improve the complete solve.

Selected-cell interaction records for homogeneous interior, interface, authority-localized, and anisotropic-region cells are persisted in [interaction-graphs.json](../artifacts/attention-e1/interaction-graphs.json). Each record contains neighbor coordinates, normalized signed application weight (positive adjacency inside the SPD inverse action), field values, operator score, and deterministic selection reason.

## Information advantage audit

- Position/direction and face conductance are already in local `K`.
- The principal anisotropy bond is directly in `K`; a strength-of-connection control sees it without semantic help.
- Material identity and the sharp interface are cheaply inferable from coefficient changes, though Aetheris knows them before assembly.
- Exact interface normal/distance is a real semantic prior not explicit in one row of `K`, but it did not help this equal-budget solve.
- Authority confidence is genuinely contextual and absent from `K`, but it is a policy carrier rather than PDE information; its sweep was not robust.

Thus Aetheris does know more before assembly, but E1 found no operational advantage from spending preconditioner edges on that extra information.

## AMG and domain decomposition

The coefficient-only ranking is deliberately close to a small strength-of-connection test: it preserves strong direct bonds and uses short coefficient paths for alternative edges. The isolated material-axis result is information AMG already receives in the off-axis `K` bond. E1 did not show reduced setup; semantic graph construction was vastly more expensive than the simple conventional two-level setup in this prototype.

The material interface and confidence regions also resemble subdomains and interface policies. Suppressing cross-interface edges is a domain-decomposition intuition, not a new attention mechanism. Here harmonic operator coupling handled the interface better than semantic suppression. No novelty over AMG, Schwarz, or domain decomposition is claimed.

## Mathematical contract, determinism, and gates

Numerical symmetry defects were below `2e-17`; all probed energies were positive. PCG detected positive energy on every iteration. Construction, selection, solve, and evidence are deterministic. The timing-free evidence hash is stored in [deterministic-hash.json](../artifacts/attention-e1/deterministic-hash.json).

GPU decision: **no GPU**. The field method is numerically inferior at `32^3`, setup-heavy, and not dominated by a uniquely valuable parallel application. Oct/Prometheus and Copeland/Aurelian were not modified.

Optional field-aware aggregation was not run. The local result failed the gate, and the conventional two-level control was already stronger. Cut cells and exact BRep geometry were not added because they would confound/rescue a failed regular-grid hypothesis. No learning, softmax, elasticity, or new GPU infrastructure was introduced.

## Conclusion and next action

E1 is a useful negative result. The interaction-space mental model still makes edge budgets, influence, and symmetry explicit, but “attention” is no longer useful technical branding here. Once the real anisotropic bonds and harmonic interface coefficients are visible to the classical control, the three-field operator is classical-equivalent at best and usually worse.

Recommended action: return to Continuum M3. A later experiment should revisit semantic priors only when exact BRep/CIR/Cut-cell metadata creates information that is genuinely absent from `K` and expensive for a generic solver to infer—for example, repeated solves where exact feature/material partitions can amortize setup. It should begin with AMG/domain-decomposition controls, not a more elaborate attention graph.

## Reproduction and evidence

```powershell
dotnet run --project tools/Aetheris.Continuum.AttentionE1/Aetheris.Continuum.AttentionE1.csproj -c Release
```

Artifacts under `docs/continuum/artifacts/attention-e1/` include benchmark, ablation, contrast, anisotropy, orientation, authority, scaling, weight-ablation, residual histories, mode analysis, contracts, interaction graphs, setup/application cost, information audit, Oct review, matched-budget CSV, deterministic projection, and SHA-256 hash.
