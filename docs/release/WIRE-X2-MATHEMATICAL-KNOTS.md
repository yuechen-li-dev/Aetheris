# WIRE-X2 — Mathematical Knot Paths

## Executive verdict

**Accepted.** Aetheris can author named Trefoil, mathematical Figure Eight, and coprime TorusKnot families as closed semantic WireForm paths; qualify a finite tube using deterministic nonlocal separation and local curvature evidence; reconcile transported-frame holonomy; and export pcurve-complete, enclosed, non-rational AP242 sculptures. The qualification is honestly described as a conservative numerical criterion, not a formal proof of reach or knot type.

## Substrate audit and architecture

WIRE-X2 reuses WIRE-X1 rather than introducing an arbitrary-curve stack. `WireEvaluablePathAir` is the shared semantic/evaluable operation substrate; `WireCoilAir` remains its open winding specialization and `WireKnotPathAir` is its inherently periodic named-family specialization. Straight/Bend remain exact line/arc state transitions with cylinder/torus realization.

```text
WireForm / Knot family identity
  → WireKnotPathAir (exact evaluable periodic law; seam t=0)
  → deterministic cubic polynomial centerline spans
  → rotation-minimizing frame transport
  → measured holonomy + linearly distributed correction
  → four non-rational polynomial quarter-tube patches per span
  → shared closed BRep + face-local pcurves
  → deterministic AP242 export/reimport
```

Audit answers:

1. KnotPath reuses the WIRE-X1 evaluator, deterministic approximation, tangent transport helpers, polynomial B-spline geometry, topology bindings, STEP exporter/importer, renderer, and WireForm stock accounting.
2. Closed curves require explicit periodic semantics, cyclic station/span topology, no duplicated semantic endpoint, no caps, cyclic nonlocal-neighbour exclusion, one stable seam, frame-holonomy correction, and seam pcurves.
3. WIRE-X1 propagates orientation by the minimal rotation mapping each sampled tangent to the next, then reprojects/normalizes the up vector.
4. Without correction, the final section orientation can differ from the initial orientation and create a twist discontinuity at the seam. X2 measures the signed residual about the tangent and distributes its inverse over the full domain.
5. X0/X1 already had deterministic sampled segment/chord separation for distinct operations and turn-spacing witnesses. X2 adds a cyclic single-curve nonlocal witness and sampled curvature-radius bound because open operation-pair logic cannot qualify tube reach.
6. Turns, pitch/height resolution, support offset/clearance, support families, latitude progression, and adjacent-turn clearance remain coil-only.

## Family and tube evidence

| Family | Closed | Single component | Diameter (mm) | Valid tube | STEP |
| --- | --- | --- | ---: | --- | --- |
| Trefoil, right-handed | yes | yes | 6 | yes | enclosed manifold |
| FigureEight | yes | yes | 6 | yes | enclosed manifold |
| TorusKnot(3,5) | yes | yes (`gcd=1`) | 5 | yes | enclosed manifold |
| TorusKnot(2,4) | semantic family only | no (`gcd=2`) | — | rejected | none |

| Family | Centerline length (mm) | Min nonlocal distance (mm) | Min curvature radius (mm) | Wire radius (mm) | Tube radius limit (mm) | Safety margin (mm) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Trefoil | 576.525803979 | 24.284904872 | 25.746163949 | 3.0 | 12.142452436 | 9.142452436 |
| FigureEight | 773.395525000 | 20.530164330 | 14.839340545 | 3.0 | 10.265082165 | 7.265082165 |
| TorusKnot(3,5) | 910.394968258 | 22.153729016 | 32.313624401 | 2.5 | 11.076864508 | 8.576864508 |

The 20 mm-scale Trefoil admits a maximum diameter of approximately 24.284705 mm after numeric margin. The canonical thin (3 mm) and near-limit (22 mm) fixtures pass. The 100 mm invalid fixture fails `wireform-knot-tube-self-intersection` with requested/admitted diameters and closest parameters `t1≈0.306640625`, `t2≈0.693359375`; compilation emits no STEP. Uniform 2× scale plus 2× diameter preserves the dimensionless ratios. Rigid frame rotation preserves intrinsic length and clearance.

## Frame closure

| Family | Initial frame source | Raw transported mismatch (rad) | Distributed correction (rad) | Final error (rad) |
| --- | --- | ---: | ---: | ---: |
| Trefoil | projected authored Up at `t=0` | -2.225490679 | +2.225490679 | 1.11e-16 |
| FigureEight | projected authored Up at `t=0` | +0.536591895 | -0.536591895 | 0 |
| TorusKnot(3,5) | projected authored Up at `t=0` | +3.109257060 | -3.109257060 | 2.15e-16 |

The final cyclic span consumes the last fraction of the distributed correction and reuses the initial ring, so the topology and section frame close together. The general wireframe renderer successfully traverses every face and recovered every emitted pcurve without a knot-specific rendering path.

## Approximation and representation

| Family | Semantic authority | Degree | Spans | Max/RMS centerline error (mm) | Faces / edges / vertices | Pcurves | Max authored pcurve error (mm) |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Trefoil | evaluable periodic named law | 3 | 96 | 0.000082880 / 0.000046239 | 384 / 768 / 384 | 1,536 | < 4.6e-14 |
| FigureEight | evaluable periodic named law | 3 | 128 | 0.000140117 / 0.000079406 | 512 / 1,024 / 512 | 2,048 | 4.37e-14 |
| TorusKnot(3,5) | evaluable periodic named law | 3 | 160 | 0.000199003 / 0.000134698 | 640 / 1,280 / 640 | 2,560 | 4.50e-14 |

Every canonical product contains only degree-3 non-rational B-spline curves/surfaces: analytic product surfaces `0`, rational product surfaces `0`, faceted fallback `0`, and terminal planes/caps `0`. Every export reimports as one body, one enclosed shell, with every edge incident to two coedges. Renderer-side STEP pcurve recovery for the Trefoil recovered 1,536 pcurves with maximum residual `5.61e-9 mm`; no curve/surface family was unsupported.

Canonical SHA-256 values:

- Trefoil: `6502bfd9dbafa597d2efacad5c593690a9967a2bebc891e90a298dbbd106dd25`
- Figure Eight: `cc70d9f97655cd320df22545752d8adba7438da6fc8a535911314a0b2f725514`
- TorusKnot(3,5): `3faa38954166abd7f96f4041bc40faa2c636873bc2672e5f87e95b03b2f66c10`

## Scope and validation

The geometric family identity is authored semantics; sampling validates closure, approximation, and finite-diameter embedding but does not prove a reconstructed knot-theory classification. Closure manufacturing is unspecified. X2 deliberately excludes physical tying, friction/contact, deformation, arbitrary parametric expressions, links, braids, multi-strand weaving, variable diameter, and topology inference.

Focused coverage exercises all three generators, P/Q constraints, periodic position/tangent closure, deterministic approximation, numerical length, cyclic nonlocal distance, curvature limit, tube radius limit, too-thick failure/no STEP, scale/orientation invariance, holonomy correction, cyclic no-cap BRep topology, pcurves, polynomial-only STEP/reimport, renderer, determinism, and stock/volume/mass. The existing WireForm suite retains Straight/Bend/Paperclip and Axis/SurfaceCoil coverage. The warning-free Release solution build passed, followed by a clean full serial run of 3,248 tests with zero failures/skips; the intentionally empty FrictionLab test assembly remains without discoverable tests. One preceding loaded run tripped an existing through-hole performance threshold, then passed in isolation and in the clean full rerun.

Manual generated artifacts and iso/front/top previews live under ignored `artifacts/local/` per repository policy:

1. `wire-x2-knot-1-trefoil.step`
2. `wire-x2-figure-eight.step`
3. `wire-x2-torus-knot-3-5.step`

Reproduce with `aetheris build <fixture> --output artifacts/local/<name>.step` and `aetheris wireframe <step> --out <preview>.svg --view iso --density 8`.
