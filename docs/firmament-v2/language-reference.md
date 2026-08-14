# Firmament V2 language reference

## Document profiles

Firmament V2 is one language. `.firmament` is the general document profile and may contain assemblies. `.firmasm` is the Assembly/interchange profile: it uses the same lexer, parser, binder, declarations, Templates, diagnostics, `SemanticValue` model, and `AssemblyIR`, and requires exactly one exported/root `Assembly`. Supporting Record, Table, Template, Concept, Interface, semantic, part, and resource declarations remain valid. The historical JSON-shaped `.firmasm` syntax is legacy migration input; the `.firmasm` extension is supported and current.

This is the canonical implementation-grounded reference for Firmament V2 as of Assembly M1. Parser acceptance alone is not support: each feature below is classified by binding, lowering, tests, and a real consumer. Older milestone documents are historical evidence when they disagree with this page.

## Language model

Firmament is a deterministic, statically evaluated CAD authoring language. Source declarations bind names, units, immutable values, semantic contracts, selections, and construction intent. Compile-time Records, Tables, `with`, Templates, Match, Pattern, and `Require` are erased before feature AIR. Exact geometry lowers through bounded AIR/materializers to BRep and STEP AP242. Tolerance is symbolic engineering intent and does not perturb nominal geometry.

### Engineering Modules (M0)

Engineering Modules own domain vocabulary and lowering above the shared language/kernel. M0 built-ins are `Aetheris.Surfacing`, `Aetheris.Piping`, and the reserved `Aetheris.SheetMetal`. Firmament adds no `Module`, `Use`, or import keyword in M0: module-owned Templates expand to ordinary canonical Firmament/Core construction, while Forge capability boundaries use catalog-qualified IDs such as `Piping.PipeRoute`. This avoids syntax with no native collision to resolve. Module identity, version, ownership, and docs/tooling inspection come from the deterministic built-in catalog, not runtime plug-in discovery. See [Engineering Modules](../modules/architecture.md).

**The 3D semantic model is the product definition. A Drawing is a printable projection of that definition, not a parallel source of engineering truth.** Drawing M0B binds `Source` to an authoritative Part or AssemblyIR Product, retains occurrence identity, derives BOM directly from leaf Part instances, and lowers manually selected views/PMI to zoned DrawingIR plus A4 vector output. See [Drawing concepts and templates](../drawing/templates.md).

There are two current frontend lanes:

1. the canonical `Model { Units: mm ... }` V2 parser/binder; and
2. the supported Assembly relational parser for `Interface` plus `Assembly` source. M1 lets its Part tags apply declarations from the adjacent canonical V2 definition catalog.

This split is implementation structure, not two languages. Current `.firmasm` is the Firmament V2 Assembly profile. Only the historical JSON-shaped `.firmasm` syntax is a legacy compatibility format; it is migrated into this current profile and does not define V2 architecture.

## Lexical structure

Identifiers start with a letter or underscore and continue with letters, digits, or underscore. Keywords are case-sensitive in canonical syntax. `//` starts a line comment. Blocks use braces. Lists use brackets and comma separators. Member/path access uses `.`. Named fields use `:`. Template application uses `<...>`. `=>` belongs to Match arms, Template `Require`, and selected canonical projections—not general lambda syntax.

Numeric literals are invariant-culture decimal/scientific numbers. Implemented engineering suffixes include `mm` and `deg`; canonical Models declare `Units: mm`. Primitive compile-time types are `Int`, `Float`, `Length`, `Angle`, `String`, and `Bool`. Static Drawing metadata also admits strict `Version` (`major.minor.patch`) and ISO `Date` (`yyyy-MM-dd`) values. Concept IR additionally carries Point/Vector/Axis/Plane/Box/Region/PointSet values.

## Models, values, and tolerances

```firmament
Model Example {
    Units: mm
    let Width: Length = 40mm tol PlusMinus(0.05mm)
}
```

`Model` is the ordinary compilation root. `Units` establishes the document length unit. `let` declares an immutable compile-time scalar or record field; dependency evaluation is ordered by an acyclic expression graph. Arithmetic is typed. It is not runtime or mutable state.

`tol` attaches an interval to a nominal Length/Angle value. The implemented canonical forms are bilateral `PlusMinus(value)` and asymmetric `PlusMinus(plus, minus)`; assembly-local `Dimension` and `Relation` also accept `tol 0.02mm` and `tol +0.03mm -0.02mm`. Internally all become nominal, lower, upper, and unit. Arithmetic that would silently lose tolerance is diagnosed. Canonical docs prefer `PlusMinus(...)` in Models and the explicit `+... -...` assembly spelling where asymmetric.

An ordinary `let Length` is a compile-time scalar. An Assembly `Dimension` is a named engineering semantic that carries `DimensionalCapable`, an exact `TolerancedDimensionBinding`, provenance, and participation in fits/stackups.

## Records, Static Tables, and `with`

```firmament
Record BlockSpec { Width: Length Height: Length Depth: Length }
Static Table Blocks: BlockSpec Key: Width {
    Width: [20mm, 40mm]
    Height: [10mm, 15mm]
    Depth: [20mm, 40mm]
}
Static Base = Blocks[20mm]
Static Tall = Base with { Height: 15mm }
```

`Record` defines a structural compile-time product type. `Static Name: Type = Type { ... }` creates an immutable value. `Static Table` is a finite columnar compile-time collection with equal column lengths and an optional typed key. Indexing selects a row. `with` derives a new immutable record while rechecking fields and types. Record, row, derivation, and Template argument provenance survive Template expansion and now survive into Assembly definition artifacts.

## Concepts, Structs, and semantic exposure

`Concept` defines a structural contract for one value/type. `Struct Name: Concept { ... }` and `Concept Struct` bind members and validate structural conformance. A materialized Struct may contain primitives/features and an `Expose` block. Exposed members form its public semantic surface and carry stable IDs into Concept IR.

`satisfies` has one implemented meaning: a Template type parameter must structurally conform to a Concept (`type T satisfies C`). `requires` is not its synonym. Lowercase `requires` belongs to Assembly Role capability requirements. Capitalized `Require` is a compile-time predicate/constraint declaration. These spellings are intentionally distinct and are not interchangeable.

Assembly `Semantic Name { ... }` is a first-class exposed semantic member group on a Part definition/instance. It creates a named `SemanticValue` namespace with structural capabilities aggregated from its members. It is not a `Struct`, a runtime object, or a Concept declaration. M1 Template parts obtain the same normalized seam from their ordinary `Expose` block, currently under the generated `Interface` group.

## Point, Axis, Plane, and Dimension

Assembly-local exact datum syntax is:

```firmament
Semantic Joint {
    Point Origin = [0,0,0];
    Axis Axis = [0,0,0] -> [0,0,1];
    Plane Seat = [0,0,10] normal [0,0,1];
    Dimension Diameter = 20mm tol +0.01mm -0.008mm;
}
```

- `Point` is a definition-local millimetre coordinate and produces PointCapable + ExactPoint.
- `Axis` stores a definition-local origin and direction. M0/M1 accept the point-to-direction form above; the direction is normalized by placement/query consumers. It produces AxisCapable + ExactAxis.
- `Plane` stores a definition-local origin and oriented normal and produces PlaneCapable + ExactPlane. Coincidence currently treats opposite normals as angularly coincident; material-side semantics are not inferred.
- `Dimension` stores a named nominal scalar interval and produces DimensionalCapable. An optional direction string exists in the semantic binding but Assembly syntax does not author it yet.

Canonical Concept IR also produces exact Point3/Axis/Plane/Length values from expressions such as `Bounds.Center.Axis(+Z)` and `Bounds.Face(+Z)`. M1 converts Template-exposed values into the same Assembly capability/binding seam and composes them with the instance transform for world queries.

## Profiles, Concept Path, Compose, and Modify

`Concept Path` is a compile-time ordered planar construction (`Start`, `Heading`, named `Line`/`Arc`, `Close`). Validation establishes continuity, positive lengths, winding, and a stable provenance identity. `Profile Name From Path` converts an admitted closed path to exact `ResolvedProfile2D` semantics.

`Compose` builds a named bounded constructive body from ordered operations such as profile stock/extrusion. `Modify Target` applies admitted feature families to an already-bound body. Supported families include semantic Hole variants (Shaft, Counterbore, Countersink with ThroughAll/Depth policies), bounded Slot/profile operations, Pattern, and EdgeFinish routes. Support is intentionally route-specific; diagnostics reject unsupported surface/termination/topology combinations rather than claiming a general CAD operation.

`Selection` names a typed, validated set from an exposed/profile/body source and a bounded `Require` such as closed-loop selection. It is semantic selection—not backend topology-ID scripting.

## Templates, Match, Pattern, and Require

```firmament
Template < Spec: BlockSpec, type T satisfies PrismaticBody >
Struct Block: BlockConcept {
    Require Positive => Spec.Width > 0mm
    // compile-time Match and materialized declarations
}
Struct Part = Block < Spec: Standard, T: Box >
```

Templates accept typed value/Record parameters, type parameters constrained with `satisfies`, and typed defaults. Binding diagnoses missing/unknown/mismatched arguments, default cycles, failed `Require`, and recursive specialization. `Match` is finite compile-time selection over admitted enum/static values. `Pattern` expands a bounded static source into deterministic generated declarations. Specialization occurs before feature AIR and records arguments, defaults, selected Match arms, Record/Table provenance, source spans, and a deterministic specialization identity.

`Require Name => BoolExpression` gates Template/static expansion. Canonical semantic `Require` records may also validate an expected engineering value and project limited PMI. Assembly `Assert ToleranceStackup` uses a `Require: Clearance >= ...` field but is a distinct assertion grammar.

## SemanticReference and SemanticValue

Concept paths and exposed names resolve to `SemanticReference` (value + resolved path + consumer span). The normalized `SemanticValue` carries a stable identity, structural capabilities, exact bindings, exposed children, authored/generated spans, and ordered provenance.

Supported producers are native Firmament concepts/profiles, Template expansion, bounded InlineStep/Recognize, and Forge extensions. Consumers include Profiles/Compose, Selection/Modify, FEA regions, and Assembly Roles. Capability checks are origin-independent. Exact-capability claims require an exact binding.

## InlineStep, Recognize, and Replace

`InlineStep` imports only canonical, exact AP242 accepted by the current importer. `Recognize` attaches bounded region semantics to verified imported face IDs with evidence/confidence; it does not recover arbitrary design history. Recognized values currently expose boundary/selectable/exact/analysis/modify capabilities. `Replace` supports a bounded recognized through-hole rematerialization workflow with verification. Because recognizers do not currently emit AxisCapable/PlaneCapable/DimensionalCapable, recognized regions cannot yet fill those Assembly Roles.

## Forge

Forge concepts are registered typed capabilities with field descriptors. A Forge producer can return validated SemanticValues. Assembly and other consumers dispatch by capability and exact binding, not by Forge/native origin. General arbitrary source-level capability call syntax remains Experimental; the supported public seam is the Forge host/SDK integration and registered concept application tested in the repository.

## FEA analysis language

`Analysis Name { ... }` is parsed by the FEA compiler. The supported bounded declaration includes a source model/InlineStep resource, one isotropic material (elastic modulus and Poisson ratio), fixed-displacement regions, traction vectors, resultant-force vectors, pressure scalars, and requested fields from Displacement, Strain, Stress, and ReactionForce. Regions must resolve through exact semantic bindings. This is compile/lower support to AnalysisIR and solver input, not Assembly FEA.

## Interfaces, Mates, and Assemblies

### Panels and Surfacing

`Panel` is the supported engineering-level Surfacing declaration. Its `Surface` field selects one bounded construction; the result is a four-sided `PanelIr`, not a closed solid Part.

```firmament
Model Canopy {
    Units: mm;
    Panel Saddle {
        Surface: ParametricSurface {
            DomainU: [-1, 1];
            DomainV: [-1, 1];
            X: 40mm * u;
            Y: 30mm * v;
            Z: 12mm * u * v;
        }
        Orientation: Front;
        Thickness: 1.2mm;
        Material: "Aluminum";
    }
}
```

`u` and `v` are dimensionless. `X`, `Y`, and `Z` must have Length dimension. The bounded expression grammar admits numeric and `mm` constants, `u`, `v`, parentheses, `+`, `-`, `*`, `/`, integer `^`, `sin`, and `cos`. `DomainU` and `DomainV` must be finite increasing intervals.

Named parameter constructions are `HyperbolicParaboloid { Width; Depth; Rise; }`, `ParabolicCylinder`, `EllipticParaboloid`, and `Helicoid { Radius; Rise; Turns; }`. `RuledSurface` and `RuledTransition` require explicit `BoundaryA`/`BoundaryB`. `BoundaryPatch` requires `South`, `North`, `West`, and `East`. `SectionSurface` requires at least two explicitly ordered sections. M0 Firmament boundaries admit `Line`, `Arc`, and `Circle`; arbitrary trim networks and authored non-rational B-spline control nets are not language syntax.

```firmament
Panel Strip {
    Surface: RuledSurface {
        BoundaryA: Line { Start: [-20mm,-10mm,0mm]; End: [20mm,-10mm,0mm]; }
        BoundaryB: Line { Start: [-20mm, 10mm,5mm]; End: [20mm, 10mm,5mm]; }
    }
}
```

The Panel exposes `South`, `East`, `North`, `West` edges and `SW`, `SE`, `NE`, `NW` corners with deterministic semantic IDs. Edges have `BoundaryEdgeCapable`, `CurveCapable`, `ExactGeometryCapable`, and a directed exact curve binding; raw BRep edge IDs are not exposed. `Orientation: Back` reverses support normal and boundary winding. `Thickness` and `Material` are optional metadata.

Templates may target `Panel`; ordinary Record/Static Record/Table binding and deterministic specialization occur before the Surfacing bridge:

```firmament
Template < Spec: CanopySpec > Panel RuledCanopy { /* one Surface field */ }
Panel Roof = RuledCanopy < Spec: StandardCanopy >
```

Assembly product trees admit `<Panel Name = Definition>`. A Panel-edge Interface uses two Roles requiring `BoundaryEdgeCapable` (normally also `CurveCapable` and `ExactGeometryCapable`), plus `Continuity: G0;`, optional `Correspondence: OppositeDirections|SameDirection;`, and optional `GapTolerance: ...mm;`. It lowers through the existing Interface/Mate architecture and records deterministic endpoint/G0 residuals. `G1` is bound but diagnosed as unsupported. A Mate does not Boolean-join Panels.

`Concept` is unary structural semantics; `Interface` is an independent relational contract over named Roles.

```firmament
Interface SeatedAxis {
    Role Fixed requires AxisCapable, PlaneCapable;
    Role Moving requires AxisCapable, PlaneCapable;
    Lower AxisCoincident Moving.Axis Fixed.Axis;
    Lower PlaneCoincident Moving.Seat Fixed.Seat;
    Allow rotation:about-axis;
}
```

`Role ... requires ...` states the required capabilities of another semantic participant. `Lower` declares a bounded relational consequence: AxisCoincident, AxisAligned, PlaneCoincident, PointCoincident, or OffsetAlongAxis. It is not a user-defined compiler pass. `Fit A.Diameter inside B.Diameter` evaluates a typed clearance interval and now contributes a typed Mate/Interface dimensional transition. It does not implement ISO fit classes. `Allow` admits a remaining solver freedom (`rotation:about-axis` or `translation:along-axis`); it is neither tolerance relaxation nor multiplicity.

An `Assembly` body contains one XML-like product tree, an `Anchor`, independent `Mate` declarations, optional explicit `Relation`s, and stackup assertions. Part definitions are named after `=`; M1 also admits one Template application, for example `<Part P = Block<Spec: Standard>></Part>`. `Anchor` selects the instance datum whose owning Part receives identity placement. A final transform is a lowered result from Anchor + Mates.

`Template < Spec: T > Assembly Name { ... }` creates a reusable Assembly
definition; `<Assembly Left = Name<Spec: Value>></Assembly>` creates an
occurrence. The normalized Template application is the definition identity.
Local Mates, relations, assertions, and child transforms are solved once per
specialization. Identical occurrences reuse that result and compose child world
transforms from the occurrence transform.

Template Assembly internals are source-private. Existing `Expose` syntax forms
the public module surface, so `Left.Mount` can participate in a parent Interface
while `Left.Housing.Mount` is rejected. Exposed Point/Axis/Plane bindings are
definition-local and become world bindings through occurrence composition.
`Expose` may also publish a `Relation Public: From -> To = Internal.From ->
Internal.To;`; the parent graph sees one summary edge while stackup evidence
retains its structured private contributor chain.

`Relation Name: A -> B = nominal tol ... from "provenance";` is a bounded directed dimensional edge, not an unrestricted equation graph. `Assert ToleranceStackup` finds one deterministic path, sums signed worst-case intervals, retains every contribution, and emits a typed compile failure if its clearance minimum is not met.

## Assertions

- `Assert Volume`: canonical Model assertion evaluated after exact STEP reimport/mass properties; authoritative routes fail the build on mismatch.
- `Assert ToleranceStackup`: Assembly compile-time worst-case interval assertion with full relation provenance.
- Historical V1 `expect` validation forms belong to the legacy frontend and are not canonical V2 assertions.

## Engineering reviews

`Review StableId { ... }` is an ordinary Firmament declaration that compiles to backend-independent ReviewIR. `Target` names a stable semantic/PMI reference and `Status` is typed as `Open`, `Accepted`, `Rejected`, `Resolved`, or `Superseded`. Nested `Comment`, `Issue`, `Proposal`, and `Resolution` entries require a stable ID (an authored ID is recommended), `Author`, and an authored ISO `Date`. `Organization` and `Email` are optional.

A `Proposal` may add `Property`, `Current`, `Proposed`, `Units`, and `Reason`. It records a candidate change but never mutates product source. Drawing compilation rejects unknown review targets and explicit current/proposed unit mismatches. See [the review reference](../collaboration/reviews.md).

## Status appendix

| Status | Constructs |
|---|---|
| Supported | Model, Units, primitive literals/types, let, Record, Static Record/Table/array, indexing, with, Concept, Concept Struct, Struct, Expose, Concept Path, Profile, Compose, bounded Modify/Hole/Pattern/EdgeFinish/Selection, Template, Match, Require, Panel with ParametricSurface/named/RuledSurface/RuledTransition/BoundaryPatch/SectionSurface construction, semantic Panel edges/corners, exact G0 Panel edge Mates, InlineStep, bounded Recognize/Replace, PMI datum/diameter binding, Analysis declarations, bounded Drawing declaration/Concept/Template specialization, Review/Comment/Issue/Proposal/Resolution, SemanticValue, Interface/Role/Lower/Fit/Allow, Assembly/Part/Panel/Anchor/Mate/Relation/Dimension/tol, Assert Volume, Assert ToleranceStackup |
| Experimental | standalone/fill lattice routes, broader PMI controls/export, general Forge source invocation, some Slot/EdgeFinish/Boolean/placement routes, M1 Template Part syntax while the central grammar remains split |
| Internal-only | AIR/CIR/BRep plans, topology IDs, semantic source maps, Template expansion artifacts, Judgment/route machinery |
| Legacy/Deprecated | Firmament V1 TOON-style fixture syntax, `.firmasm`, transform-first assembly execution, legacy lowercase/alternate PMI spellings accepted for compatibility |
| Parser-only / dead | no syntax is promoted solely from an AST type; historical AST `FirmamentV2TemplateDecl` manufacturing-template representation and broad parser regex branches without binder/lowering evidence remain internal/legacy, not public Template syntax |
| Future/incomplete | Panel G1/G2 verification, arbitrary trim networks, SheetMetal flat patterns, SubD/SDF-backed Panels, native AP242 Panel semantics, recognizer-produced datum/dimension capabilities, unrestricted symbolic relations, general kinematics/contact/assembly FEA |

The machine-readable status and source evidence are in `language-features.json` and `artifacts/language-audit-m1/`.
