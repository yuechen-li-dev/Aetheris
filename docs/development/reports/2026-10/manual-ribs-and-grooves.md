# Manual ribs and bounded grooves

Date: 2026-10-27

## Result

Manual `Rib<Rectangular>` and `Groove<Rectangular|Semicircular>` consume named
Concept guides or one-segment Paths through the unified mechanical AST owner.
The compiler constructs one integrated exact boundary and uses enforced
production STEP export. Validate uses that same construction path.

The old surface-feature descriptors and evidence generator were planning
scaffolds, explicitly claiming no materialization. They are not used as evidence
that the new features work. PlasticShell AutoRib remains unchanged; manual
placement and geometry qualification precede any expansion of automated network
selection.

## Geometry and admission

- Box-top bands follow lines, circular arcs and circles. Rectangular ribs have
  explicit base thickness, height and nonnegative narrowing side draft.
- Rectangular channels have exact planar/cylindrical sides and planar floors.
  Semicircular channels use cylinders or tori; rounded straight ends use spheres.
- Circumferential grooves replace cylindrical bands with shoulders/floor or a
  toroidal meridian, on solid shafts and top-open hollow cylinders. Inner grooves
  cut outward into the wall. The original stock radius, height, base and cavity
  floor remain construction authority.
- Shared topology replaces the support footprint. No overlapping rib solids,
  general Boolean tools, faceted geometry or spline support approximations are
  emitted. Optional STEP UV data may use non-rational polynomial pcurves.
- Construction receipts, named feature identities, source spans, guide
  references and topology descendants survive materialization. The engineering
  inventory uses the existing report contract.
- Dimensions, wall/floor margins, draft collapse, missing/ambiguous guides,
  unknown fields, unsupported frames and unsupported feature combinations
  reject. Planar bounding-box disjointness is a conservative admission proof;
  it is deliberately reported as a missing junction plan rather than falsely
  asserting that every rejected pair intersects.

## Existing Path debt repaired

Scalar serialization exposed two pre-existing assumptions in the Path owner:
literal-only unit-whitespace matching for Start/Heading, and PascalCase-only
step fields. Header values and simple Line/Span/Arc fields now use the existing
AST dimensional and closed-field readers. No new regular expression, document
dispatch, expression evaluator or global source rewrite was added.

Tighter material-effect witnesses also caught a pre-existing exact-volume
recognizer which accepted any nine-face box/cylinder arrangement as a convex
quarter-cylinder fillet. It now requires outward convex support, quarter-circle
end trims and the actual stock-corner cylinder placement. A concave semicircular
channel explicitly rejects that exact recognizer. The same witnesses caught
and corrected the new Flat-ended semicircular channel's end-cap normals.
The effect tolerance is relative to the feature volume, so an omitted small
channel cannot hide inside a large stock-volume allowance.

Circle guides consume the existing ClosedBoundary2 typed dimensions after
lowering erases their original surface syntax. Qualifiers are checked against
authored Concept members. Duplicate guide names are explicitly unqualified;
the legacy leaf-name guide dictionary is not allowed to silently choose one.
The same refusal covers duplicate Point2 dependencies across Concept Structs:
a qualified line must not capture another layout's Start/End coordinates.

Repeated full qualification exposed a fixture-reader race: one CLI regression
created and deleted temporary mutation files under the canonical source tree
while another enumerated it. Those self-contained generated specimens now live
under ignored artifacts/local, fixing the creating owner rather than weakening
the corpus reader or suppressing missing-file errors.

## Qualification

Persistent core witnesses cover twelve planar combinations and six rotational
combinations, closed/orientation-consistent topology, analytic support families,
strict export, reimport, and independent volume comparison. Additional core
negatives cover failed dimensions, collapsed draft, support escape, lost floors
and overlapping cuts.

Frontend tests cover real admission/build agreement, all fixture families,
scalar parameters, single-segment Paths, source locations, unknown fields,
unsupported frames/sections, mixed channel sections and name stability after
manual rib reordering. The fixtures are under
`fixtures/Canonical/Features/PathFeatures/`. Raw generated evidence defaults to
ignored `artifacts/local/path-feature-*`.

Final evidence against the completed implementation:

- Complete Release solution build: zero errors, 37 warnings.
- Core fast lane: 1,164 passed.
- Complete solution regression: 6,279 passed, zero failed, two skipped, twenty
  test assemblies. The skips require an optional external PDF renderer.
- Core suite includes all twenty-three new path-feature geometry/negative cases;
  frontend coverage includes twenty-two path-feature cases. The focused schema
  and feature lane passed all thirty-four cases.
- All six canonical fixtures passed real CLI validate, build and output analyze.
  STEP artifacts and JSON evidence are in artifacts/local/path-features.
- Rebuilt managed/WASM browser SDK and passed all nineteen JavaScript SDK tests.
- Existing canonical/interchange tests passed without refreshing any geometry
  baselines during this pass. Only the schema inventory gained the two admitted
  construct names.
- Dedicated quality review checked closed field ownership, scoped guide capture,
  feature accounting, geometric end-cap orientation, material-effect tolerances,
  compatibility, source locations, and generated-file placement. No parser
  routes, diagnostic-code allowlists or regular expressions were added.
- Git diff whitespace checks passed; no task-owned .NET/test hosts remained.

Real CLI dogfooding also ran `measure --sdf --error 5 --compare-mesh` on the manual
rib. Geometry builds, but authoritative volume is explicitly unavailable: this
new composite construction does not retain a qualified CIR field and is outside
the existing exact volume recognizers. Topology and the numerical mesh comparison
remain available. The compiler does not promote the planned volume delta or mesh
estimate into assertion authority. Qualifying the composite CIR/volume witness
is a separate measurement integration boundary, recorded rather than patched
with a misleading primitive-parity flag.

## Deliberate limits and next ownership boundary

This is manual construction, not injection-molding certification. The existing
AutoRib zero-draft warning and specialized standoff-network limits remain true.
No automatic rib placement, base blends or sink/warpage guarantee is implied.

Planar hosts are direct Box tops; cylindrical hosts are direct stock or top-open
hollow cylinders without bottom blends. Other Box faces, Extrude/Compose,
imported supports, compound/branching paths, overlapping/nested envelopes and
combination with Hole/EdgeFinish/Perforation/Thread remain explicit refusals.
Open arcs require Flat ends and positive sweeps no greater than 180 degrees.

The next geometric prerequisite for broader manual/automatic rib networks is an
authoritative footprint-junction arrangement that joins rib intersections and
retains named descendants. That belongs in the material arrangement owner,
before AutoRib selects among candidate networks. Ordinary selector dispatch and
analytic construction here require no judgment scoring.

Public behavior and examples:
[grooves and manual ribs](../../../public/firmament/grooves-and-ribs.md).

## Verdict

**Success within the documented manual-feature boundary.** Named ribs with side
draft and both requested groove sections construct integrated analytic solids,
survive strict STEP round trips and agree between validate/build. Unsupported
hosts, guide arrangements and feature combinations reject explicitly. AutoRib
automation, public named rib-face selectors and composite SDF/volume authority
remain separate, disclosed follow-up boundaries.
