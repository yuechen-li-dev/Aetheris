# CIR-CONE-V1: CIR cone primitive for Firmament path

## Scope outcome

This milestone adds a bounded `CirConeNode` primitive to CIR semantic and tape evaluation paths, plus Firmament→CIR lowering for cone primitives/tools.

## Parameter convention

`CirConeNode(bottomRadius, topRadius, height)`.

- Native frame is origin-centered on Z.
- Bottom cap plane: `z = -height/2` with radius `bottomRadius`.
- Top cap plane: `z = +height/2` with radius `topRadius`.
- Radius varies linearly along Z from bottom to top.
- Allowed: frustum (both radii > 0), point cone (one radius = 0), equal radii (cylinder-like cone).

Validation:
- `height > 0` and finite,
- `bottomRadius >= 0` finite,
- `topRadius >= 0` finite,
- at least one radius strictly greater than zero.

## Evaluation/SDF convention

- Uses a bounded finite cone signed-distance style function (same evaluator in node and tape), not a cylinder approximation.
- Capped finite behavior is modeled directly.
- Point-cone apex handling is stable (no NaN/Inf on tested probes).

## Transform policy

- No axis/orientation fields in `CirConeNode`.
- Arbitrary placement/orientation stays in `CirTransformNode` and tape inverse payload transforms, matching existing primitive conventions.

## Tape/lowering status

Added:
- `CirTapeOpCode.EvalCone`,
- `CirTapeConePayload`,
- point evaluation path,
- conservative interval path (corner-sampled bound in local transformed AABB),
- `CirTapeLowerer` support.

## Firmament lowering status

Firmament already has cone primitive syntax and lowered cone parameters. CIR lowering now maps:
- primitive `cone` -> `CirConeNode`,
- boolean tool `cone` -> `CirConeNode`.

## Source-surface extraction status

Deferred for V1.

`SourceSurfaceExtractor` currently inventories box/cylinder/sphere/torus evidence only. Conical source-surface descriptor/evidence integration is a follow-up milestone to avoid broadening this primitive-only change.

## BRep point-cone cleanup status

Inspected existing BRep cone/frustum path; no low-risk apex-topology cleanup was applied in this milestone. Keeping CIR-CONE-V1 bounded; if tiny degenerate apex geometry persists in BRep point-cone construction, track as a focused BRep follow-up.

## Next milestone suggestion

CIR-RECOVERY-V11:
1. update `CountersinkVariant` recognition to admit bounded cone tool patterns using `CirConeNode`,
2. add strict coaxial/entry/depth/radius checks,
3. wire executor cone subtract path and countersink STEP smoke in that milestone (not in V1).
