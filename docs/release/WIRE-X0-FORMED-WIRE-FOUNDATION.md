# WIRE-X0 — Formed Wire Foundation

## Executive verdict

**Accepted.** Aetheris can author a formed wire product as an ordered semantic `Straight`/`Bend` program, derive exact centerline geometry and ideal cut-stock length, and materialize deterministic analytic STEP through the existing circular Sweep kernel.

## Architecture and audit

```text
WireForm stock + StartFrame + ordered operations
  → deterministic WireState transitions
  → centerline AIR (LineSegment / CircularArc with operation identity)
  → circular Sweep BRep plan
  → cylinders / tori / planar terminal caps
  → STEP AP242 + reimport evidence
```

Direct reuse:

- the Material DB resolver and density contracts;
- kernel `Line3Curve`, `Circle3Curve`, cylinder, torus, and plane geometry;
- BRep topology/binding validation, export preflight, AP242 export/import, and deterministic hashing;
- the mature circular Sweep topology law: one analytic side face per centerline segment, shared section rings at tangent joins, and two terminal caps;
- template specialization and Forge list/describe/invoke.

Shared mathematics without shared semantics:

- Concept Path/Sweep line/arc length and analytic realization;
- Piping ordered route/frame ideas and circular route surfaces;
- Sheet Metal's manufacturing-derived geometry, bend vocabulary, and stock-accounting design law.

The runtime IRs remain specialized. Sheet Metal continues to own regions, neutral/flat patterns, bend allowance, relief, and fabrication evidence. Piping continues to own ports, connections, routing proposals, fittings, cut lists, equipment keep-outs, and assembly interfaces. WireForm is not forced through either domain.

WireForm owns constant circular stock, the explicit start frame, authoritative forming-operation order, downstream state replay, centerline-radius semantics, operation/segment identity, ideal cut length/volume/mass, terminals, local and nonlocal wire clearance, and operation-level inspection. Direct Sweep remains the lower-level trajectory authoring facility.

## Semantics

`WireState` retains position, tangent, `Up`, derived `Right`, and accumulated length. Straight preserves the frame. Bend chooses the current local `Up` or `Right` as its plane normal and rotates the entire frame by the signed angle. Positive angles use the right-hand rule about the named local normal; negative angles reverse it. This admits transformed planar forms and bends in successive perpendicular planes without global-coordinate hacks or frame spin.

X0 Bend `Radius` is the **centerline radius**. The minimum-radius policy is geometric-only: `Radius > Diameter / 2`; no unsupported material forming table is claimed. Angles are nonzero and bounded to ±180°. A bend no greater than 180° plus the minimum-radius rule prevents local tube self-overlap.

Nonadjacent operations are compared in 3D using deterministic chord witnesses. Arc chord distances are reduced by an analytic sagitta error bound, producing a conservative clearance lower bound. Any lower bound below the wire diameter fails closed with `wireform-self-intersection:<opA>:<opB>`. Adjacent tangent joins are excluded. Intentional contact has no X0 semantics and is rejected.

Changing one operation correctly changes every downstream state. Identical stock, start frame, and operations replay to byte-identical STEP.

## Paperclip flagship

The Standard Product template preserves `WireDiameter`, `OuterLegLength`, `InnerLegLength`, `OuterBendRadius`, `InnerBendRadius`, and `Material`. It derives `LowerReturnRadius = (OuterBendRadius + InnerBendRadius) / 2` and lowers to:

```text
Straight InnerRight 14 mm
Bend InnerTop R3 180°
Straight InnerLeft 14 mm
Bend LowerReturn R4 180°
Straight OuterRight 15 mm
Bend OuterTop R5 180°
Straight OuterLeft 15 mm
```

Default evidence:

| Quantity | Result |
|---|---:|
| Diameter | 1 mm |
| Straight length | 58 mm |
| Bend arc length | 37.69911184307752 mm |
| Total wire/cut-stock length | 95.69911184307752 mm |
| Volume | 75.16190668032007 mm³ |
| Mass (304 annealed database density) | 0.5937790627745286 g |
| Envelope | 11 × 25 × 1 mm |
| Surfaces | 4 cylinders, 3 tori, 2 planes |
| Rational product surfaces / faceted fallback | 0 / 0 |
| STEP SHA-256 | `c46578236cd1909984be9b5a9f3720464f7fd6f49adc1ad1e1bba8608327d3c2` |

The recovered Concept Path reference and WireForm centerline are sampled bidirectionally at identical parameters. Qualified RMS, p95, maximum, endpoint deviation, and length difference are numeric-equivalent zero (each geometric deviation `< 1e-12 mm`; length equal at 12 decimal places). Envelope and diameter differences are zero at the same qualified precision. The 15%-longer and wider variants remain enclosed, exact, contact-free, deterministic, and free of rational/faceted fallback.

## Additional flagships and stock accounting

| Witness | Straight | Bend arc | Total | Volume | Mass | Analytic surfaces |
|---|---:|---:|---:|---:|---:|---|
| U-wire | 40 mm | 15.707963267948966 mm | 55.70796326794897 mm | 43.75293203725963 mm³ | 0.3456481630943511 g | 2 cylinder / 1 torus / 2 plane |
| 3D bent wire | 45 mm | 15.707963267948966 mm | 60.70796326794897 mm | 47.679922854246875 mm³ | 0.37667139054855034 g | 3 cylinder / 2 torus / 2 plane |

Both reimport as enclosed manifolds with zero other/rational surfaces and zero faceted fallback.

## 3D state witness

Rounded values below are millimetres and unit directions:

| # | Input position / tangent / up | Operation | Output position / tangent / up |
|---:|---|---|---|
| 1 | `(0,0,0) / +X / +Z` | Straight Lead 20 | `(20,0,0) / +X / +Z` |
| 2 | `(20,0,0) / +X / +Z` | Bend XYBend R5 +90°, `Plane: Up` | `(25,5,0) / +Y / +Z` |
| 3 | `(25,5,0) / +Y / +Z` | Straight Middle 15 | `(25,20,0) / +Y / +Z` |
| 4 | `(25,20,0) / +Y / +Z` | Bend YZBend R5 +90°, `Plane: Right` | `(25,25,5) / +Z / -Y` |
| 5 | `(25,25,5) / +Z / -Y` | Straight Tail 10 | `(25,25,15) / +Z / -Y` |

Structured build inspection retains the unrounded input/output position, tangent, `Up`, `Right`, accumulated length, stable operation ID, centerline segment family, and resulting sweep surface for every row.

## Representation, CLI, and artifacts

`inspect --json` exposes WireForm ID, stock/material, operation counts and ordered list, straight/bend/total length, start/end terminals, radius and frame policies, and state progression. `build --json` additionally exposes volume, mass, bounds, surface inventory, rational/faceted counts, manifold status, STEP reimport, and deterministic hash. Forge's Paperclip schema and invocation contract are preserved.

Manual artifacts follow the generated-artifact policy under ignored `artifacts/local/`:

- `wire-x0-paperclip.step`
- `wire-x0-u-wire.step`
- `wire-x0-3d-bent-wire.step`
- corresponding general-CLI wireframe SVG previews

## Qualification and limits

Targeted tests cover parser/binder integration, ordered state replay, Straight, signed 90°/180°/general Bend, local `Up`/`Right` orientation, 3D frame transport, exact length/volume/mass, geometric minimum radius, fail-closed nonlocal and adjacent-away-from-join contact, operation identity, analytic lowering, STEP export/reimport, deterministic repeat export, corrected Paperclip reference equivalence, longer/wider variants, CLI inspect/validate, server demo, Forge, and direct Sweep regression. Final qualification passed a warning-free Release solution build, the full serial suite (3,215 tests; no failures or skips), all 122 canonical fixtures, the repository layout guard, Markdown/link tests within the suite, `git diff --check`, fresh CLI package creation, three STEP analyses, and general-CLI wireframe rendering. The empty `Aetheris.FrictionLab.Tests` assembly reports no discoverable tests and is not counted.

Three independent fresh-agent reviews, restricted to public docs and canonical fixtures, selected the intended answers for prompts A–F: direct `WireForm` Straight/Bend authoring, Paperclip policy override, perpendicular local bend planes, compiler-derived stock length, typed tight-radius/contact failure, and analytic Paperclip STEP. Their documentation-friction findings were corrected in the public WireForm, Paperclip, and diagnostics guides.

X0 intentionally defers closed WireForm authoring, coils/springs/helices, arbitrary or parametric curves, mathematical and physical knots, variable/non-circular section, branches, deliberate contact, springback, plastic forming, and process-specific forming allowances. `TotalWireLength` is ideal centerline cut length, not a springback-compensated manufacturing blank.
