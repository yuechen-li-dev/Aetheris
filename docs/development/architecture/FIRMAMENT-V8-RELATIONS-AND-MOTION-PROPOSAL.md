# Firmament V8: shared datums, articulated assemblies and keyframes

Date: 2026-10-16. Status: **approved design; partially implemented in the production compiler**.
Reviewed checkout: `9cf7b4af`.

## Decision

Represent the engine as reusable components connected to shared design datums,
with motion expressed through typed joint coordinates and finite keyframe curves.
The compiler derives dependent poses. Authors do not supply rod/piston rest
coordinates, trigonometric basis components or per-Part animation matrices.

Keep the existing Part/Subassembly/Assembly/Scene hierarchy, Inventory Records,
Concept Structs, Interfaces, Mates and finite Patterns. Extend their current AST
and semantic owners. Do not introduce a V8 parser, a second assembly solver or a
general simulation language.

This is one production refactoring pass, implemented in dependency order. It is
not another shadow-only milestone. The examples below are proposed design
fragments, not complete programs that current Firmament accepts.

## What the current source actually does

The current project already uses one CylinderUnit template and one keyed Pattern
for eight occurrences. The old eight-file duplication is gone. The remaining
friction is elsewhere:

| Evidence | Consequence |
| --- | --- |
| `assemblies/cylinder.firmament`: 74 lines, 19 Mate declarations and five stack Interfaces | The piston, wrist pin, lugs, valve stem and retainers are seated independently rather than moving as complete rigid components. |
| `rest-configurations.firmament` and `CylinderRest` | Source carries precomputed rod coordinates, sine/cosine values, piston position, valve lift and spring pitch. These are derived mechanism state, not independent design inputs. |
| `engine.firmasm` and `assemblies/crankshaft.firmament` | Common bank/shaft frames are partially present, but repeated bases such as `0.7071067811865476` still leak into placement declarations. |
| `EngineProject.Create` | C# serializes derived rest values back into Firmament using round-trip numeric spelling. |
| `EngineMotion.cs` and `web/engine-motion.mjs` | The demo classifies and animates individual Parts outside the authored joint graph. The two adapters share primitive motion laws but retain V8-specific transform logic. |
| Current `asm inspect` result | 193 occurrences: 171 Parts and 22 Assembly nodes. Twelve joints, all Fixed; no Revolute or Prismatic joints. |
| `AssemblyM0Parser` reusable-definition admission | Internal Revolute frame placement is explicitly rejected with `assembly-axis-internal-motion-unsupported`: reusable children are currently rigid. |
| `AssemblyM0Compiler` / `AssemblyKinematics` | Ordinary joints form a directed placement graph; unresolved kinematic cycles are rejected. A slider-crank needs an explicitly admitted closure relation. |

The 41 Firmament-family files contain 976 lines. Line count alone is not the
acceptance metric: meaningful geometry declarations should remain. The target is
removing duplicated placement and motion authority.

Read-only reproduction:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/EditableV8/engine.firmasm --json --out artifacts/local/v8-declarative-proposal/current-assembly.json
```

This inspection succeeded without diagnostics. It is current-state evidence, not
qualification of the proposed features. The demo generator was not run and no
source or geometry was regenerated during this design pass.

## 1. Shared assembly scaffolding

The engine should own one Concept Struct containing its shaft frame, bank frames,
deck planes, cam frames and keyed cylinder installation frames. Components keep
their intrinsic local datums and publish meaningful ports. Do not make a reusable
component capture an enclosing assembly's names implicitly.

Extend derived DatumFrame authoring to use the existing frame-composition
semantics directly:

```firmament
Concept Struct EngineLayout {
    DatumFrame Shaft { from: World }
    DatumFrame RightBank {
        from: Shaft
        rotateLocal: { axis: Z; angle: -45deg }
    }
    DatumFrame LeftBank {
        from: Shaft
        rotateLocal: { axis: Z; angle: 45deg }
    }
    DatumFrame RightCam {
        from: RightBank
        translateLocal: [0mm, d.camHeight, d.camStation]
    }
}
```

This example assumes local +Y is the bore direction and +Z the shaft direction.
Under right-handed rotation, rotating +Y toward +X uses -45deg about +Z. The
actual migration must account for the current first-main-journal origin and its
shaft station; the sketch is not permission to move that origin.

Translations are expressed in the referenced `from` frame, consistent with
current FrameTransform behavior. A rotation does not silently rotate a sibling
translation. When radial placement needs the rotated basis, derive the bank or
throw frame first, then place relative to it. Planes can reference these frames;
a Plane alone does not define an in-plane origin and clocking for a rigid body.

Extend the existing finite Pattern owner to produce datum families inside Concept
Structs. Explicit keyed addressing should connect the datum and occurrence
families without constructing path strings:

```firmament
Concept Struct EngineLayout {
    // Shaft, RightBank and LeftBank declarations precede this fragment.
    Pattern CylinderSeats over cylinderSites {
        (key, site) => DatumFrame Mount {
            from: site.bank
            translateLocal: [0mm, 0mm, site.station]
        }
    }
}
```

The matching occurrence Pattern consumes `EngineLayout.CylinderSeats[key].Mount`.
The two-name binding exposes the existing Set key separately from its row. It is
an extension of finite Pattern binding, not a new loop executor. Typed keyed
addressing must retain stable identities under Set reordering.

Record/Set fields that carry a bank datum or actual journal must bind typed
semantic references, not strings or copied matrices. Reuse the existing
structural Concept contracts to describe a journal port's Axis/Frame/Dimension
requirements. Preserve the real Part-owned journal diameter and the existing fit
checks. An assembly datum is not a replacement for physical geometry authority.

## 2. Assemble complete rigid components

Introduce no new rigid-group keyword. Use existing Subassemblies:

- Piston assembly: crown, skirt, lugs and wrist pin; one public pin/mount contract.
- Valve assembly: valve, stem and retainer; one guide coordinate for the complete
  moving component. Intake and exhaust reuse a definition where geometry permits.
- Crank throw: paired pin and two webs; a keyed Pattern places four complete
  throws by station and angle. Main journals use their own finite row Pattern.
- Cylinder unit: fixed sleeve/head, piston assembly, rod, two valve assemblies
  and two springs.

Fixed component offsets are declared once, against their defining local datums.
The engine still owns the bank and installation layout. Each moving component
has one pose authority; its rigid children inherit that pose.

Split geometry inputs from installation/motion inputs. A cylinder's bank,
journal reference and firing phase should not specialize its geometry template.
The eight installation rows should retain meaningful choices such as bank,
station, actual journal and ignition angle. Remove `rodX`, `rodY`, `rodSin`,
`rodCos`, `pistonY`, precomputed lift and precomputed pitch from author input.

Use one typed EngineSpec for independent engineering dimensions. Derived values
such as crank radius, deck height and the crank-web envelope should remain
checked expressions in their defining owner. A JSON/CLI adapter may still supply
the independent specification; it must not generate a second rest-pose program.
Qualify any missing bounded math intrinsic through the existing typed value
owner, rather than adding another expression evaluator or rounding the result.

## 3. Articulated Subassemblies and public relation ports

This is the main IR change, not syntax sugar. Reusable definitions must retain
their local joint/relation graph as well as their static geometry and zero frames.
Instancing creates independently keyed joint state and bindings for each
occurrence, while immutable geometry and relation definitions remain shared.

Extend Expose to publish typed Interface contracts and realized Mate channels.
A reusable Interface may bind its local roles and leave explicitly required
external roles for its parent to realize. For example, a CylinderUnit publishes
its slider-crank contract with local rod/piston/guide participants already bound;
the parent supplies the actual crankpin:

```firmament
Pattern Cylinders over cylinderSites {
    (key, site) => {
        <Assembly Cylinder = Units.cylinder<spec: cylinderSpec>></Assembly>
        // Existing Fixed seating installs Cylinder.Mount at the matching
        // EngineLayout.CylinderSeats[key].Mount datum.
        Mate Drive: Cylinder.Drive { crankPin: site.journal }
    }
}
```

`Cylinder.Drive` is a proposed exported relationship contract, not a private child
path. The Mate completes one realization; it must not create a second placement
driver for the same mechanism. Required external roles can remain open only in a
reusable definition. Building an occurrence with an unbound role fails.

The same boundary exposes named joint coordinates such as `Intake.translation`.
Animation targets the realized Mate instance's stable ID, never a shared
Interface definition name or an ambiguous leaf name. Repeated cylinders own
independent channels even when they reuse one relationship definition.

Pose evaluation must propagate a moving parent's transform through rigid
descendants before a dependent joint or closure reads their ports. In particular,
a crankpin nested inside a rotating crankshaft cannot be read at its old rest
world position. Evaluation is a dependency graph over occurrence frames and
relation outputs, not a late pass that overwrites world matrices.

## 4. Kinematic mates, including bounded closure

Retain Fixed, Revolute and Prismatic. Add a bounded `Interface<SliderCrank>` family
whose Mate binds actual ports:

```firmament
Interface<SliderCrank> PistonDrive {}
Mate Stroke: PistonDrive {
    crankPin: Crankshaft.Pin1
    rodBigEnd: Rod.BigEnd
    rodSmallEnd: Rod.SmallEnd
    sliderPin: Piston.Pin
    guide: Layout.Bore
    branch: Extended
}
```

This is a one-cylinder fragment; the reusable version binds local roles inside
CylinderUnit and receives the crankpin through its public contract.

Reuse `Core.Mechanisms.SliderCrank` as the analytical authority. Derive the crank
radius and rod center distance from checked published ports/dimensions; do not
ask authors to repeat geometry as an unrelated motion specification. Admit the
current in-line geometry and its explicit Extended branch. Validate compatible
axes, guide alignment, axial seating and the domain `L > r > 0`. Other mechanisms
and branches remain rejected until deliberately qualified.

The crank rotation is an independent coordinate. Rod orientation and slider
position are outputs of this relation. Do not independently keyframe the piston
and then hope its wrist pin still meets the rod.

Retain ordinary cycle rejection. A SliderCrank is one admitted closure node with
known inputs, outputs and residual checks; enabling it is not permission to
remove `assembly-kinematic-loop-unsupported` for arbitrary cycles.

Cam rotation uses the existing prescribed 1:2 angular relationship. A proposed
typed `drive` field on a realized Mate may derive its free coordinate, for
example `drive: Crank.angle / 2`. This is a prescribed ratio, not a claim that
physical timing gears or cam contact were modeled.

## 5. Declarative keyframes

Add `Motion`, `Track`, `Keyframe` and reusable `Keyframes<Domain, Value>` values.
They share one typed curve/interpolation owner. Add Time with seconds and
milliseconds to the existing units/binding system rather than parsing time as an
untyped Float special case.

```firmament
Motion FourStroke {
    duration: 2s
    repeat: Loop
    Track CrankRotation {
        target: Engine.Crank.angle
        interpolation: Linear
        Keyframe Start { at: 0s; value: 0deg }
        Keyframe Finish { at: 2s; value: 720deg }
    }
}
```

`Engine.Crank` denotes a realized Revolute Mate, not a mesh or Interface type.
Two authored keys drive the whole mechanism. Playback rate changes how the
consumer advances clip time; it does not change geometry or the motion law.

A valve lift profile is keyed by crank phase, not by a second independent clock:

```firmament
Keyframes<Angle, Length> IntakeLift {
    period: 720deg
    interpolation: Cosine
    Keyframe Rest { at: 0deg; value: 0mm }
    Keyframe Opening { at: 360deg; value: 0mm }
    Keyframe Peak { at: 450deg; value: 9mm }
    Keyframe Closing { at: 540deg; value: 0mm }
    Keyframe CycleEnd { at: 720deg; value: 0mm }
}
```

Two half-cosine segments through 0 → lift → 0 reproduce the current sin² lift law
mathematically. Equal-valued intervals remain constant. Exhaust uses its existing
180deg opening; per-cylinder ignition offset comes from meaningful firing data.
Do not replace the current law with unspecified smooth interpolation.

A Track can reference a reusable profile with `values: IntakeLift` and
`input: Crank.angle - site.ignition`, targeting the exposed valve translation.
Its sign follows the declared guide axis; the current outward-positive guide
requires negative translation for inward valve lift. Motion-scope Patterns reuse
the same finite keyed expansion to bind the two valve channels for each cylinder.

Rules:

- Track domains are Time or Angle in the first pass; values are typed Angle or
  Length. Inline time keys infer their value type from the target joint.
- Interpolation is explicit: Linear, Hold or Cosine initially. No implicit cubic
  overshoot, arbitrary runtime callbacks or per-frame source evaluation.
- Preserve unwrapped angles: 0deg → 720deg is two revolutions, not identity and
  not a shortest-path quaternion interpolation.
- Loop wraps clip time. Preserve the final authored key for endpoint sampling;
  validate continuity of the physical pose and dependent channels at the seam.
  Unwrapped angular endpoint values may differ by complete turns.
- Key domains are checked, finite, ordered and unique. Periodic profiles include
  their domain endpoints explicitly. Profile input wraps by its declared period.
- One active writer per independent coordinate. A Track, Mate drive expression
  and closure cannot simultaneously own the same output. Fixed coordinates and
  closure outputs are not writable animation targets.
- Scrubbing in arbitrary order must give the same pose. No accumulated matrix
  updates or previous-frame solver state.

## 6. Springs need a separate, explicit deformation relation

The current viewer compresses a WireForm coil along its published winding axis.
A Prismatic joint can move a spring but cannot change its length. Do not conceal
that difference behind a rigid pose transform.

Propose a bounded `Interface<AxialSpring>` relation binding the actual Winding,
fixed seat and moving retainer seat. It derives compression from those ports,
retains the fixed seat, and uses the existing qualified axis-aware presentation
deformation. Free length, turn count and clear diameter belong to the spring's
published geometry, not another V8-specific manifest table.

At a baked engineering pose, specialize the existing WireForm using the derived
pitch and materialize a real coil. In particular, phase zero already contains
compressed springs in the current engine: replacing them with free coils would
change the existing STEP geometry. A visual scale is not an exact posed STEP.
If exact pose baking for a requested deformation is unavailable, report that
boundary instead of exporting undeformed geometry as if it matched the pose.

No spring force, stress, contact or dynamics is implied. This family is an
explicit prescribed geometric/presentation relation.

## 7. One compiled authority, multiple consumers

Compile the new syntax through the unified AST into typed datum, joint, closure
and Motion IR. Retain source spans, definition/occurrence identities and a finite
dependency graph. Reuse the existing kinematic and mechanism owners, extending
their graph admission; no source-text motion expansion or regex dispatch.

Default/rest pose is a named deterministic query of this graph. It replaces the
current author-supplied CylinderRest table. Generated matrices and sampled curves
are legitimate artifacts; they should not become authoring source again.

Native CLI and the browser's existing WASM runtime evaluate the same compiled
graph. The viewer owns its clock, scrubbing and drawing, not a switch over V8
Part kinds. Load/cache the compiled graph once; do not parse or rebuild BRep every
frame. Retire demo-specific transform/binding logic only after parity is proven.

STEP exports a selected static pose, with real geometry for any baked deformation.
USD uses existing joint/state sample support with an explicit seconds-to-timecode
conversion. GLB animation, when emitted, must retain intermediate rotations so
two endpoint quaternions cannot erase a 720deg turn. If an output cannot retain
the motion contract, diagnose it or export an explicitly selected static pose.

This remains compile-time Firmament authoring that emits static geometry and a
finite animation description. Playback evaluates a bounded artifact; Firmament
does not become a general runtime or a dynamics simulator.

## Implementation order and acceptance

1. Qualify shared derived frames and typed keyed datum references. Refactor bank,
   crank throw and cam placement against the existing rest-pose geometry.
2. Retain articulated local graphs in reusable definitions and bind public
   Interface/Mate ports per occurrence. Qualify parent/child pose propagation.
3. Admit the bounded SliderCrank and axial spring relations through the current
   engineering owners, with closure residuals and exact default-pose baking.
4. Add typed keyframes and one coordinate-driver graph. Connect existing native,
   WASM and export consumers to it; remove V8-specific animation ownership.
5. Re-author V8 around the shared layout, rigid components and meaningful keyed
   cylinder data; update syntax.md only for syntax that actually passes production
   admission. Keep this proposal separate from the accepted-language tutorial.

Completion requires real production-path evidence:

- One reusable CylinderUnit source instantiated eight times; no authored
  CylinderRest data, basis sine/cosine literals or per-Part motion bindings.
- All 171 physical Parts accounted for. Additional rigid grouping may change
  Assembly-node counts and paths; explicitly map those hierarchy changes.
- Preserve each Part's engineering geometry, world placement at the current
  default pose, fits, spring winding axes and explicit PMI/provenance. Compare
  definition geometry and posed bounds/topology; do not demand a byte-identical
  whole-assembly STEP after an intentional product-tree change.
- Derive dimensions from authored inputs rather than round-trip G17 literals.
  Do not globally round geometry, change tolerances or hide classified numerical
  differences behind a widened test threshold.
- Sweep the existing baseline and +6mm-stroke variant through 0–720deg at the
  established 1deg stations. Preserve rod closure, firing TDC, valve timing,
  spring seat/axis checks and the existing bounded clearance checks.
- Check non-key times, reverse/random scrubbing, loop endpoints, nested moving
  parents and independent state for repeated cylinder occurrences.
- Reject conflicting writers, missing roles, unit errors, unsupported mechanism
  geometry and arbitrary cycles with precise source diagnostics.
- Qualify native/WASM pose agreement and actual browser playback, plus exact
  default/selected-pose STEP and the advertised animation export paths.
- Run focused checks and the complete serial .NET regression, then recheck
  canonical assemblies. Preserve shared definitions and reap task runtime hosts.

The practical target is one shared engine layout, four crank-throw rows, eight
cylinder installation/firing rows, one reusable articulated cylinder, and one
keyframed crank driver. Rod/piston positions, cam rotation, valve motion and
spring compression follow declared relationships. No line-reduction percentage
or general collision certification is promised before implementation.

## Original design-pass outcome

**Success as a design pass:** current source and production inspection identify
the actual friction and the missing owners. This document makes the language and
IR decisions concrete. No compiler, model, geometry or motion implementation was
changed. No .NET build/test qualification of proposed syntax is claimed.


## Implementation checkpoint

Shared derived frames, articulated local joint retention, moving exposed
geometric ports, typed finite clips and cached native/WASM sampling now have
production implementations. Syntax admitted by this checkpoint is documented in
`docs/public/firmament/syntax.md`; the rest of this design remains proposed.
The V8's common cam datums use the shared-frame path. Its closed mechanism,
external Interface roles and exact spring deformation have not migrated.
See `docs/release/FIRMAMENT-V8-DECLARATIVE-MOTION.md` for qualification and the
specific remaining ownership boundary. This is not acceptance of the complete
V8 refactor described above.
