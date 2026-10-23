# Requirements and interfaces

This page covers how a part states what must be true of it: dimensional
tolerances, compile-time conditions, structural contracts, and the named
engineering interfaces other parts and tools refer to.

## Tolerances

A tolerance is a requirement attached to a dimensional value:

```firmament
Model TolerancedPlate {
  units: mm
  Static drillDiameter: Length = 8mm tol +0.10mm -0.05mm
  Box Body { size: [40mm, 30mm, 8mm] }
  Modify Body {
    Hole<Shaft> Mount {
      on: +Z
      center: Point2(0mm, 0mm)
      diameter: drillDiameter
      end: ThroughAll
    }
  }
  Pmi {
    HoleDiameter MountSize { target: Mount dimension: drillDiameter }
  }
}
```

- `tol 0.1mm` means ±0.1 mm. `tol +0.10mm -0.05mm` gives separate upper and
  lower allowances. Allowances are finite, non-negative, may be zero, and use the
  value's unit: mm for `Length`, deg for `Angle`. Tolerances on Int, Float, Bool
  or String reject.
- `tol` applies to the whole initializer, after any arithmetic.
- **Geometry uses the nominal value.** A tolerance never loosens the kernel's
  geometric precision.
- **Publishing is explicit.** Declaring a tolerance emits nothing by itself;
  `Pmi` projects it into AP242. See [PMI](../../firmament/pmi.md) for targets and
  export limits. Angle allowances can be bound and inspected, but angular PMI
  export from a Model is not supported yet.
- **Arithmetic drops allowances.** A direct alias keeps the tolerance. An
  arithmetic result is nominal, and the compiler warns that it dropped an
  allowance. If the result is a new requirement, give it its own `tol`. There is
  no inferred interval propagation and no tolerance-stack certification in
  scalar arithmetic; assembly stack-ups are a separate, explicit check (see
  [checks](checks.md#tolerance-stack-ups)).

Requirements travel through Records, Template parameters and Feature arguments
and locals:

```firmament
Record BoreSpec { diameter: Length }
Static production: BoreSpec { diameter: 8mm tol +0.10mm -0.05mm }

Template<spec: BoreSpec> Model PlateFamily {
  units: mm
  Feature Drill(spec: BoreSpec) -> Hole<Shaft> {
    let inspection: Length = spec.diameter
    return Hole<Shaft> {
      on: +Z
      center: Point2(0mm, 0mm)
      diameter: inspection
      end: ThroughAll
    }
  }
  Box Body { size: [40mm, 30mm, 8mm] }
  Modify Body { Feature Mount = Drill(spec: spec) }
  Pmi { HoleDiameter MountSize { target: Mount dimension: spec.diameter } }
}
Model ProductionPlate = PlateFamily<spec: production>
```

Passing a scalar field or a whole Record keeps the requirement, as do nested
Records, `with` overrides, Template defaults and nested specialization. Feature
locals stay local: PMI in the enclosing Model names a Static or Record value it
can see. `validate --json` reports where each requirement came from and how it
was bound.

Where tolerances are not accepted:

- inline on a geometry field (`diameter: 8mm tol 0.1mm` rejects; name the value
  with a Static or Record field instead);
- in Table cells and other collection elements;
- in scalar positions of domains that take their inputs through typed Templates,
  such as Profile/Compose recipes, Sweep and WireForm. These reject unsupported
  scalar consumers rather than discarding the allowance.

## Conditions: `Concept` and `Require`

A predicate `Concept` is a named yes/no check over compile-time values. A
bundle `Concept` names several checks, each with a label. `Require` applies one:

```firmament
Concept Positive(value: Length) => value > 0mm

Concept PlateRequirements(width: Length, height: Length) {
  positiveWidth: Positive(width)
  positiveHeight: Positive(height)
  aspect: width >= height
}

Template<width: Length, height: Length> Model Plate {
  units: mm
  Require dimensions => PlateRequirements(width, height)
  Box Body { size: [width, height, 5mm] }
}
Model Product = Plate<width: 40mm, height: 20mm>
```

- Defining a Concept checks nothing; applying it does. `Concept AlwaysFalse => false`
  is legal until a `Require` uses it. Definitions are type-checked even when
  unused.
- `Require` works in Templates and directly in a mechanical Model with literal or
  Static inputs. It runs before any geometry is built.
- Predicate parameters are scalar `Length`, `Angle`, `Time`, `Int`, `Float`,
  `Bool` or `String`. Calls may be positional or named. Pass Record members, not
  whole Records.
- A bundle's labels name the failed check in the diagnostic, which beats a long
  chain of `&&`. `&&`, `||`, `!`, comparisons and dimensional arithmetic are all
  available, and every operand must resolve and type-check even if it would not
  change the answer.
- Zero-argument predicates can be referenced by name or called with `()`.
- Toleranced inputs are checked at their nominal values. This is not an interval
  proof.

Concepts are not runtime functions, do not convert units, cannot query arbitrary
geometry, and are not a constraint solver.

## Structural contracts

A structural `Concept` lists the typed members a construction must provide:

```firmament
Concept Bounded { Bounds: Box3 }
Concept Planar { TopPlane: Plane }
Concept PlateContract : Bounded && Planar { }
```

(These declarations are a fragment; a `Concept Struct` or Template uses them.)

Apply a contract with `type T satisfies PlateContract` or
`Concept Struct Design: PlateContract`. Conformance checks the members and their
types; a structural Concept is not a Boolean value. Composition with `&&` allows
identical inherited members and rejects conflicting types, duplicate own
members, missing contracts and cycles. A `Concept Struct` that claims a contract
publishes exactly that contract's members; to publish more, compose a larger
contract. There are no private members and no general inheritance.

Member names belong to you: `Bounds` is never silently renamed to `bounds`.
Concept names resolve within the part being compiled; there are no namespaces.

## Construction data: `Concept Struct`

`Concept Struct` holds design geometry that never becomes material, such as
layouts, guides and frames, or implements a structural contract. The `Concept`
prefix here is historical; it does not apply a predicate. Inside a layout, write
plain `Rect2`, `Circle2` and so on. Older spellings such as
`Concept Circle2 circle { ... }` bind to the same shapes with the same fields.

## Engineering interfaces: `Semantic`

`Semantic Name { ... }` groups references that consumers can name, such as a
mounting `DatumFrame` and a toleranced `Dimension`. It is usually published
through `expose`, or attached to an assembly occurrence. A Semantic is not a
second body, not a free-form metadata bag, and not a way to recognize arbitrary
BRep faces. Published values keep their types and source identity.

PMI selects which requirements to export; a Semantic alone exports nothing as
PMI. [Assembly authoring](../../firmament/assembly-authoring-foundation.md)
explains ports and placement.

## Paths

`Path` is an ordered chain of construction segments, open or closed. It creates
no material. An open Path can guide a Sweep; a Profile traced from a Path must be
closed. `Concept Path Name { ... }` is an older spelling of the same thing. See
[profiles](profiles.md).
