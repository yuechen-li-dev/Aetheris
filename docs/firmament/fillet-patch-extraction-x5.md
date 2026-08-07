# Fillet patch extraction X5

X5 separates the reusable analytic part of the existing finite Profile Fillet
routes from their open-chain shell closure.  This is deliberately a planning
change: M1/M2/M3 continue to use their established materializers while the
closed mixed line/arc shell is completed only after its sharp contact topology
is composed exactly.

## Audit

The old M1 straight-edge route owns a quarter-cylinder plus two planar endpoint
termination faces.  M2 owns two cylinder rolls, one sphere junction, a cap,
trimmed sides, and endpoint faces.  M3 similarly owns two rolls and its horn
torus.  `SphereSeamCompatibility` substitutes only the M3 torus junction with a
sphere.  The cylinders, sphere/torus surfaces, their roll-to-junction seams,
cap contacts, side contacts, semantic identity, and source provenance are
reusable.  Endpoint termination faces, retained sharp stubs, and complete cap
and side closure are not.

## Component IR

`ProfileFilletComponent` records source identity, analytic family, frame,
predecessor/successor interfaces, cap and side contacts, semantic descendants,
and provenance.  Its concrete variants are:

- `StraightFilletRollComponent`;
- `ConvexSharpFilletJunctionComponent`;
- `ReflexSharpExactRollingJunctionComponent`;
- `ReflexSharpSphereCompatibilityComponent`;
- `FilletSeamComponent` for an explicit oriented shared boundary.

M1 now exposes its roll component separately from the start/end termination
policy.  M2 and both M3 policies expose ordered roll/junction/roll components
in `ProfileFilletShellPlan.Components`.  The component records have no
termination-face interface or semantic descendant.

## Composition boundary

The existing X4 mixed plan already owns the rounded-source cylinder, sphere
limit, and torus components plus source-ordered Cylinder/Torus and
Cylinder/Sphere seam records.  An attempted direct adaptation of the Chamfer
emitter was rejected by the STEP B-rep preflight: a sharp M2/M3 junction's
contacts are not source-offset endpoints.  In particular, its roll side and
cap contacts are displaced into the junction, so replacing the junction with
an ordinary line/arc seam creates trim points off the Cylinder, Sphere, and
Torus surfaces.  This validation is retained as the required next composition
constraint; it must not be bypassed by disabling preflight or using NURBS.

X6 now adds `ProfileFilletContactBoundary` and an immutable contact-shell plan
ahead of topology allocation.  It rejects the first unresolved sharp
wraparound contact with a typed invariant rather than allowing an invalid
offset-derived B-rep.  See `fillet-contact-shell-composer-x6.md`.  The next
emitter must resolve the extracted sharp components and the X4 rounded
components before allocating one parent top-cap boundary and parent-owned side
trims. A closed loop has zero endpoint termination faces; open M1/M2/M3
fixtures retain their explicit external terminations.

## Status

X7 supplies the typed side-chain and pre-emission incidence layer needed by
that composer.  It establishes that M2 requires an explicit roll-plus-support
chain and that M3 ExactRolling uses a point contact at its retained notch,
not a zero-length support edge.  The seven-station closed-loop Fillet
materializer, its volume assertion, persistent Fillet STEP artifacts,
compatibility comparison, inspection JSON, and external-kernel smoke remain
blocked until all component extractors and the parent emitter consume those
contracts.  No Fillet artifact is claimed by this document.
