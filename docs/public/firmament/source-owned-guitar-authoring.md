# Source-owned guitar authoring

The remaining guitar recipes now live in reviewed Firmament files. Python is a
read-only CLI reproduction harness; Blender remains a downstream presentation
step. Neither authors or replaces the guitar BRep.

The guitar now demonstrates the [mixed-case naming convention](language-style.md):
artifact/type vocabulary remains prominent, built-in properties and scope words
read quietly, and private pickup locals and fret functions use camelCase. A safe
formatter pass followed by a bounded manual pass retained published ports,
occurrence paths, template specialization identities and exact export bytes.
Older snippets below remain accepted compatibility spellings.

## Profiles and finite sections

Generic Templates may output `Profile`. Local Point2/Curve2 guides are namespaced
per specialization; Loop span names remain stable for section correspondence.
Profiles are output values here, not higher-order Template arguments.

```firmament
Include "neck-profile.firmament";
Record NeckStation { Y: Length; Depth: Length }
Static Stations: Set<NeckStation> {
 Shaft => NeckStation { Y: 440mm; Depth: 10.1mm }
 Nut => NeckStation { Y: 660mm; Depth: 7mm }
}
SectionChain Neck {
 Pattern Sections Over Stations {
  s => Section Station {
   Frame: Plane { Origin: [0mm,s.Y,55mm]; Normal: [0,1,0]; Up: [0,0,1] }
   Profile: NeckBack<HalfWidth: 21mm, WidthHandle: 11.6mm,
     Depth: s.Depth, DepthHandle: 4mm, SeatHandle: 14mm>
   Seam: Edge0
  }
 }
 // Supply transition and termination intent as in the actual Neck fixture.
}
```

The actual guitar keeps its approved straight shaft taper, adds two isolation
stations before the nut, and uses two more sections for the headstock transition.
`Sections_Nut` is the generated section identity; inspection retains its `Nut`
source key. Inline frame/profile values lower to existing Concept planes and
ordinary Profiles before the SectionChain binder runs.

`Point2` profile controls accept finite, dimension-checked scalar arithmetic after
Template substitution, such as `Position: [-Width / 2, -Depth * 0.55228475]`.
This reuses the existing bounded scalar evaluator. Wrong units, division by zero
and unresolved values produce `profile-layout-point-invalid`; no host source is
executed. The neck's original cubic D-profile is split exactly at its midpoints
so six spans can correspond to the rectangular headstock root. This changes the
span layout without approximating the original cross-section.

The short scarf beneath the veneer is an ordinary exact extrusion seated on the
headstock's published Base frame. The neck, scarf, slab and veneer remain separate
witness solids; their construction is not a certified fused manufacturing joint.
The tuner stem lives inside `GuitarTuner`, so the two three-instance patterns also
reuse it. The tailpiece's exact semicircular rail publishes `StringCrown`; the
bridge forwards that frame as `TailStringPlane`, and string bindings use zero
height offset there.

Body boundary replacements may request `Derivatives: Periodic`. A centered cyclic
Hermite derivative is derived from the ordered replacement knots on straight seed
carriers, with a 1,024-knot limit. Exact closure, cusp and self-intersection checks
still apply. Partial ranges and non-straight seed carriers are rejected. This is a
bounded interpolation recipe, not a general equation-curve or fairness solver.

## Physical material and finish

```firmament
Appearance BareSteel { Color: [0.55,0.57,0.60]; Metallic: 1; Roughness: 0.2; }
Appearance PaintedRed { Color: [0.45,0.02,0.01]; Metallic: 0; Roughness: 0.25; }
Material Steel { Identity: "steel"; Appearance: BareSteel; }

// Inside an ordinary Part occurrence:
Material: Steel;
// Or, for a finish that retains the same physical identity.
Material: Steel with { Appearance: PaintedRed; };
```

Color channels, Metallic and Roughness must be finite and within 0..1. The `with`
override selects only appearance. Material identities are nominal physical
designations, not certified engineering properties; existing material/FEA
qualification remains separate. A decorative pickup compound is explicitly a
presentation placeholder.

Precedence is occurrence `with` override, then physical material default. Authored
occurrence bindings are stronger than legacy exporter definition-material JSON.
Unassigned legacy occurrences retain that compatibility path. USD carries both
physical material and appearance identities as custom properties; Cycles consumes
these identities rather than occurrence-name or specialization-string matches.
Sunburst, lacquer, pearl and pickup feature shading are downstream shader choices.
They do not alter the exported geometry. Look-only edits preserve exact definition
buffers and reuse successful materializations, including length-changing look edits.

## Placed public route endpoints

```firmament
// Lead is a Struct Template with A/B/C: Point3 inputs and one WireRoute.
<Part Wire = Lead<>>
 Placement { From: Origin; To: World; }
 Bind {
  A { At: Point3(0mm,0mm,0mm); On: Product.Start.Entry.Frame; }
  B { At: Point2(0mm,0mm); On: Product.Nut.Top.Frame; }
  C { At: Point3(0mm,0mm,0mm); On: Product.Tuner.StringEntry; }
 }
</Part>
```

Fixed assembly placement resolves first. Each public datum lifts a local Point2/3
to world space and converts it into the consuming route occurrence's coordinates
exactly once. Those resolved Point3 arguments become ordinary specialization/cache
inputs. Inspection reports the port, world point, local point and bound definition.
Changing a terminal placement rebuilds its affected route while unchanged terminal
definitions reuse their materializations.

The first lane requires a fully fixed assembly, ordinary route Templates without
published outputs, and 1..16 named bindings. Dynamic joints, private ports and
route-dependent published frames fail explicitly. Reusable subassembly definitions
remain local prototypes; final occurrence route bindings are recorded separately.
The guitar's tailpiece string rail retains an explicit 5mm decorative stand-off
above its published top plane; this is not a claim of a modeled string hole.

## First locked guide

```firmament
Concept Struct HarnessLayout {
 Line3 Groove { From: Point3(0mm,0mm,0mm); To: Point3(100mm,0mm,0mm); }
}
WireRoute Signal {
 Diameter: 2mm; MinimumBendRadius: 10mm;
 Path {
  Start { At: Point3(0mm,-40mm,0mm); }
  Follow MainGroove { Curve: HarnessLayout.Groove;
    From: 20mm; Distance: 60mm; Direction: Forward; }
  End { At: Point3(120mm,0mm,0mm); }
 }
}
```

This lane follows a finite literal Concept Line3 interval, derives an entry fillet
at the requested minimum bend radius, and requires the end to extend along the
outgoing tangent. It lowers through the existing exact WireForm cylinder/torus
authority. Invalid intervals, insufficient approach room and non-tangent exits
fail. Reverse traversal is supported. Arbitrary/circular/spline guides, exact Via,
multiple corners and obstacle search remain separate qualification work.

## CLI and editor

`aetheris build <root.firmasm> --output artifacts/local/guitar.step` delegates to
the existing native assembly exporter. Root-aware `inspect` delegates to assembly,
SectionChain or ordinary part inspection. For a canonical ordinary M1 assembly:

```powershell
aetheris inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --json --profile --repeat 2 --out artifacts/local/guitar/inspect.json
python scripts/create-guitar-x0.py --out artifacts/local/guitar
```

`--repeat` retains one explicit compilation session for 1..16 builds. JSON includes
per-build elapsed time, reuse/rebuild reasons and optional phase timings. It is not
a disk cache: placement, validation, tessellation and export still run each build.
The reproduction harness hashes its inputs and never rewrites fixtures.

Compiler-owned editor metadata covers route/Follow, keyed Sections, point recipes,
appearance/material and placement fields. Project-snapshot analysis and lexical
cross-file definition navigation support profile recipes and material aliases.
Single imported modules report that project context is needed instead of running
an unrelated part parser. Navigation does not grant private-port placement access.
Whole-project diagnostics currently retain aggregate locations; full generated-span
mapping, every draft grammar and editor-client integration are not claimed here.

## Deliberate remaining boundaries

The guitar already has physical planar seating evidence at its deck datum. No new
guitar case requires arbitrary tangent-frame evaluation, trim/seam attachment rules
or full curved-base footprint proofs. Those contracts, richer fit predicates, G2/
explicit surface laws, equation curves and higher-order geometry remain follow-ons.
The bounded source-authoring milestone does not add a general constraint solver.
