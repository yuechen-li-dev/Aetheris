# BRep Surgery candidate surface

## Proposed boundary

Initial internal namespace:

```text
Aetheris.Kernel.Core.Brep.Surgery
```

M3 should keep it internal. Only after recipe migrations prove contracts should selected operations be surfaced through `Aetheris.Forge.KernelSDK`. Ordinary `Aetheris.Forge.Host` must not gain arbitrary topology editing.

## Bounded candidate operations

These candidates are based on repeated code in current builders, not a speculative complete modeling API.

| Candidate | Existing pressure | Contract |
|---|---|---|
| `AddOrientedLoop` / `AddFaceWithLoops` | duplicated coedge allocation/linking in hole, slot, prism, and orthogonal builders | consume ordered edge uses; create cyclic next/previous coedges and a loop; no feature recognition |
| `TopologyAssemblyBuilder` extension over `TopologyBuilder` | every recipe separately assembles faces -> shell -> body | explicit IDs in, explicit shell/body out; caller chooses topology |
| indexed vertex/edge reuse for known boundary graphs | orthogonal union and polygonal extrusion builders maintain lookup maps | deterministic reuse by caller-provided geometric key; no tolerance-based topology inference |
| known ring/section extrusion scaffold | polygonal through-cut and profile/section emitters construct corresponding lower/upper rings and side faces | caller supplies correspondence, orientation, and expected inner/outer roles |
| insert a known inner trim loop on a known face | through-hole/prism recipes build outer plus reversed inner loops | explicit target face/loop and trim curve; no intersection search |
| append known analytic curve/surface bindings | hole/slot builders repeatedly create circle/line and plane/cylinder/cone/sphere bindings | geometry and parameter domains are caller-provided; validate binding completeness |
| deterministic topology/geometry/binding remap | mixed-void and assembly transformation paths copy/remap IDs | preserve maps and provenance; reject collisions/dangling references |
| explicit shell assembly/stitch from known paired edges | Firmament `SurfaceFamilyShellAssembler`, `SurfaceFamilyStitchExecutor`, combined-body remapper, and Boolean mixed builders | caller supplies pairing/evidence; Surgery performs mutation and reports structural failures |
| validation bundle | all builders call `BrepBindingValidator`; several paths separately check manifold/export properties | binding, loop closure, shell connectivity/orientation, manifoldness, and optional provenance checks |

## Not Surgery

The layer must not answer:

- whether operands represent a through hole, counterbore, keyway, sphere cavity, or union;
- which entry/exit faces a numerical intersection implies;
- whether a continuation is semantically allowed;
- what result topology subtraction should have;
- whether one recognized family should beat another;
- how to infer intent from imported or legacy geometry.

Those decisions remain in construction recipes, recognizers, and Judgment-backed policy.

## Safe, unsafe, and internal API placement

| Tier | Candidate exposure | Examples |
|---|---|---|
| safe advanced (`KernelSDK`, later) | bounded constructors with complete inputs and mandatory validation | create a closed ring extrusion; add a validated inner loop; deterministic remap with complete correspondence |
| explicit unsafe (`KernelSDK` only with unsafe consent) | partial topology replacement where caller owns invariants | replace loops/faces, stitch caller-selected shells, preserve imported residual topology |
| internal-only | raw ID allocation, unchecked topology mutation, binding-store mutation, bypassing validation | `TopologyModel.Add*`, arbitrary coedge rewiring, unchecked face/shell deletion |

`KernelSdk` currently exposes only a signed-side query. That small surface is useful evidence: M3 should not broaden public APIs while contracts are still being extracted. Forge already has an unsafe-consent concept; M5/M6 can decide whether surgery participates in that permission model.

## Geometry query separation

Keep `SignedSideQuery`, `ClosestPointQuery`, `IntersectionQuery`, and `ContactQuery` in geometry/query ownership. A recipe may use their results as predicates or evidence. Surgery accepts the caller's explicit trims/topology plan and does not convert arbitrary intersection curves into semantic topology.

## M3 acceptance test

Extract only enough mechanics for two representative paths:

1. polygonal prism through-cut or standard cube-through-hole, proving loop/face/binding mechanics; and
2. one mixed/remap path, proving ID/binding preservation.

The existing `BrepBoolean` facade must produce byte/topology-equivalent canonical artifacts before and after extraction. If an abstraction needs family names in its inputs, it belongs in the recipe layer, not Surgery.
