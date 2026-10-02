# Immutable Profile boundary edits

A Profile can start from a closed boundary or another Profile and apply named,
compile-time edits. Unedited spans retain their geometry. Replacements inherit
their endpoints, so ordinary authors do not have to write a spline control cage
or manually reconnect the loop.

```firmament
Rect2 Blank { Center: [0mm,0mm]; Size: [120mm,80mm] }
Profile Panel {
    From: Blank
    Replace Grip {
        On: Blank.Right
        Range: [20mm,60mm]
        Through: [[48mm,-10mm], [48mm,10mm]]
        Join: Tangent
    }
}
```

Use the result through ordinary Extrude, Compose or SectionChain. These edits
resolve into `ResolvedProfile2D`; they do not introduce another sketch solver,
geometry engine or runtime editing loop.

## Sources and targets

`From` is the first field, after optional comments, in a derived Profile body.
The seed is a named closed primitive or Profile. It must have one outer loop and
no inner loops in this lane. A Profile may derive from another derived Profile;
cycles fail. Explicit `Loop` authoring and existing Profile pipelines remain
available, but cannot be mixed into the same derivation block.

Rect2 provides Bottom, Right, Top and Left in counterclockwise traversal order.
Square2 and linear polygon members also supply straight carriers. Explicit
polygons retain named Set<Point2> vertices and adjacent-name edge identities.
`Using Layout` allows references to a boundary under that Concept Struct.
Circle2/Ellipse2 can be unchanged seeds; editing their periodic boundaries is
not qualified yet. Curved replacement carriers also remain deferred.

`On` selects a seed member. Range is two Lengths measured from the directed
member's start, not global coordinates or normalized parameters. It must be a
positive interval contained in a straight carrier. Omit Range to replace the
whole member. Through-points use the seed's local 2D coordinates.

One block is an atomic edit set against its immutable seed. Overlapping edits
are rejected, regardless of declaration order. Further edits to generated
members require another explicit Profile derivation. Unselected ProfileDelta
programs do not modify a closed-primitive seed implicitly.

## Reusing engineering edit templates

```firmament
Use Profile.Modifications;
Static ServiceTab: ProfileTabSpec {
    Center: 60mm; Width: 24mm; Extension: 8mm
}
ProfileDelta ServiceTabEdit = Tab<P: ServiceTab, Owner: Blank.Top>
Profile TabbedPanel {
    From: Blank
    Apply ServiceTabEdit
}
```

Apply reuses the existing semantic ProfileDelta parser/resolver, including its
anchor, levels, spans and transition validation. Outward means away from the
counterclockwise region's material interior. Generated members retain names such
as `TabbedPanel.ServiceTabEdit.Crown`; a subsequent Profile can select that
straight member. Sheet Metal retains its manufacturing/domain authority and
formed/flat correspondence. Its existing implicit attachment syntax is preserved.
CornerProfile authoring remains on its existing route; Apply of a corner program
is not added by this milestone.

The complete [panel fixture](../../../fixtures/Canonical/Boundary2/edited-panel.firmament)
combines a templated tab with a smooth grip and builds through Aetheris.CLI.

## Smooth curves and explicit shape control

Through describes a finite open cubic Hermite chain. The inherited endpoints
are included automatically. For points p0..pn, let hi be the distance between
pi and p(i+1), and si = (p(i+1)-pi)/hi. Endpoint derivatives default to s0 and
s(n-1). Interior derivatives are:

```
di = (hi * s(i-1) + h(i-1) * si) / (h(i-1) + hi)
```

Each interval uses controls `pi + hi*di/3` and `p(i+1) - hi*d(i+1)/3`.
Chord parameter intervals make the shared derivative direction continuous;
this is a fixed interpolation rule, not a shape optimizer. Overshoot is possible
and resulting intersections are checked.

`StartTangent` and `EndTangent` are finite nonzero dimensionless direction
vectors; they replace the corresponding endpoint derivative direction.
`Join: Position` is the default. `Join: Tangent` obtains directions from the
source carrier and verifies both realized joins against the neighboring result
curves. A sharp corner cannot silently satisfy a tangent join.

For preserving a designed curve exactly, supply explicit Length-valued
Derivatives, one for every knot **including both inherited endpoints**:

```firmament
Replace Grip {
    On: Blank.Right
    Range: [20mm,60mm]
    Through: [[48mm,-10mm], [48mm,10mm]]
    Derivatives: [[0mm,20mm], [-10mm,15mm], [10mm,15mm], [0mm,20mm]]
}
```

These derivatives use a unit parameter interval per cubic, so controls are
`pi + di/3` and `p(i+1) - d(i+1)/3`. They cannot be combined with tangent fields
or Join: Tangent. Through: [] admits a single cubic between inherited endpoints.
Repeated adjacent points, zero derivatives/tangents, cubic cusps and individual
cubic self-crossings are rejected. There are at most 1,024 knots per replacement
and 1,024 resulting segments in an edited loop.

## Validation, identities and inspection

Validation checks closure, winding, coincident overlap, inter-span crossings
and extra contacts, including adjacent spans away from their shared endpoint.
Pair checks reuse the shared bounded line/arc/cubic intersection kernel at
1e-7 mm tolerance. Individual cubic loops and stationary tangents have polynomial
checks. Curved intersection search uses bounded convex-hull subdivision and
numerical parameter refinement; this is tolerance-based qualification, not a
certified global algebraic simplicity proof. Unsupported edited curve families
fail rather than being converted to a display polygon.

New curve names are `Grip_Span0`, `Grip_Span1`, etc. Retained fragment names use
their bounding named edits rather than a global counter. Editing one member
does not rename unrelated members. Consumed names remain source provenance;
they are not redirected to whichever child happens to be nearest.

Resolved Profiles carry BoundaryEdits with source members, normalized selected
intervals, construction methods, descendants and source identity. SectionChain
CLI inspection exposes these as `boundaryEdits` per section, alongside Concept
derivations. Normalized evidence intervals are distinct from Length-valued
authored Range. Existing SectionChain correspondence still owns span matching.

## Guitar dogfood and reproduction

[body-outline.firmament](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/body-outline.firmament)
now uses six named landmark chords and six edits: LowerTreble, HornRise, Cutaway,
Shoulder, UpperBass and LowerBass. Its source is 45 lines instead of 93, with no
separate point/control/Segment declarations. Explicit derivatives preserve the
previous 18 cubic spans. Carve, back and binding still derive from BodyOutline;
their seam is now the named `LowerTreble_Span0`.

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/Boundary2/edited-panel.firmament --out artifacts/local/profile-loop-implementation/panel.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll section-chain inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/CarvedMaple.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm export-ap242 fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --out artifacts/local/profile-loop-implementation/guitar.step --json
```

Fresh before/after carve exports contain 2,324 corresponding Cartesian points
each. Maximum point displacement is 4.68e-7 mm, attributable to replacing the
old six-decimal control coordinates with explicit Hermite derivatives. This is
numerical surface-control parity, not byte-identical STEP: semantic names changed.
Fresh USD has 53 shared geometry definitions and 91 visible product occurrences.
Mesh checks found no unmatched edges or nonpositive normal triangles in the
qualified guitar definitions. Cycles hero/isometric output is regenerated from
that USD; shading preserves imported product topology.

Release solution build passed. The fast core lane passed 1,004 tests; the final
full serial lane passed 4,096 tests with 7 existing skips and no failures. The
19 focused authoring cases cover pure seed preservation, real extrusion,
templated Apply, overlapping edits, invalid units/targets, tangent mismatches,
individual cubic loops/cusps and rejected periodic edits. STEP reimport preserves
53 geometry definitions, 91 bodies and 35 subassembly occurrences.

One host run measured cold compilation at 13.514 seconds, warm uncached at
6.766 seconds, and populated-session recompilation at 1.599 seconds. Display
preparation was 0.539 / 0.516 / 0.368 seconds respectively. The incremental run
reused all 53 definitions and produced byte-identical USD to the same-run uncached
baseline. Process startup/build are excluded; tests/rendering also ran on this
host during qualification, so these are not isolated comparative benchmarks.
Camera interaction was not remeasured by this authoring milestone.

Both inspected Cycles images are 1,800 by 2,200 at 64 samples on an RTX 3070:
hero rendering took 26.5 seconds, shaded isometric 21.2 seconds. Their topology
hash checks passed. The refreshed images retain the presentation witness.

Generated evidence, STEP/USD and renders live under ignored
`artifacts/local/profile-loop-implementation/`: final-build.log, final-fast.log,
final-full.log, final-carve-inspection.json, carve-parity.json, timings.json,
mesh-check.json, step-reimport.json, guitar.step, guitar.usda, beauty/hero.png,
isometric/shaded-isometric.png and their saved Blender scenes/render reports.

## Future equation-driven curves

Curve construction has a typed internal payload seam. A future bounded Equation
payload can produce the same resolved curves as Through/Hermite without adding
an executable callback to Profile or a runtime dependency to exported geometry.
No Equation syntax or C# evaluation is implemented here; such requests fail.

The intended extension is compile-time only: a declared parameter interval,
Length-valued 2D result, pure arithmetic and approved math intrinsics, bounded
evaluation/approximation work, and explicit dependencies for cache identity.
If lowering to C#, validate the restricted program before compilation. No I/O,
reflection, arbitrary CLR calls, dynamic code loading, mutable static state,
async work, recursion or unbounded loops. Do not treat running arbitrary C# as
a purity check.

Recognized polynomial/analytic laws should preserve exact representations.
Other equations need an explicit approximation tolerance, admitted representation
and reported error bound before they can be materialized. Exact BRep geometry of
an approximant must not be reported as an exact representation of the equation.
After equation binding, the ordinary boundary-edit validity checks still apply.
See the [larger design proposal](programmable-profile-loops-proposal.md) for
deferred periodic intervals, Curve2 payloads, inner loops and Profile-valued Features.

## Owl authoring update

The body witness now uses Derivatives: Periodic to derive cyclic Hermite tangents from ordered replacement knots. Straight seed carriers and full replacements only; ranges and non-straight carriers are rejected. Existing exact loop validation remains authoritative. See [source-owned guitar authoring](source-owned-guitar-authoring.md).
