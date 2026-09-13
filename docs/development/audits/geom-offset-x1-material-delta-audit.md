# GEOM-OFFSET-X1 material-delta audit

This audit was completed before selecting the public `AddOffset<T>` / `RemoveOffset<T>` names and the internal `PrismaticMaterialOffsetFeature` AIR record.

## Existing authorities

| Existing operation | Bounded material meaning | Preserved semantic information |
|---|---|---|
| `Boss` | connected finite profile addition | host, support, profile, height, stable feature identity |
| `Pocket` | enclosed finite top removal | host, support, profile, depth, remaining-floor policy |
| `Hole` / `Slot` | typed analytic or profile openings | feature variant, diameter/width, placement, termination, topology descendants |
| Compose `Add` / `Remove` | axial section-stack construction | source-ordered profile intent and exact axial intervals |
| `ProfileDelta` | pre-solid 2D boundary change | boundary operation and profile provenance |
| Sculpt `OffsetRegion` | displacement of an existing region | `MayModify`, authorized envelope, preservation contracts, geometric delta |
| Sculpt section chains | bounded surface-chain add/remove | attachment/support, authorized envelope, preservation evidence, replay identity |

`OffsetRegion` is therefore not an available name for material addition/removal: it already means displacement of an existing surface region.

## Reusable implementation evidence

The prismatic section-stack compiler is safe to reuse for the X1 Prism lane. It consumes closed `ResolvedProfile2D` values, preserves source order and line/arc carriers, rejects invalid or disconnected slab material, emits deterministic exact B-rep topology, and supports STEP round-trip. This is stronger than recognizing an anonymous temporary solid after a Boolean.

The low-level `BrepBoolean` implementation is deliberately a recognized-family facade, not a general kernel. Its proven lanes include box/safe-root analytic holes, box sphere cavities/pits, orthogonal box additions, prismatic through cuts, and narrow cylinder-root coaxial holes/keyways. The current cylinder-root validator explicitly requires world-Z-aligned bores; it cannot qualify a perpendicular cross-hole. There is no bounded spherical-cap union builder. Those gaps prevent honest Cylinder/Sphere promotion in this pass.

## Decisions

- Public names: `AddOffset<Prism>` and `RemoveOffset<Prism>` are unambiguous beside Sculpt `OffsetRegion`.
- Target is mandatory and must name the active Compose body.
- X1 Prism direction is `+Z` in the Compose frame. General axis projection is not inferred.
- Add uses positive `Height`; Remove uses exactly one of positive `Depth` or `Termination: ThroughAll`.
- Whole-plane removal profiles may cross the target silhouette, enabling edge grooves and notches. A `Span<Plane>` remains a strict support contract, so a footprint escaping it is rejected.
- Typed AIR retains intent, tool family, target, support, profile, direction, termination, authorized region, and stable identity until section-stack planning.
- Cylinder and Sphere spellings fail typed as not qualified; they do not fall through to public arbitrary Booleans.

## Hole-host decision

Curved-support `Hole` is deferred to `GEOM-HOLE-HOST-X1`. Current Hole placement and the reusable cylinder-root subtraction path are planar/world-Z or coaxial. Generalizing them now would either infer an ambiguous curved-surface frame or require the same missing exact curved-target intersection machinery as Cylinder offsets.
