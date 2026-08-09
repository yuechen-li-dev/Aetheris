# Preview 2 consolidation M1

Status: implemented and validated for the existing Concept Path language; the
broader semantic-reference proposal is audited and deliberately deferred.

## Outcome

Firmament's `Concept Path` is an ordered planar Line/Arc/Close construction. It
normalizes into `ResolvedProfile2D` at Profile binding. P2-CONSOLIDATION-M1
removes Compose's older syntax-category restriction: path-derived and low-level
Profiles now enter the same operand dictionary, section-stack construction,
exact BRep planner, and STEP exporter. Path syntax does not reach downstream
lowering.

The durable native fixture is
`fixtures/FirmamentV2/Canonical/valid/table-template-concept-path-compose.firmament`:
Static Table row -> Static Record -> `with`-derived Record -> Template -> Concept
Path -> Profile -> Compose -> Selection -> exact STEP. The smaller direct fixture
is `fixtures/FirmamentV2/Canonical/valid/concept-path-compose-profile.firmament`.

## Semantic contract and capability rule

A path exposes its typed start point, named Line/Arc guides, and named endpoints
inside one planar frame. Profile capability is proven only by successful
`ResolvedProfile2DValidator` validation: closed ordered loops, valid winding and
nondegenerate exact line/arc geometry. The rule is structural and independent of
whether the profile originated in low-level guides or Concept Path syntax.

Compose capability is therefore “resolves to a validated `ResolvedProfile2D`,”
not “was parsed by `Profile ... Using ...`.” Selection uses the same Profile
identity and named segments. Existing Profile EdgeFinish/Chamfer/Fillet routes
continue to admit only their documented exact boundary contracts; Compose
boundary finishes and other Modify targets were not broadened.

## Provenance and inspection

Resolved segments retain Profile stable ID, `concept-path:<Path>.<Step>`, source
span, derivation, and frame. `FirmamentV2Document.TemplateInstantiations` now
retains Template specialization plus Record/Table provenance on Profile/Compose
adapter documents. `aetheris inspect --json` reports path capabilities, exposed
members, consumers, and provenance alongside Template instances.

## Audit boundary

The milestone request used “Concept Path” to also mean navigation through
Template outputs, imported recognized regions, Forge extension outputs, and FEA
regions. That language feature does not exist today. Concept IR supports bounded
compile-time `Instance.Member` expressions and materialized `Expose` metadata;
Recognize stores exact face references; FEA currently parses region paths into a
typed `SemanticRegionBinding`; Forge capabilities return construction artifacts.
None exposes a common Profile/Compose semantic-value kind. Treating their dotted
strings as paths would violate the no-reflection/no-string-escape requirements.
The required common typed semantic-reference layer is a real post-M1 compiler
feature, not a safe consolidation patch.

## Subsystem closeout

- SurfaceMeshIR: Cone, Sphere, Torus, Hyperbola trim bands, sampled
  non-rational B-spline trims on analytic supports, planar feature bands, STL,
  and OBJ are implemented. Rational spline trims and spline/NURBS support
  surfaces remain bounded/unsupported.
- Continuum: BRep is exact topology; CIR is occupied continuum; SDF is a CIR
  backend; bounded SDF-to-BRep is decompilation. The old authoritative-volume
  roadmap is marked superseded without deleting its useful history.
- FEA: basic linear elasticity, exact planar semantic boundaries, traction,
  resultant force, pressure, native Box/box-hole, and canonical imported boxes
  work. Curved weak constraints, generalized Nitsche bounds, higher-order stress
  recovery, nonlinear mechanics, contact, and dynamics remain future work.
- Forge: typed Template invocation and the sample exact construction extension
  work. One construct output, bounded prism ConstructionIR, explicit discovery,
  no generalized loft/multi-body assembly, named imported resources, and no
  production source generator remain deliberate M1 limits.
- Mixed Profile Fillet: still Experimental. Later CIR architecture changed the
  representation story but did not close the release blocker: reimported curved
  trim volume still carries roughly 41k mm^3 conservative error envelopes. It is
  not a small local promotion.

## Evidence

Machine-readable classification, the support matrix, workflow results, and CLI
hashes live under `docs/preview2/evidence/consolidation-m1/`. The ranked source of
truth for remaining work is `consolidation-gap-inventory.md`; public status is
recorded in `feature-manifest.json`.
