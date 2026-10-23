# Concept-directed coaxial mating

Status: implemented through the existing assembly frame solver and kinematics path.
The guitar knob stack uses Fixed axis seating. The complete-knob fixture qualifies
one root-owned Revolute with rigid children. Geometry construction remains independent.

## Design intent

Declare one Concept axis, publish each component's corresponding axis and seat
frame, then realize the components on that common datum. Do not solve a network
of pairwise cylinder coincidences. Product-tree nesting remains containment;
the Interface owns the relationship and the Mate owns its realization.

A shared axis establishes a common line. It leaves axial translation and angular
phase unspecified. Fixed placement therefore needs an axial station and clocking;
Revolute placement fixes the station but permits one angle state. A clocking
reference is authored explicitly, rather than inferred from global X or Up.

Assembly axes are three-dimensional. The existing `Axis2` is a two-dimensional
profile/pattern guide, and `Axis2D` is not the current spelling. An Axis2 may later
be lifted through an explicit construction frame; silently treating a profile
line as a spatial rotation axis would conflate two meanings. Use the existing
`Axis` name for spatial datum intent.

## Knob syntax

```firmament
Concept Struct KnobLayout {
 Axis Spindle {
  Origin: [0mm,0mm,0mm];
  Direction: [0,0,1];
  Reference: [1,0,0];
 }
}

Subassembly ControlKnob {
 <Assembly ControlKnob>
  <Part Skirt = Drum<R:12mm,H:3mm>></Part>
  <Part Grip = Drum<R:9mm,H:9mm>></Part>
  <Part Cap = Drum<R:5mm,H:1mm>></Part>
 </Assembly>

 Interface<Fixed> CoaxialStack {
  Datum: KnobLayout.Spindle;
  Members: [ControlKnob.Skirt.SpindleSeat,
            ControlKnob.Grip.SpindleSeat,
            ControlKnob.Cap.SpindleSeat];
 }
 Mate SkirtOnSpindle: CoaxialStack {
  Member: ControlKnob.Skirt.SpindleSeat;
  At: 0mm;
  Clocking: 0deg;
 }
 Mate GripOnSpindle: CoaxialStack {
  Member: ControlKnob.Grip.SpindleSeat;
  At: 3mm;
  Clocking: 0deg;
 }
 Mate CapOnSpindle: CoaxialStack {
  Member: ControlKnob.Cap.SpindleSeat;
  At: 12mm;
  Clocking: 0deg;
 }

 Anchor: ControlKnob;
 Expose {
  DatumFrame Mount = Skirt.SpindleSeat.Frame;
  Semantic Spindle = Skirt.SpindleSeat;
 }
}
```

The existing public Mount frame can remain compatible with current placement
callers. The additional composite Spindle publication carries axis and frame for
coaxial contracts. Each declared member must be realized exactly once. There is
no Placement block on these mated occurrences and no second placement authority.

Here `At` is a signed Length measured from the Concept axis origin along its
normalized Direction. It is not a world-space Z coordinate. `Clocking` is an Angle
about that direction, measured from Reference. Require an explicit At; Clocking
may default to zero. Existing Orientation spellings can remain SameDirection and
OpposedDirection, defaulting to SameDirection. The latter reverses the member's
seat-frame axis without reversing the datum's station convention.

Reference is a dimensionless radial zero-angle direction. Project it perpendicular
to Direction and normalize; reject zero, nonfinite or parallel vectors. Require
Reference for this complete-placement contract. Preserve the ordinary Axis value
as origin/direction: the assembly layout datum owns the auxiliary reference used
to construct its frame. This does not change revolve's axis meaning.

## Definition-owned ports

The current publication grammar already accepts an explicit axis and frame in
one semantic port. A Drum can publish the following intent while still using
Circle2 plus Extrude:

```firmament
Expose {
 Semantic SpindleSeat {
  Axis Axis = [0mm,0mm,0mm] -> [0,0,1];
  DatumFrame Frame = [0mm,0mm,0mm]
                    x [1,0,0] y [0,1,0] z [0,0,1];
 }
}
```

The axis is definition-owned, not guessed from an occurrence bounding box or an
arbitrary BRep face. This particular Drum has a circle centered at local origin
and extrusion along local Z, so the declared axis matches its construction.
For more general definitions, axis publication must remain explicit or derive
through a qualified construction source.

Validate that the published frame origin lies on the published axis and its Z
direction agrees with the axis. A misplaced frame must not silently become the
material axis merely because it has been named SpindleSeat. Preserve exact datum
residual evidence; distinguish it from any additional material-carrier proof.
No arbitrary-body symmetry recognition, bore-fit inference or automatic face
selection is part of this slice.

## Revolute use

Fix the knob's children to their common local spindle. In the owning control
assembly, mount the complete knob as one rotating occurrence:

```firmament
Interface<Revolute> VolumeControl {
 Datum: ControlLayout.VolumeAxis;
 Members: [Electronics.VolumeKnob.Spindle];
}
Mate VolumeRotation: VolumeControl {
 Member: Electronics.VolumeKnob.Spindle;
 At: 0mm;
 Clocking: 0deg;
}
```

ControlLayout.VolumeAxis has its own authored Origin, Direction and Reference,
or derives from an independently placed shaft's published axis/frame through a
later qualified derivation form. Clocking specifies the zero-state pose; it does
not lock a Revolute joint. Its angle state belongs to VolumeRotation and rotates
the complete knob and its children together.

One axis does not imply shared motion. Giving three separate parts Revolute
mates would create three independent angular degrees of freedom. Do not silently
couple those joints. The axis-directed Revolute contract admits one
moving component per Interface; independent coaxial rotors can declare separate
Interfaces sharing the same Concept axis. General multi-body coupling is deferred.

The moving member may be a direct Part or a direct reusable Assembly with an
appropriate exposed port. Traversal through private subassembly children remains
forbidden. Supporting direct Assembly ports is necessary here: a complete knob
should not have to be flattened to get a joint.

## Implementation and inspection

AssemblyDatumAuthoring distinguishes Plane and Axis layout datums. Axis layout
constructs an exact frame from Origin, normalized Direction and the perpendicular
projection of Reference. Existing plane seating retains its named DatumFrame At
form. Axis seating uses a required signed mm At and optional deg Clocking.

The binder validates the published Axis/Frame contract, membership, direct-owner
scope and unique placement authority. Frame authoring lowers the result to the
existing FrameCoincident solver. Fixed locks the frame; Revolute creates an ordinary
one-DOF AssemblyJointIr owned by the containing assembly. No helper Part is created.
Composite public ports rebase their nested Axis and Frame to the solved definition
coordinate system before reuse.

Inspection exposes `AssemblyIr.AxisSeats` for direct root-owned mates and
`AssemblyDefinitionIr.LocalAxisSeats` for rigid reusable definitions. Evidence names
the source Interface, datum, member, signed station, clocking, orientation and DOF.
It measures exact axis-line, seating-origin and angular residuals against the Concept
target. This proves the published datums agree; it does not infer cylinder geometry,
bore fit or surface contact. Existing plane `DatumSeats` retain their separate
material-contact checks.

Current bounded limits:

- Axis declarations admit literal Origin/Direction/Reference only. Projection from
  a published shaft is a future derivation form, not currently accepted syntax.
- Members name a direct Part or direct reusable Assembly public semantic port.
  Private child traversal is rejected.
- A datum Revolute contract has exactly one member. The state key is the Mate name.
- Reusable definitions remain rigid solved components. A Revolute inside a reusable
  definition reports `assembly-axis-internal-motion-unsupported`; put that joint on
  the complete occurrence in the root assembly. Dynamic definition propagation is
  deferred explicitly.
- No general Lower language, symmetry detector or Revolve rewrite is introduced.

## Geometry choice and qualification

Keep the present extruded circular parts for the first pass. They already express
the right simple geometry. A shaped rotary knob with an axial silhouette, shoulder
or taper could later use Revolve for clearer construction intent; switching to
Revolve must not change its mating contract. Features within one materialized
part share that part's Concept construction axis; assembly Interfaces apply to
separate occurrences, not decorative features that are already one rigid body.

The regression suite covers:

- The knob's three members share the datum axis at stations 0, 3 and 12mm.
- Translating/tilting only the datum changes placement and reuses exact bodies.
- Nonzero clocking and OpposedDirection produce deterministic proper frames.
- Missing stations, zero/parallel basis vectors, bad units, unpublished axes,
  inconsistent axis/frame ports and competing placement authorities fail typed.
- A whole-knob Revolute has exactly one angle state; its children stay rigid and
  geometry definitions remain shared when state changes.
- The real guitar retains 53 exact body definitions and 91 visible part occurrences.
  Fresh STEP and USD exports qualify the existing interchange path.

Moving the body-level knob seat locations away from their current explicit
coordinates is a separate layout refinement. This slice removes the
independent child X/Y/axis assumptions inside each coaxial component.

## Executable examples

The directly authored guitar implementation is
[`knob.firmament`](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/knob.firmament).
Its leaf names remain KnobSkirt, AmberKnob and KnobCap for appearance compatibility.
The public Mount frame remains available to its four patterned callers.

[`coaxial-knob.firmasm`](../../../fixtures/Canonical/AssemblyInterfaces/coaxial-knob.firmasm)
uses that same component on a tilted spindle with nonzero station and clocking:

```powershell
dotnet run --project Aetheris.CLI -c Release -- asm inspect fixtures/Canonical/AssemblyInterfaces/coaxial-knob.firmasm --json
dotnet run --project Aetheris.CLI -c Release -- asm export-usd fixtures/Canonical/AssemblyInterfaces/coaxial-knob.firmasm artifacts/local/coaxial-mating/knob.usda --state VolumeRotation=90 --json
```

Changing only Concept layout reuses the exact part definitions in a
FirmamentCompilationSession. Changing VolumeRotation evaluates occurrence poses
against the compiled model; its three child transforms remain rigid and no
materialization is invoked.

## Qualification evidence (2026-10-01)

- Release solution build succeeded. The fast core lane passed 1,004 tests.
- Focused axis, datum, kinematics, guitar, Gear and Difference Engine tests: 67 passed.
- Full solution gate: 4,130 passed, seven existing skips, zero failures, 20 projects.
  The gate uses one MSBuild worker and serial xUnit test collections, as in the
  preceding component milestone.
- All three local knob seats and the complete-knob Revolute have zero exact datum
  residuals. The same-session datum edit reuses all three exact body definitions.
- Every guitar position/normal/index buffer and every occurrence transform matches
  the preceding component witness exactly: 53 display definitions, 91 visible parts,
  130 occurrences including the root. The appearance and silhouette are preserved.
- AP242 export and CLI reimport succeed: 53 geometry definitions, 91 bodies and
  129 non-root occurrences (38 subassembly occurrences). The posed knob USD has one
  Revolute and two authored pose samples. The guitar USD preserves shared hierarchy.
- Running the guitar generator changes no authored files. This authoring change
  does not repeat camera-orbit or beauty-render qualification.

Generated evidence lives under ignored `artifacts/local/coaxial-mating/`:
`guitar-inspect.json`, `knob-inspect.json`, `gear-inspect.json`, `comparison.json`,
`evidence-summary.json`, `guitar.step`, `guitar.usda`, `knob-posed.usda`,
`display.json`, `knob-display.json`, `reimport/`, `generator-check.json`, and
`test-summary.json`. Final validation logs are `build-verified.log`,
`focused-verified.log`, `fast-verified.log` and `full-verified.log`.

The first full run caught a recursive public-port rebase regression in synthesized
Gear members. The correction preserves the typed Gear authority and only rebases
matching ordinary published members. The final focused and full runs above cover
that correction. The initial failure remains recorded in `full.log`.
