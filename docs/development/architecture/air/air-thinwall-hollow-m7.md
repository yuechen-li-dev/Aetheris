# AIR-THINWALL-HOLLOW-M7

`Hollow` is a constrained construction policy on a materialized primitive:

```firmament
RoundedBox<Hollow> Body { ... }
Frustum<Hollow> Body { ... }
```

It is not a policy on `Struct`. A struct can contain bodies, references, ports, and scaffolding, so making a whole struct hollow would be ambiguous. Solid remains the implicit primitive policy. This work does not provide general post-construction `Shell`.

## Doctrine

> A cup is not a cone with a Boolean crime committed inside it.

A hollow body is born as paired outer and inner analytic boundaries. The generic compiler owns the common `WallThickness`, `Openings: [Top]`, correspondence, rim, face-sense, common admission, and reporting semantics. Each admitted primitive supplies an immutable `HollowConstructionWitness` describing how its exact inner boundary is derived. The emitted `ThinWalledBodyBRepPlan` is the sole topology authority; there is no outer-solid/minus-inner-solid lowering and no safe-Boolean or legacy-shell route.

M7 admits exactly one top opening. Empty opening lists, multiple openings, side openings, and arbitrary face selections are rejected. Closed inaccessible cavities and general shelling remain deferred.

## Proof families

`RoundedBox<Hollow>` reuses the rounded-rectangle linear-sweep family. With thickness `T`, its witness proves the orthogonal offset equations:

```text
inner width  = outer width - 2T
inner depth  = outer depth - 2T
inner radius = outer radius - T
inner bottom = outer bottom + T
inner top    = outer top
```

The plan has eight outer walls (four planes and four cylinders), eight paired inner walls, two bottom planes, and eight exact top-rim faces. It rejects `T <= 0`, consumed width/depth/height, and `cornerRadius <= T`.

`Frustum<Hollow>` uses a true parallel cone, not radial scaling. For `r(z)=Rb+kz`, `k=(Rt-Rb)/H`, the inner support is:

```text
rInner(z) = r(z) - T sqrt(1+k^2)
```

and is trimmed at `z=T` and `z=H`. The offset has support-normal distance `T`; merely subtracting `T` from both radii would not. M7 rejects the cylindrical degeneracy and any non-positive trimmed inner radius.

## Authority and export evidence

Feature AIR retains primitive parameters, policy, wall thickness, opening intent, provenance, and witness. Construction AIR retains outer/inner roles, rim roles, closed-bottom roles, and per-support thickness witnesses. The shared vessel plan owns vertices, edges, loops, coedges, face roles, analytic supports, opening loop, and deterministic signature.

The production route invokes `BrepExportPreflightMode.Enforce`, emits planes/cylinders/circles for the rounded vessel and planes/cones/circles for the frustum vessel, then reimports the STEP artifact. Canonical fixture evidence is produced by:

```text
fixtures/Compatibility/LegacyV1/Experiments/air-thinwall-m7/rounded-enclosure.firmament
fixtures/Compatibility/LegacyV1/Experiments/air-thinwall-m7/frustum-cup.firmament
```

The CLI reports the witness, exactness, topology and surface counts, deterministic plan signature, analytic volume expression, STEP SHA-256, and reimport result. Independent CAD Assistant inspection is not automated by this repository route and is therefore pending external review; no CAD Assistant admission is claimed here.
