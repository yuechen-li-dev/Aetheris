# HexBolt as a Concept-constrained Firmament Template (M2)

## Architectural transition

M1 deliberately answered the geometry question first. `HexBoltSpec`,
`HexBoltParameterBinding`, and `HexBoltBuilder` established an exact, deterministic
AP242 oracle with a regular hex head, exact cone/plane hyperbola trims, toroidal
under-head blend, cylindrical shank, and conical tip. M2 preserves that path.

M2 moves family authoring to
`testdata/firmament/examples/hexbolt_template_m2.firmament`. The canonical source
declares `BoltConcept`, the family-specific `HexBoltSpec`, scalar Static records,
and `Template < Spec: HexBoltSpec > Struct HexBolt: BoltConcept`. It contains no
`StandardPart Family: HexBolt` dispatch.

The doctrine is:

- the compiler owns generic exact geometry machinery;
- Templates own reusable engineering-family knowledge;
- standards and presets are typed data;
- generated instances are specializations, not stored part files.

`BoltConcept` stays intentionally small: `NominalDiameter`, `Length`,
`ThreadLength`, `ThreadDesignation`, and `Axis`. Hex-head construction dimensions
remain in `HexBoltSpec`. Property class and thread designation remain semantic;
the material thread region is cylindrical and no marking is engraved.

## Reusable construction recipe

The Template spells a bounded `ExactCoaxialPart` recipe from domain-neutral
operations:

- `RegularPolygonPrism` for the head;
- `CoaxialConeTrim` with exact conic intersection;
- `BoundedTorusBlend` with periodic support splitting;
- `Cylinder` for the shaft/material thread region;
- `CoaxialConeFrustum` for the end treatment;
- `SemanticAxialRegion` and metadata publication.

The connected recipe uses a polygon-generic regular-prism emitter (the HexBolt
Template authors six sides). `ExactCoaxialPartBuilder` lowers the recipe to `ExactCoaxialConstructionPlan` and
uses `ExactConstructionMaterializer`, rather than calling `HexBoltBuilder`.
M1 remains the comparison facade over the same planner/materializer; the Firmament
route has no bespoke bolt-builder dependency. See
[construction-ir-m1.md](construction-ir-m1.md) for the bounded IR and support-sharing
contract.

## Admission rules

Template Require validates positive nominal/head dimensions, head width above
shaft diameter, top flat inside the across-flats envelope, cone angle in
`(0deg, 90deg)`, nonnegative blend radius, reduced tip diameter, tip length inside
the axial length, and thread length in `[0, Length]`. `HexBoltBuilder.Validate`
continues to enforce its stricter derived geometric checks (including cone-height
and torus-radius admission) at the exact construction boundary. There is no
constraint solver.

## Dogfood and comparison

The same Template source contains data for the McMaster 91180A151 M8 × 35
reference, M10 × 50, and a deliberately nonstandard 8.25 × 37.5 mm bolt. Tests
bind each Static record without changing geometry source.

Their deterministic construction signatures are respectively
`b2e86e279016f55625e074153f76561144dc7c3c85cbc943c3e3492a7e19acda`,
`1922883929da993de801a7975727ff371b5e2dc002999c4d6c0907bb8387cf4e`,
and `5c1b9bb6919de54f469d82c6d4febf7ad109cf520793ce88117c235ba0a1f828`.

For the reference, M1 and M2 currently produce byte-identical STEP:

| Evidence | M1 builder | M2 Template |
|---|---:|---:|
| SHA-256 | `7221a75bbf8d21a72080dede80d9253f65d66986419c224f0a8b50682dec1a85` | same |
| Vertices / edges / faces | 26 / 44 / 21 | 26 / 44 / 21 |
| Imported planes / cylinders / cones / tori | 9 / 2 / 8 / 2 | 9 / 2 / 8 / 2 |
| Exact hyperbola trims | 6 | 6 |
| STEP B-splines / NURBS | 0 | 0 |
| Bodies / shells | 1 / 1 | 1 / 1 |
| Structural assessment | enclosed manifold | enclosed manifold |
| Semantic descendants | 29, same stable IDs | 29, same stable IDs |

Aetheris verification reports bounds `[-5.3,-6.5,-7.50555349946514]` to
`[35,6.5,7.50555349946514]`. Its deterministic triangulated sanity estimate is
2501.457449 mm³ and is explicitly non-authoritative. FreeCAD 1.0 / OCCT imports
one valid solid and one shell with 21 faces and reports 2526.679305 mm³. OCCT
retains Plane, Cylinder, Cone, Torus, and all six Hyperbola curves. The emitted
STEP contains no B-spline entity and required no healing.

## Cadmata and TemplateInstance

Cadmata fixture `hexbolt-m2` uses the generic `TemplateInstance` entity. It
publishes Template, instance, specialization identity, Record parameter/type,
Static source, Require evidence, and semantic descendants. The parameter table
uses MachinaLayout Table and shows parameter, declared type, value, and
Static/default status. There is no bolt-specific inspector.

## Future fastener seam

Socket-head, flat-head, round-head, and thumb-screw families should reuse
`BoltConcept` and author their own typed specs and construction recipes. Stud and
threaded-rod Templates can satisfy the same contract without a head recipe.
`BoltHeadConcept` may become useful after multiple real heads expose common
composition evidence. M2 intentionally does not introduce `Bolt<THead>` yet.

The next milestone should be **P2-TEMPLATE-STANDARD-M1**: represent standards and
presets as typed data bound to these Templates, without turning the generator
into a lookup table.
