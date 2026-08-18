# Profile EdgeFinish convex/reflex audit X1

## Result

Profile-boundary chamfers are explicit source-bound constructions for both
`ConvexProfileJunction` and `ReflexProfileJunction`. The isolated M1
Profile-boundary fillet now exists for one named straight outer segment on Top
or Bottom. It remains deliberately unavailable for chains, loops, and convex or
reflex junctions; no 2D Profile arc is counted as a 3D fillet.

## Junction model

For predecessor tangent `u` and successor tangent `v`, the signed turn is
`atan2(cross(u,v), dot(u,v))`. Material side is left/right according to loop
winding, then reversed for an inner loop. The material interior angle is
`pi - materialSideSign * signedTurn`. Positive material-side turn is convex;
negative is reflex; zero is collinear; an antiparallel/invalid tangent is
degenerate. This handles outer and inner loops without any global-axis rule.

`aetheris inspect-profile <fixture> --json` emits Profile/loop IDs,
predecessor/successor segments, vertex ID, signed turn, material interior angle,
classification, and EdgeFinish-selection/descendant slots. The L fixture reports
five convex junctions and one reflex junction; the reflex is `Inner -> Upright`,
signed turn `-90°`, material interior angle `270°`.

## Chamfer route and evidence

The route is `Concept Path or Segment/Trace -> ResolvedProfile2D ->
ProfileBoundaryChamferTarget -> ProfileBoundaryChamferPlanner -> authoritative
planar section-transition BRep -> STEP export/reimport`. It admits one outer
line loop, a source segment, connected chain, or complete loop. Selected
selected junctions intersect inward offset supports directly. The same
section-polygon reconstruction handles convex and reflex vertices; it adds no
separate junction patch and emits planar `ChamferFace`/`InsetEdge` descendants,
plus classification descendants for selected junctions.

| Fixture | Result | STEP SHA-256 | Reimport topology / volume |
| --- | --- | --- | --- |
| `profile-chamfer-convex-junction-top` | convex chain | `5bafdd7f…33d9d` | enclosed, 12 planar faces, 6370.333333333333 mm³ |
| `profile-chamfer-reflex-junction-top` | reflex chain | `4fbca459…20fea` | enclosed, 18 planar faces, 5569.666666666666 mm³ |
| `profile-chamfer-mixed-convex-reflex-loop-top` | whole L loop | `76c8f9e1…f7aaf` | enclosed, 14 planar faces, 5521.333333333333 mm³ |
| `profile-chamfer-reflex-junction-bottom` | Bottom reflex chain | `e4580c5a…e2a04` | enclosed, 16 planar faces, 5569.666666666666 mm³ |
| `profile-chamfer-reflex-low-level-segments` | low-level Segment/Trace reflex chain | `4fbca459…20fea` | byte-identical STEP to the Concept Path route |

For the 40×40 L with 30×30 notch, lower area is 700. At `d=1`, whole-loop
upper inset area is 544 and the midpoint section area is 621. The exact
piecewise-linear transition volume is `(700 + 4*621 + 544)/6 = 621.333333333333`;
the unmodified 7 mm prism contributes 4900, giving 5521.333333333333 mm³.
All generated surfaces in these fixtures are planes (no cylinder, torus, or
sphere), each result has one enclosed orientation-consistent shell, and the
fixture tests compare two independently materialized STEP strings.

The Bottom construction initially exposed a collinear chain-termination
triangle. It now uses the changed Bottom cap endpoint directly, rather than
splitting through that collinear original endpoint.

## Compose and invalid admission

`profile-compose-reflex-chamfer-with-shaft` and
`profile-compose-reflex-chamfer-with-counterbore` prove a Top whole-loop
variable-outer interval carries a disjoint ThroughAll Shaft or Counterbore.
Counterbore admission uses its entry radius, not merely its shaft radius.
`profile-compose-reflex-chamfer-shaft-collision` fails before materialization
with `ProfileBoundaryChamferIntersectsShaft`; its source cavity is contained in
the stock but its radius-plus-corridor reaches the nearby reflex-side boundary.
`profile-compose-reflex-chamfer-counterbore-collision` similarly proves the
larger entry radius is checked and reports
`ProfileBoundaryChamferIntersectsCounterbore`.
`profile-chamfer-reflex-inset-collapse` fails with
`ProfileBoundaryChamferInsetCollapse`. The planner also has typed
`ProfileBoundaryChamferOffsetSelfIntersection` and
`ProfileBoundaryChamferJunctionDegenerate` paths.

## Edge-finish inventory and fillet matrix

| Route | Target / support | Generated family | State |
| --- | --- | --- | --- |
| Box chamfer | authored primitive boundary/edge | plane | supported |
| Box/rounded primitive fillet | bounded authored primitive support | cylinder; selected routes also torus/sphere | supported |
| bounded internal primitive fillet | explicit internal concave vertical edge | cylinder | supported, fixture-backed |
| RoundedBox top boundary fillet | primitive top perimeter | torus plus retained cylinders | supported |
| Frustum routes | bounded primitive rim routes | analytic primitive support | supported where admitted |
| Profile chamfer | source Profile outer line loop/chain, Top/Bottom | planar section transition | supported |
| Compose Profile chamfer | Top whole outer line loop, disjoint cavities | planar variable-outer transition | supported |
| Profile fillet, one straight outer segment | authored `Profile.Outer.Segment`, Top/Bottom | quarter cylinder + planar end faces | supported (M1) |
| Profile fillet chain / loop / convex junction / reflex junction | ordered source Profile boundary | no junction rolling-surface plan | binding is supported; materialization reports `ProfileBoundaryFilletJunctionTopologyNotMaterialized` or `ProfileBoundaryFilletLoopTopologyNotMaterialized` |

Primitive internal/reflex fillet support is not evidence of a Profile fillet:
the former is a bounded primitive/cell planner. M1 instead has an authoritative
constant-radius straight Profile-boundary rolling-surface plan, including
cap/side contact curves and semantic descendants. Convex/reflex junction
patches, trim/intersection topology, cavity corridors, and whole-loop fillets
remain outside that plan.

Implemented bounded follow-up: **PROFILE-STRAIGHT-EDGE-FILLET-M1** — one
source-selected straight outer Profile segment on Top, with a constant-radius
cylinder, explicit endpoint termination policy, `FilletSurface`,
`CapContactEdge`, and `SideContactEdge` descendants. The next bounded work can
therefore be a convex two-line junction; reflex junctions remain later work.

## Authoring and performance notes

Fresh canonical authoring used the `Selection { Source:
Profile.Loop.[segments] Require: ConnectedChain }` spelling on the first attempt
for both convex and reflex chains, and direct `Target: Bracket.Outer` for the
whole loop. The low-level Segment/Trace fixture was byte-identical after STEP
export to its Concept Path counterpart. The first Bottom reflex attempt exposed
the collinear termination defect described above; after the local fix it needed
no syntax retry. The composed disjoint/colliding Shaft fixtures and the
unsupported reflex fillet likewise reached their intended result first attempt;
the fillet diagnostic named the backend boundary rather than implying a 2D arc.

On this development machine, three warm `dotnet run` samples for the mixed L
fixture averaged 1672.2 ms for `inspect-profile --json` (including process and
build-host startup), 1819.8 ms for build/export, and 1737.7 ms for verify.
These are end-to-end CLI timings, not kernel-only microbenchmarks. Repeated
builds have byte-identical STEP, while classification order follows resolved
loop segment order and descendant IDs derive from authored IDs.
