# P2-TEMPLATE-PART-M1 — StandardLibrary HexBolt foundation

## Outcome

One real McMaster-style threadless hex bolt is now authored through Firmament and
materialized by `Aetheris.Kernel.StandardLibrary` as a deterministic analytic AP242
solid. The top treatment is an actual coaxial cone. Its six sharp-hex side
intersections are exact bounded `HYPERBOLA` curves; no loft, mesh, B-spline, or NURBS
is used as source geometry.

## Supplied STEP audit

The supplied file is McMaster-Carr part `91180A151`, an M8 × 35 class 8.8 bolt model
with physical threads suppressed. Aetheris reports one body, one shell, 66 faces,
164 edges, 108 vertices, an enclosed manifold, and bounds
`X=[-5.3,35]`, `Y=[-7.4304979645,7.4304979645]`,
`Z=[-6.5,6.5]` mm. The reference SHA-256 is
`e90d61aead58021db75855aa1f3f5b78606e743c25a857427e0cbb1bace08953`.

Audited dimensions:

| Invariant | Reference |
|---|---:|
| nominal/shank diameter | 8 mm |
| under-head length | 35 mm |
| total length | 40.3 mm |
| head height | 5.3 mm |
| head across flats | 13 mm |
| rounded-head radial bound | 7.4304979645 mm |
| ideal sharp-hex circumradius | 7.5055534995 mm |
| top-flat diameter | 12.35 mm |
| top cone semi-angle | 65° (25° to top plane) |
| top cone apex X | -8.1794497891 mm |
| under-head blend | torus, major 4.2 mm, minor 0.2 mm |
| longitudinal head-corner radius | 0.26 mm |
| tip chamfer | 45°, X=34.0625..35 mm |
| tip diameter | 6.125 mm |

Reference surface faces are Plane 20, Cylinder 8, Cone 4, Torus 2, and
`BSplineSurfaceWithKnots` 32; Sphere is 0. The 32 B-spline faces are the embossed
head markings. Reference edge curves are Line 68, Circle 18, and BSpline 78. The
reference has no `HYPERBOLA` STEP entity: its exporter encoded cone/rounded-side
intersection trims as splines. Geometrically the head uses one coherent cone support
split into two faces. Its sharp-side limit is the expected hyperbolic conic section.

## Engineering interpretation

M1 intentionally reconstructs engineering intent instead of copying reference
topology. It retains the measured M8 shank, 13 mm hex, 5.3 mm head, 12.35 mm top
flat, 65° cone, 0.2 mm under-head torus, and 45° tip. It omits class/manufacturer
marking geometry and the tiny 0.26 mm longitudinal head-corner rounds. The latter
keeps the requested regular-hex semantic profile and makes the cone/side boundary
the exact reusable hyperbola needed by the family. Property class `8.8` remains
metadata.

## StandardLibrary architecture

`HexBoltSpec` owns the typed parameter data (repository-native millimetre doubles
and degrees): nominal diameter, length, head across flats, head height, top-flat
diameter, top-chamfer angle, tip-chamfer length/diameter, thread length/designation,
property class, and under-head radius. `HexBoltBuilder` separates admission,
dimensional derivation, exact topology construction, semantic publication, and a
deterministic signature. `McMasterHexBoltSpecs.Reference91180A151` is the explicit
reference instantiation. `HexBoltParameterBinding` is the typed boundary from the
record-shaped Firmament V2 fields to the spec; geometry code contains no M8-only
literals.

The canonical authoring fixture is
`testdata/firmament/examples/mcmaster_91180a151_threadless_hex_bolt.firmament`:

```firmament
Record HexBoltSpec {
    StableId: String
    NominalDiameter: Length
    Length: Length
    // remaining typed engineering fields
}
Static Bolts: HexBoltSpec[] = [
    HexBoltSpec { StableId: "McMaster91180A151" NominalDiameter: 8mm Length: 35mm /* ... */ }
]
Template HexBolt(HexBoltSpec spec) {
    StandardPart Bolt {
        Family: HexBolt
        StableId: spec.StableId
        NominalDiameter: spec.NominalDiameter
        Length: spec.Length
        // remaining fields
    }
}
HexBolt(Bolts[0])
```

This is canonical Firmament V2. `Record`, `Static`, and `Template` expose and bind
the parameters; the generic `StandardPart` record is the reusable StandardLibrary
invocation boundary. Static expansion produces a normalized
`FirmamentV2StandardPartRecord`, and the V2 build dispatcher calls the typed
StandardLibrary builder before generic primitive lowering. No deprecated V1
`library_part` execution and no bolt-specific grammar such as `Bolt<Hex>` is used.

## Exact construction

The head starts from a regular hex with apothem `S/2` and circumradius
`S/sqrt(3)`. Six planar side faces run from the under-head plane to the cone
intersection. The top cone has apex
`topX - topRadius/tan(semiAngle)` and is represented once in the geometry store.
Six face sectors share that support. Each sector is bounded by a circular top arc,
two cone generators, and one exact hyperbola against a vertical hex-side plane.
The six top arcs also bound one planar circular top face.

The underside is planar outside the under-head blend. Two toroidal half faces share
one torus support, two cylindrical half faces share one shank support, and two
45-degree conical half faces share one tip support. Periodic faces are split only for
ordinary downstream topology; their analytic supports remain coherent.

Admission rejects non-finite/non-positive dimensions, a top circle at or outside
the apothem, a cone consuming all head-side height, invalid tip reductions, an
invalid under-head radius, thread length outside `[0, Length]`, and empty thread or
property metadata. Diagnostics use typed `HexBoltAdmissionCode` values and stable
StandardLibrary sources.

## Semantics and thread policy

Stable descendants include `Head`, `Head.TopFlat`, `Head.TopChamfer`,
`Head.TopChamfer.Face[0..5]`, `Head.Side[0..5]`, `Head.UnderHead`, `Shank`,
`ThreadRegion`, `TipChamfer`, and `TipFace`. Periodic face children are published
where a region owns two or six faces. Firmament V2 carries these identities, parent
relationships, face ownership, parameter metadata, the template name, and the
deterministic signature into `FirmamentStandardPartReport`.

Cadmata exposes the fixture as `hexbolt-m1`. Its concept-visualization artifact has
one `TemplateInstance`, inspectable V2 parameter metadata, semantic `PartRegion`
entities, generated-face entities, and BRep face ownership. The server integration
test proves the top-chamfer region owns its six conical faces. This stays within the
existing concept-visualization infrastructure; no separate TemplateInstance UI or
browser-side topology matching was introduced.

`ThreadRegion` aliases the cylindrical shank faces semantically. It carries
`M8 x 1.25`, 22 mm, and `material-geometry=Cylinder`. No helix exists. Head markings
are not material geometry; `PropertyClass=8.8` is metadata.

## Generated result and comparison

Final Aetheris reimport: one body, one shell, 21 faces, 44 edges, 26 vertices,
enclosed manifold. Faces are Plane 9, Cylinder 2, Cone 8, Torus 2, with zero spline
surfaces. Edge curves contain 6 Hyperbola, 22 Line, and 16 Circle curves. Raw STEP
contains 6 `HYPERBOLA`, 2 `CONICAL_SURFACE` support entities (one shared by all six
head sectors and one shared by both tip sectors), 1 shared `CYLINDRICAL_SURFACE`,
1 shared `TOROIDAL_SURFACE`, and zero B-spline curve/surface entities. Face-family
counts remain Cone 8, Cylinder 2, and Torus 2 because multiple trimmed faces refer
to each coherent analytic support.

Generated bounds are `X=[-5.3,35]`, `Y=[-6.5,6.5]`,
`Z=[-7.5055534995,7.5055534995]` mm. This is the same 40.3 mm total length,
8 mm shank, 13 mm across flats, and ideal regular-hex circumradius; the reference
radial corner bound is 0.075055535 mm smaller because its six corners are rounded.

The generated SHA-256 is
`7221a75bbf8d21a72080dede80d9253f65d66986419c224f0a8b50682dec1a85`.
Two independent rebuilds produced the identical hash. FreeCAD 1.0 imported one
closed valid solid/shell without healing and preserved Plane/Cone/Cylinder/Toroid
families. FreeCAD volumes are 2526.679304884845 mm³ generated versus
2519.532295447053 mm³ reference (+0.284%, explained by sharp versus 0.26 mm rounded
head corners and omitted markings). Aetheris `verify` reimport succeeds and reports
orientation-consistent enclosed topology.

Visual comparisons are under `docs/preview2/evidence/hexbolt-m1/`. The generated
isometric and side views show the circular-flat/conical hex head, cylindrical shank,
under-head blend, and tip chamfer. The evidence README records the bounded Cadmata
viewer finding and final FreeCAD capture.

## Tests and parameter dogfood

Focused tests cover regular-hex derivation, across-flats/circumradius, top-flat and
head-height admission, exact hyperbola trims, one coherent head-cone support,
preflight/manifold topology, stable semantic IDs, deterministic STEP, reimport,
zero NURBS, tip chamfer, reference dimensions, V2 record/static/template expansion,
V2 StandardPart materialization, and Cadmata descendant publication. A second
M10 × 50 spec changes diameter, length, head dimensions, tip, thread length, and
designation without changing geometry code and reimports successfully.

## Validation

The canonical V2 fixture passes `aetheris validate`, `aetheris build`, and
`aetheris verify`. Three V2 outputs (two explicit builds and the checked-in fixture
STEP) are byte-identical at SHA-256
`7221a75bbf8d21a72080dede80d9253f65d66986419c224f0a8b50682dec1a85`.
CLI JSON publishes `standardPart.family=HexBolt`, `template=HexBolt`, the complete
parameter record, deterministic spec signature, and semantic descendants.

Repository validation on .NET 10.0 completed with zero build warnings:

- restore: all projects up to date
- build: success, 0 warnings, 0 errors
- tests: CLI 348, Core 994, Firmament 1035, Server/Cadmata 48; 2425 passed total
- FrictionLab: no discoverable tests (existing project state)
- frontend: production build, ESLint, and all 69 Vitest tests passed

## M2 recommendation

Proceed with `P2-TEMPLATE-PART-M2 — general HexBolt Template + semantic/template-
instance inspection`: generalize the V2 template/preset layer around this unchanged
builder, promote the current metadata table into the normal interactive parameter
editor, add standards data as a separate layer, and decide whether rounded-corner
head treatment is an optional family parameter. Do not add physical threads by
default.
