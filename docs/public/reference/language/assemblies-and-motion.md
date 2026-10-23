# Assemblies and motion

An assembly lists occurrences of parts and subassemblies and the relationships
that place them. [Lesson 6](../../learn/06-assemblies.md) builds a complete
two-part assembly; the [assembly guides](../../firmament/assemblies.md) cover
stack-ups, reusable subassemblies and keyed component sites.

## Occurrences

The tree is written with `<Assembly>` and `<Part>` tags (brace bodies are also
accepted). Each `<Part Name = Definition<args>>` is one occurrence;
occurrences of the same specialized definition share one exact body. Nesting
describes product structure only. It never creates a mate.

`anchor:` fixes an occurrence in place. A `Placement { from: ...; to: ...; }`
block places an occurrence explicitly: `to: Parent` is relative to the parent
occurrence and `to: World` is the assembly root. See
[assembly authoring](../../firmament/assembly-authoring-foundation.md).

## Ports, interfaces and mates

Parts publish ports with `Semantic` blocks, usually inside `expose`. A port is a
reference such as a `DatumFrame`, `Axis` or `Dimension`, never extra material.

A typed interface family states what two ports must satisfy. Declare a reusable
contract once and apply it with a named `Mate`:

```firmament
Interface<Fixed> Installation { }
// Inside the consuming Assembly:
Mate InstallBracket: Installation {
    A: Machine.Frame.Mount;
    B: Machine.Bracket.Mount;
}
```

Or apply it inline, as in lesson 6:
`Interface<Fixed> Install { a: Machine.Frame.Mount; b: Machine.Bracket.Mount; }`.

- `Fixed` needs ports with datum frames and makes them coincide. `Revolute` and
  `Prismatic` leave one rotation or translation free; they are the joints motion
  can drive.
- `A` and `B` are role names, written as the family declares them. The inline
  form also accepts lowercase `a` and `b`.
- A Mate is applied at compile time. It is not a runtime call.
- Unknown clauses, duplicate assignments and unresolved roles reject.

Older source uses custom `Interface` blocks with `Role ... requires ...` and
`Lower ...` clauses. They still compile, but new relationships should use the
typed families, and `Interface<Custom>` only when explicit low-level relations
are really needed.

## Shared datums and articulated motion

An assembly `Concept Struct` can publish derived three-dimensional frames. Use
`from`, `translateLocal` and `rotateLocal` rather than authoring trigonometric
basis components:

```firmament
Concept Struct Layout {
  DatumFrame Shaft { from: World; translateLocal: [3mm,4mm,5mm] }
  DatumFrame Bank { from: Shaft; rotateLocal: { axis: Z; angle: -45deg } }
  DatumFrame Cam { from: Bank; translateLocal: [0mm,100mm,20mm] }
}
```

A local name such as `Bank` resolves within this Concept Struct. A qualified
name can reference another declared datum. Translation is expressed in the
referenced frame. A sibling `rotateLocal` changes orientation without rotating
that translation; derive an intermediate frame when translation should follow
its rotated basis. Required lengths and angles are dimension checked. Unknown
fields, unresolved sources and datum cycles reject. The existing `DatumFrame`
form with `on`, `at` and `X` on a Plane remains supported.

Reusable Subassemblies retain their local Fixed, Revolute and Prismatic joint
graphs. Occurrences share geometry and own independent coordinates. Exposed
geometric ports follow their actual child occurrence when it moves. At the
compiler API boundary, a nested realized Mate is qualified by occurrence, for
example `Articulated.First.Elbow`; a root Mate keeps its declared name. Names in
motion source address those realized Mates, not the shared Interface type.
Reusable definitions publish their motion coordinates explicitly, for example
`expose { Mate Bend = Elbow; }`. A parent Track uses
`Articulated.First.Bend.angle`; the private local `Elbow` name is not a parent
Motion target. A wrapper can re-export a nested public channel with
`Mate Bend = Unit.Bend;`. These aliases retain the realized Mate's stable ID,
so two public aliases cannot become independent writers for one coordinate.

Declare `Motion` and reusable `Keyframes` in the root Assembly or its module:

```firmament
Motion Cycle {
  duration: 2s
  repeat: Loop
  Track Rotation {
    target: Machine.Crank.angle
    interpolation: Linear
    Keyframe Start { at: 0s; value: 0deg }
    Keyframe Finish { at: 2000ms; value: 720deg }
  }
}
```

This fragment requires an admitted Revolute Mate named `Crank` in `Machine`.
`Time` is a compile-time scalar with `s` and `ms`, normalized to seconds; it can
flow through checked Record fields. Time is distinct from Length and Angle.
A Revolute track writes `angle` in degrees; a Prismatic track writes
`translation` in millimetres. Angles remain unwrapped: 720deg means two turns.
Inline keys use Time, start at zero and end at the declared duration.

Reusable curves declare their domain and value dimensions explicitly:

```firmament
Keyframes<Angle,Length> Lift {
  period: 720deg
  interpolation: Cosine
  Keyframe Rest { at: 0deg; value: 0mm }
  Keyframe Open { at: 360deg; value: 0mm }
  Keyframe Peak { at: 450deg; value: 9mm }
  Keyframe Close { at: 540deg; value: 0mm }
  Keyframe End { at: 720deg; value: 0mm }
}
```

A Track can use `values: Lift` with `input: Machine.Crank.angle`. Input
expressions use the existing typed arithmetic owner; for example, negate the
input for a clockwise crank coordinate or divide an angular coordinate by two.
Reusable Time curves use clip time when input is omitted. Curve domains are
Time or Angle; values are Angle or Length. Interpolation is
explicitly Linear, Hold or Cosine. A periodic curve includes zero and period
keys with equal endpoint values. Without `period`, a curve holds its endpoint
values outside its key domain.

Keys must have unique names and strictly increasing finite domain values.
`repeat: Once` clamps clip time; `repeat: Loop` wraps it, including negative
scrub times, and requires matching physical poses at the clip seam. Compiler
sampling can preserve the final endpoint explicitly. Each independent Mate has
one Track writer per clip. Unknown targets, Fixed targets, conflicting writers,
incorrect dimensions and driver cycles reject with source diagnostics.
Sampling is stateless and retains shared geometry; it does not accumulate the
last frame's transforms.

The complete executable witness is
[`articulated-motion.firmament`](../../../../fixtures/Canonical/AssemblyInterfaces/articulated-motion.firmament).
Inspect or sample it with `aetheris asm inspect` and
`aetheris asm motion <file> --clip Cycle --time 1.25 --json`.

These tracks prescribe admitted rigid joints. They do not yet admit SliderCrank
closure, deform a WireForm spring, export an open Interface contract from a
Subassembly, or create Motion-scoped Patterns. The editable V8 still uses its
existing qualified mechanism adapter for those relationships. A static STEP
export is the compiled engineering configuration; sampling a clip does not
silently replace it with a different posed artifact.


## A complete mechanism

The [editable V8](../../firmament/editable-mechanisms.md) is a complete Inventory/Part/Subassembly/
Assembly/Scene project. Its construction recipes and product structure are authored
Firmament; typed Records carry dimensions and scalar rest inputs. One `Template<config:
CylinderConfig> Subassembly CylinderUnit` is instantiated eight times by keyed
`Pattern` entries derived with immutable `with`. Pattern mapping blocks admit
occurrences with local Concept Struct, Interface and Mate declarations; each key
owns their scope. No per-Part pose tables are required. Assembly
placements bind scalar Static/Record fields through the same checked value owner.
Assembly-owned Semantic datums bind in the owning occurrence; concrete WireForm
winding ports remain available across module boundaries. See the guide for static
Scene versus prescribed-motion limits and the intentional hierarchical identity
change from the old generated flat source.
