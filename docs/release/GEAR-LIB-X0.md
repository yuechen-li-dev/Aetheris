# GEAR-LIB-X0 — first-class gear geometry and library families

## Executive verdict

**Accepted.** An Aetheris/Firmament author can request external and internal involute spur gears, bounded straight bevel and miter gears, ratchet wheels, and pawls using ordinary engineering parameters without deriving tooth geometry. `Interface<Gear>` provides typed pair compatibility and ideal kinematics without repeating an Assembly role schema.

Acceptance is geometric and kinematic. It is not a claim of contact simulation, load capacity, AGMA/DIN quality, manufacturing certification, or catalog-part equivalence.

## Authority and construction

Public declarations lower to finite typed Gear AIR. The compiler derives one tooth from module, tooth count, pressure angle, backlash, and standard full-depth proportions, then creates a stable radial sequence `Tooth0..ToothN-1`. It does not emit public per-tooth source and does not copy reference topology.

External and internal flanks use the analytic involute

`x(t) = rb(cos(t) + t sin(t)), y(t) = rb(sin(t) - t cos(t))`.

Each flank is carried by two controlled non-rational cubic Hermite/B-spline spans. Tip and root portions are exact circular arcs; unresolved trochoidal manufacturing fillets use explicit straight base-to-root transitions in X0. Standard addendum is `m`, dedendum is `1.25m`, and clearance is `0.25m`. Backlash is subtracted from circular tooth thickness at the pitch circle, half at each flank. External/internal profiles are extruded along Z by `FaceWidth` through the existing profile extrusion planner. Bores are axial cylindrical inner loops.

Straight bevel/miter construction starts from two involute sections related by the declared pitch cone and face width. Corresponding line portions are planar, circular portions conical, and involute flanks are degree-3-by-1 non-rational ruled B-spline surfaces. This is a bounded pitch-cone approximation, not a scaled already-extruded mesh and not Gleason tooth manufacturing geometry.

## Reference research

All ten supplied STEP files remained in the local Downloads directory. None is copied into fixtures or required at runtime. Aetheris CLI compound analysis supplied body/root count, exact imported bounds, volume, and surface families. Catalog fields are recorded separately from measured CAD evidence. The two `2515...` files declare inch units in STEP; the raw bounds below are inches with millimetre conversions in parentheses. All other bounds are millimetres.

Surface inventory abbreviations are plane/cylinder/cone/torus/B-spline. A rotational pattern matching the catalog tooth count was visible in each gear; the pawl instead provided a single nose/pivot witness.

| Reference | Family and direct CAD evidence | Catalog/inferred engineering values | Generic lesson vs incidental detail |
|---|---|---|---|
| `2664N539` | External spur; 1 body; 21.827 × 21.827 × 20 bounds; P23/C114/Co46/T2/B0; axial bore/hub | Catalog: m1, 20 teeth, 20 pitch diameter, 22 OD, 10 face, 8 bore, 20°; standard root 17.5 inferred | Confirms `d=mz`, `da=d+2m`, axial extrusion. Hub and relief/chamfer faces are product details. |
| `2664N545` | External spur; 1 body; 31.893 × 32.000 × 20; P33/C165/Co66/T2/B0 | Catalog: m1, 30 teeth, 30 pitch diameter, 32 OD, 10 face, 8 bore, 20°; standard root 27.5 inferred | Same tooth law scales with count. Hub and bore treatment remain optional product features. |
| `2664N353_NO THREADS` | External spur; 2 imported roots; 21.827 × 21.827 × 20; P32/C120/Co52/T2/B4 | Catalog: m1, 20 teeth, pitch 20, OD 22, face 10, hub 16 × 10, bore 5, 20° | The no-thread specimen isolates gear/hub geometry; multiple STEP roots and set-screw detail are not gear theory. |
| `2664N313_NO THREADS` | External spur; 2 imported roots; 11 × 11 × 16; P31/C121/Co34/T80/B0 | Catalog: m0.5, 20 teeth, pitch 10, OD 11, face 5, hub 11 × 11, bore 3, 20° | Confirms module scaling at the same tooth count. Hub/set-screw arrangement is catalog-specific. |
| `2696N15` | Internal spur ring; 1 body; 70 × 70 × 5; P2/C206/Co0/T202/B200 | Catalog: m0.5, 100 teeth, pitch 50, OD 70, tooth-tip ID 49, face 5, 20° | Establishes reversed internal flank/tip semantics and independent outer ring stock. |
| `6529K57` | Miter; 1 body; 34.269 × 30.382 × 34.269; P2/C4/Co28/T0/B100; tapered straight teeth | Catalog: m1.5, 20 teeth, 45° pitch cone, 20° pressure, pitch 30, OD 32.8, face 11, bore 8 | Supports a 1:1 constrained straight-bevel family and cone-conforming taper. Hub/set screw is incidental. |
| `2515N337` | Larger straight bevel; 1 body; 1.799 × .477 × 1.800 in (45.69 × 12.12 × 45.72 mm); P3/C4/Co145/T0/B315 | Catalog: m1, 20°, 3:1 pair, 6 face, 8 bore; 45-tooth role and 71.565° pitch cone inferred from ratio/90° shaft convention | Cone and spline populations reject a spur extrusion. Mounting hub/reliefs are product-specific. |
| `2515N333` | Bevel pinion; 1 body; .659 × .493 × .663 in (16.74 × 12.52 × 16.84 mm); P32/C4/Co70/T0/B105 | Catalog: m1, 20°, 3:1 pair, 6 face, 6 bore; 15-tooth role and 18.435° pitch cone inferred | “Pinion” is a pair role, not a separate kernel family. X0 rejects sub-17 ordinary involute members, so this exact low-count catalog member is research evidence rather than a qualified flagship. |
| `6283K75` | Ratchet wheel; 1 body; 40 × 40 × 6; P122/C62/Co124/T0/B0; 60 asymmetric radial teeth and axial bore | Catalog: 60 teeth, 40 OD, 10 bore, 6 face | Ratchets need their own steep-face/ramp profile, not an involute or spur alias. Catalog edge finishes are incidental. |
| `6283K72` | Pawl; 1 body; 38 × 16 × 6; P12/C5/Co5/T0/B0; pivot opening and directed nose | Catalog: for 6 face, 5 shaft, 38 overall length, 30 centre-to-end | Establishes a reusable bounded nose/body/pivot companion; exact outline, chamfers, and material are not copied. |

Catalog facts were checked against the current McMaster family listings for [metric spur gears](https://www.mcmaster.com/products/steel-gear-racks), [internal spur gears](https://www.mcmaster.com/products/internal-spur-gears/), [metal bevel gears](https://www.mcmaster.com/products/metal-bevel-gears/), [miter gears](https://www.mcmaster.com/products/miter-gears), and [ratchet mechanisms](https://www.mcmaster.com/products/ratchet-mechanisms/). These links are research provenance, not runtime dependencies.

## Family matrix

| Family | Qualified | Parameters | Pair semantics | STEP | Notes |
|---|---|---|---|---|---|
| Spur | Supported | module, teeth, pressure, face, bore, backlash, phase | external/external center distance, ratio, opposite sign | Deterministic manifold AP242 | Standard full-depth involute; minimum 17 teeth |
| Internal spur | Supported | module, teeth, pressure, face, outside diameter, backlash, phase | external/internal difference center, ratio, same sign | Deterministic manifold AP242 | Correct internal tip/root and flank orientation |
| Bevel | Bounded | module, teeth, pressure, face, pitch cone, bore, phase | module/pressure, complementary pitch cones, shaft angle, ratio | Deterministic manifold AP242 | Straight pitch-cone ruled approximation only |
| Miter | Bounded | bevel parameters, normally 45° | equal teeth, 1:1, complementary cones | Deterministic manifold AP242 | Constrained bevel implementation, not a second engine |
| Ratchet | Bounded | teeth, OD, face, bore, drive-face angle, phase | ratchet/pawl direction and engagement phase | Deterministic manifold AP242 | Exact asymmetric line/arc profile; minimum 8 teeth |
| Pawl | Bounded | width, length, thickness, pivot, nose, engagement angle | ratchet/pawl direction and engagement phase | Deterministic manifold AP242 | Prismatic body and cylindrical pivot opening |

`GearFamilyCatalog` exposes these six names, categories, fields, qualification, and construction summaries without requiring source inspection. `Gear<T>` remains deferred; `Interface<Gear>` is the deliberately generic public relationship.

## Involute validation

| Witness | Quantity | Theory | Generated/report | Absolute error |
|---|---|---:|---:|---:|
| external m2/z24/20° | pitch diameter | 48 | 48 | 0 |
| | base diameter | 45.1052457977 | 45.1052457977 | < 1e-10 |
| | addendum diameter | 52 | 52 bounds | < 1e-9 radial construction; axis-aligned extrema depend on phase |
| | root diameter | 43 | 43 construction | 0 |
| | maximum sampled cubic-flank deviation | — | 0.001202 mm | bounded below 0.002 mm |
| internal m0.5/z100/20° | pitch diameter | 50 | 50 | 0 |
| | base diameter | 46.9846310393 | 46.9846310393 | < 1e-10 |
| | tooth-tip diameter | 49 | 49 construction | 0 |
| | tooth-root diameter | 51.25 | 51.25 construction | 0 |
| | ring outside diameter | 70 | 70 bounds | < 1e-9 |
| | maximum sampled cubic-flank deviation | — | 0.0000044 mm | bounded below 0.00001 mm |

The standard tooth thickness at pitch is `pi*m/2`. Backlash `b` changes it to `pi*m/2-b`, symmetrically. No arbitrary global scaling occurs.

## Pair and train validation

| Witness | Teeth | Expected ratio `omegaB/omegaA` | Center/axis rule | Verdict |
|---|---:|---:|---|---|
| spur pair | 24:48 | -0.5 | `m(zA+zB)/2 = 72 mm` | compatible |
| external/internal | 20:60 | +1/3 | `m(zRing-zPinion)/2 = 20 mm` | compatible |
| bevel pair | 20:40 | -0.5 ideal sign | 90° shafts; 26.565051177° + 63.434948823° | compatible |
| miter pair | 20:20 | -1 | 90° shafts; 45° + 45° | compatible |
| ratchet/pawl | 60 + pawl | one-way metadata, no continuous ratio | clockwise admitted; 2° engagement phase | compatible |
| three-gear train | 20:30:40 | A→idler -2/3; idler→C -3/4; net A→C +1/2 | centers 25 mm and 35 mm | both interfaces compatible |

These are compatibility and ideal kinematic facts. Pair sources are intentionally validated as multi-member semantic documents; individual bodies build separately and assembly placement consumes the reported interface law.

## Print-oriented witness

The demonstration pair uses module 2, 20/40 teeth, 20° pressure angle, 8 mm face width, 0.15 mm backlash, and 8.4 mm bores for a nominal 8 mm shaft. Pitch-circle tooth thickness is `pi*2/2-0.15 = 2.99159 mm`; the mating space is increased by the same 0.15 mm linear convention. Diameter bore clearance is 0.4 mm. This is **geometrically print-oriented, not manufacturing-certified or printer-universal**. No existing additive minimum-feature checker accepts gear teeth, so these explicit geometric witnesses replace a fabricated DFM result.

## STEP, determinism, and performance

Every single-part flagship exported AP242, re-imported through Aetheris, and returned a closed manifold. Involute side carriers remain non-rational B-splines; circular bores remain cylinders; bevel circular side carriers remain cones. Representative warm-process Release CLI observations on the qualification machine:

| Fixture | Teeth | Build ms | Faces | Edges | STEP bytes |
|---|---:|---:|---:|---:|---:|
| spur-basic | 24 | 458.2 | 195 | 579 | 349,898 |
| internal-spur-basic | 100 | 1,019.7 | 603 | 1,803 | 1,195,023 |
| bevel-basic | 45 | 563.3 | 273 | 813 | 602,626 |
| miter-basic | 20 | 427.6 | 163 | 483 | 326,569 |
| ratchet-basic | 60 | 440.3 | 183 | 543 | 265,693 |
| pawl-basic | — | 332.6 | 8 | 18 | 9,516 |
| spur-high-tooth | 100 | 1,017.8 | 603 | 1,803 | 1,195,439 |

The 100-tooth external stress witness completed in about one second and re-imported manifold. Repeated identical m2/z24 builds produced SHA-256 `0e5299e170548f34c590189a1c39c440651cd6515f539aa9b9fbc4d94351e5d7`.

## Difference Engine readiness

The Difference Engine workload can directly use external spur gears, internal rings, two- and three-member ideal ratios, stable tooth phases/identities, one-way ratchet/pawl metadata, and optional bounded right-angle redirection through straight bevel/miter pairs. It must still supply assembly occurrence placement and prescribed animation from the interface facts.

Remaining gaps are qualified hub/keyway/set-screw specialization, a unified multi-body gear assembly build/export demonstration, gear-contact/interference evaluation, tooth contact dynamics, racks, helical/worm gears, spiral bevel/hypoid geometry, and manufacturing ratings. None blocks ordinary ideal Difference Engine gear trains; contact/load claims remain out of scope.

## Visual review

Exact-BRep wireframes were rendered for the generated spur, internal ring, bevel, miter, ratchet, and pawl bodies, with a magnified involute tooth witness and annotated pair compositions. Matching reference wireframes were rendered for `2664N539`, `2696N15`, `6529K57`, `6283K75`, and `6283K72`.

Manual review found smooth, consistently ordered external involute flanks; even tooth spacing; correct inward-pointing internal teeth; visible bevel/miter tooth taper from large to small section; a steep ratchet locking face with a long ramp; and a pawl nose/pivot relationship consistent with one-way placement. The generated spur deliberately lacks the catalog specimen's trochoidal/rounded root fillet and edge chamfers. The bevel approximation lacks the reference member's product hub, reliefs, and manufactured flank refinements. Pair SVGs are annotated diagnostic compositions driven by interface math, not collision/contact proof.

The reviewable PNG/SVG evidence is generated under `artifacts/local/gear-lib-x0/visuals/`; reference-only renders and measurement JSON are under `artifacts/local/gear-lib-x0/reference-analysis/`. Those ignored artifacts contain no redistributed STEP file.

## Validation inventory

Qualification includes kernel geometry/interface tests, CLI build/inspect/validate tests, Standard Library discovery tests, all canonical gear single-part builds, all pair/train semantic fixtures, the complete invalid fixture corpus, the 100-tooth stress build, STEP re-import and manifold checks, deterministic repeat, public-document links, repository-layout checks, packaged CLI execution outside the repository, Release solution build, and `git diff --check`. Generated STEP, JSON, comparison, and preview evidence stays under ignored `artifacts/local/gear-lib-x0/`.

The repository-wide canonical script passed every new gear/interface fixture but its final verdict remains red because the pre-existing `fixtures/Canonical/Assembly/annular-pair.firmament` uses two `LegacyExplicit` placements that the script's own canonical-source guard forbids. GEAR-LIB-X0 does not weaken that guard or migrate the unrelated assembly. This is the only recorded qualification exception.
