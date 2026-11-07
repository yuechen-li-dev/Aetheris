# Hollow generalization: design

## Where it stands

`<Hollow>` is admitted on three primitives by name (`RoundedBox`, `Cylinder`, `Frustum`,
`FirmamentV2MechanicalBinding.BindMechanicalHollow`), each with a bespoke planner in
`ThinWalledBodyBRepPlanner`, one opening (`Top`), and one special case (`Cylinder.BottomBlendRadius`).
`Loft<Hollow>` has its own sectional route. `PlasticShell` admits only a coaxial frustum exterior.
`HollowConstructionWitness` is documented as "primitive-owned proof that its inner boundary can be
derived exactly". That ownership is the thing to change.

## Principle

A hollow is not a geometric operation on a finished body. It is a **transformation of the
construction program**: the cavity is built by the same program as the exterior, with every
parameter moved inward by the wall thickness, and removed from it. The witness belongs to the
*generator* (extrude, compose, revolve), not to the primitive, because the generator defines the
space in which the offset is exact:

| Generator | Offset space | Exact? |
| --- | --- | --- |
| Extrude, Compose (section stack) | section plane × axial levels | yes: planes stay planes, arcs stay arcs, levels shift by t |
| Extrude with uniform draft α | section plane, distance t / cos α | yes: drafted planes and cone corners offset to the same classes |
| Revolve | meridian half-plane (r, z) | yes: a surface of revolution's normal lies in the meridian, so the 2D parallel curve revolves to the 3D offset |
| Loft (ruled) | none | no: keeps its sectional "nominal" witness, reported as such |
| Arbitrary B-rep | none | refused; this is the commercial shell problem |

The primitives desugar: `RoundedBox` is an extrude of a rounded rectangle, `Cylinder` an extrude
or revolve of a circle, `Frustum` and the bottom-blended cup a revolve of a line/arc meridian. The
three bespoke planners and `BottomBlendRadius` become instances and are deleted (convergence rule).

This is the same move as rounded rectangles and the thread run-out: the cavity is derived from
intent the compiler already holds, never inferred back from B-rep faces.

## The transformation pass

One pass over the construction IR, before materialization. Every rule is closed-form:

| Exterior construct | Cavity construct |
| --- | --- |
| Profile line | parallel line at distance t inward |
| Convex arc, radius r > t | concentric arc r − t |
| Convex arc, r ≤ t | vanishes; neighbours meet at a sharp vertex (normal: outer radius smaller than wall) |
| Concave arc, radius r | concentric arc r + t |
| Sharp convex vertex | sharp vertex (offset lines intersect) |
| Sharp reflex vertex | arc of radius t about the vertex (true constant thickness; free in 2D) |
| Closed cap at level z | level z ± t |
| Cap named in `Openings` | not offset; the cavity runs through it |
| `Round` stage radius r | r − t (handled by the arc rules) |
| 45° chamfer of size d on a right-angle corner | chamfer of size d − t(2 − √2) (offset planes; exact) |
| Constant `EdgeFinish<Fillet>` r on a convex exterior edge | concentric fillet r − t on the cavity edge, or sharp if r ≤ t |
| Revolve span on the axis | exempt: the axis is not a material boundary |

Section stacks (Compose) apply the 2D rules per slab. Between slabs the cavity's level breaks move
by ±t, and its section in each band is the offset of the intersection of the exterior sections
within ±t of that band (2D arrangement intersection, which `ProfileArrangementBuilder` already
does). The result is again a section stack, so it lowers through the existing Compose route as
`Remove` operations: no new B-rep builder.

## Admission and diagnostics

The pass refuses, with the location named (`plastic-shell-wall-offset-collapse` already exists):

- an offset curve that vanishes or reverses other than a convex arc of r ≤ t;
- a cavity section whose region count or loop nesting differs from the exterior's (a neck
  thinner than 2t would split the cavity);
- a draft that is not uniform over a wall;
- a generator with no offset space (arbitrary B-rep, booleans of imported bodies).

The witness reports, per corresponding face pair, the exact distance and the surface class pair.
Where slab steps meet at a sharp reflex edge the wall is thicker than t (up to √2·t at 90°); that is
reported as a measured ratio, the same thick-section proxy PlasticShell already uses for junctions.

## Ordering

Exterior construction (including its finishes and exterior bosses) → derived cavity → `Modify`
features on the shell (perforations, holes, cut-outs through the wall) → interior features grown
from the cavity (ribs, standoffs, bosses with core holes). The last group is where PlasticShell
lives; its `Exterior` stops being frustum parameters and becomes any `HollowConstructible` model.

## Phases

1. Exact 2D line/arc offset of `ResolvedProfile2D` with collapse detection. Everything else rests on it.
2. `Extrude<Hollow>` and `Compose<Hollow>` through the section stack; `Openings: [Top]`, `[Bottom]`,
   `[Top, Bottom]` (a tube). Desugar `RoundedBox<Hollow>` and `Cylinder<Hollow>`; the existing
   fixtures must produce the same topology and volumes, then the bespoke planners go.
3. `Revolve<Hollow>` through the meridian; desugar `Frustum<Hollow>` and the bottom-blended cup.
4. Finish transfer: constant fillets and chamfers on exterior edges.
5. PlasticShell exterior from any hollow-constructible model; uniform draft.

## Decisions for the author

1. **Reflex corners.** Rolling-ball (arc of radius t, constant thickness) in-plane, as proposed, or
   sharp like commercial shell? Between slabs, sharp is proposed so the cavity stays a section stack.
2. **Exterior features.** A boss declared in the exterior program is hollowed with it (proposed);
   a boss added by `Modify` after the hollow stays solid.
