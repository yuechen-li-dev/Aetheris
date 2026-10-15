# Firmament Concepts and 2D construction

Date: 2026-10-14

## Intent and language decisions

A Concept is a compile-time requirement. This pass admits both named predicates
and structural contracts, plus labeled bundles that avoid long Boolean
conjunctions. It qualifies the compatibility Concept prefix across the existing
closed 2D families through production compilation.

```firmament
Concept Positive(value: Length) => value > 0mm
Concept PlateRequirements(width: Length, height: Length) {
  positiveWidth: Positive(width)
  positiveHeight: Positive(height)
  aspect: width >= height
}
```

`Require dimensions => PlateRequirements(width, height)` applies the bundle.
Every field must be Bool and true; a failure identifies its label. Definitions
are type-checked even if unapplied, but defining a false predicate is legal.
Calls use positional or named scalar arguments. Record scalar members and checked
toleranced values can supply arguments. Toleranced inputs contribute their
nominals; this does not prove interval satisfaction.

Structural Concepts keep `member: Type`. Named composition uses
`Concept PlateContract : Bounded && Planar { }`. Identical inherited requirements
merge; conflicts, duplicate own fields, unknown bases and cycles reject.
Concept Struct conformance claims are checked against actual semantic values,
including claims unused by material construction. Built-in Box type admission
retains its established Bounds/TopPlane/CenterAxis contract. Concrete semantic
values and Expose continue to belong to their existing resolver.
Claimed Concept Struct contracts retain that resolver's closed published-member
signature; additional members need a larger composed contract. General open
traits, private members and structural subtyping are not introduced.

A Record defines data shape. A record-shaped Concept bundle defines labeled
Boolean requirements; it is not a Record value. `Concept Struct` remains
non-materialized construction. Plain Circle2, Rect2 and other shape declarations
are preferred. `Concept Circle2`, `Concept Slot2` and the other closed-family
forms remain compatibility construction spellings, with the same fields and
geometry as their plain forms. They do not define new predicate types.

Construct/type names use PascalCase and built-in fields use camelCase. Predicate
parameter names and bundle labels are user-owned symbols; their spelling is
preserved. Published structural symbols such as Bounds keep their existing case.
No global case folding or new capitalization aliases were added.

## Reproduction and repairs

| Case | Observed before | Production repair |
| --- | --- | --- |
| Named predicate and bundle | Canonical witness rejected with Template require-non-bool. | Shared AST Concept binding, applied from Template and native mechanical Require. |
| Concept Slot2 used by TraceLoop | Canonical witness rejected as traceloop-not-loop. | The closed-boundary owner consumes Concept-prefixed shapes instead of skipping them. |
| Inline structural requirements | Source readers could read only a prefix or miss semicolon-separated members. | Contract definitions and semantic member extraction consume AST fields/spans. |
| Claimed conformance | Template type admission trusted a matching source annotation. Unused claims could evade semantic checking. | Actual semantic values are checked through ConceptIrResolver. Original missing-member/cycle codes survive. |
| Unknown fields with unresolved guide inputs | The boundary owner returned before closing field consumption. | Deferred Rect2/Circle2 inputs still consume their known fields and reject unknown intent. |
| Unconsumed invalid guides | A primitive-only body could bypass the profile guide binder. | Mechanical admission calls that existing binder for non-materialized guides too. |
| Explicit boundary rotation | CLI validation reported Built while nonzero rotation was discarded. | Explicit Triangle2/Polygon2 reject nonzero rotation and suggest authored-point or Curve2 placement. Zero rotation is retained. |
| Editor errors with Templates | Presence of any Template caused a project-context-only response, even for a concrete Model. | AST concrete-root identification allows normal compiler diagnostics through. Structured code/message/span projection uses the compiler receipt. |

The original rejected sources and accepted-but-ignored rotation report are
preserved under ignored `artifacts/local/firmament-concepts/before-*.json`.
Durable positive sources live under `fixtures/Canonical/Concepts/`; negative
sources live under `fixtures/Regression/Concepts/`.

## Ownership and compatibility

FirmamentConcepts binds structural signatures and Boolean requirements from the
shared syntax tree. Arithmetic types reuse the native compile-time binder's
type rules; numeric evaluation reuses FirmamentSyntaxValues. Template and native
static Require now call this same owner. Their previous Boolean regex evaluators
were removed. Template Require extraction, removal and host inspection use AST
spans rather than line matching.

This does not add a parser route for accepted geometry, a diagnostic fatality
allowlist, a field-schema copy or a source rewrite system. The existing closed
boundary, Profile, AIR, BRep and STEP owners still construct geometry. Early
conformance rejection is visible as UnifiedConceptBinding; accepted mechanical
sources retain UnifiedMechanical.

The legacy dimensional `Require { Actual: ... Expected: ... Tolerance: ... }`
keeps its semantic-constraint and projected-PMI owner. Construction declarations
inside Concept Struct keep their profile/point/derived-frame owners. The new
semantic member reader does not claim those declarations. Existing native
Require inspection receipts and Template RequireResults remain available.

Concept Circle2 now carries the same closed-guide inspection metadata as plain
Circle2. This additive metadata does not materialize a guide or force its
consumer to use the full circle: the semicircular Revolve witness remains valid.
Older routing assertions and fixture expectation metadata now expect the earlier structural conformance
failure instead of requiring a downstream missing-hole-center error. The source
is still rejected, and its original missing-member diagnostic remains present.
Generated helper names are retained as expansion receipts and excluded from the
authored boundary list; the 15-shape family still reports 15 boundaries.

Errors retain stable codes, readable messages and available source spans.
Specialization checks point to the authored Require. If preceding expansion
moves offsets, a unique original declaration supplies an enclosing anchor with
an explicit location note. Unavailable provenance is disclosed rather than
reported as an exact original expression offset. Underlying semantic binding
codes and the named failed bundle member are preserved.

## Qualification

The Release solution build succeeded with zero errors and 19 existing
nullable/analyzer/platform/WASM warnings. Builds used `-m:1 -nr:false` and
`UseSharedCompilation=false`; test execution was serial.

| Check | Result | Local evidence |
| --- | --- | --- |
| Firmament component lane, excluding SlowCorpus | 2,436 passed | `firmament-final.log` |
| Focused CLI routing, inspection, corpus and new requirements | 35 passed | `cli-final.log` |
| Required Core fast lane | 1,068 passed | `fast.log` |
| Full `dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1` | 4,914 passed across 20 executing assemblies; zero failed/skipped | `full.log`, `test-summary.json` |
| Normal CLI commands | 21 expected outcomes, including validate/build/parse-only rejection agreement | `cli-results.json`, command-specific JSON |
| Complete public examples | 9 compiled through the real compiler | `documentation.json` |
| Existing STEP baselines | 13 byte-identical outputs, including typed-tolerance and Record/Template/Feature explicit PMI | `step-parity.json` |
| Plain versus Concept-prefixed closed shapes | 17 byte-identical STEP comparisons | `FirmamentConceptRequirementsTests` in full regression |
| Slot acceptance witness | One body, six faces, world bounds (-20,-6,0)..(20,6,5) mm | `shape.step`, `shape-analyze.json`, `qualification.json` |
| Runtime cleanup | Zero remaining dotnet/test hosts or Visual Studio build nodes; 15.1 GiB memory and 103.8 GiB disk free at final snapshot | `runtime-summary.json` |

The full lane includes 2,450 Firmament and 512 CLI cases. The solution's existing
FrictionLab test project reports no discoverable tests; it is not counted among
the 20 executing assemblies. The narrower component lane is not substituted for
the full gate.

Fresh CLI checks include the threaded bolt, guitar, industrial ATLAS, warm-modern
house, factory scene, sheet-metal Template, existing PMI and generic mounting
plate. Their domain-appropriate validate/inspect paths succeeded. This is compiler
and artifact qualification; no new Helios browser-interaction claim is made.
The shared language-service test verifies the compiler's failed-requirement
message and authored Require span project into editor diagnostics.

All generated evidence is under ignored `artifacts/local/firmament-concepts/`.
Older failed logs are retained as reproduction evidence; the final gate is
`full.log`. Invalid CLI builds produced no STEP file. Byte identity is claimed
for the 13 saved baselines and paired shape cases, not every showcase export.

## Code-quality pass

The modified owners were reviewed for duplicate admission, hidden fallback,
diagnostic loss, spans, mutable parser state and new rewrite/schema debt. Contract
definitions, semantic member extraction and both Boolean Require consumers now
use AST spans. The two Boolean regex evaluators and Template conformance regex
checks were removed. No new regex, error-code fatality list, field lookup schema,
global mutable environment, whole-document dispatch or geometry operation was
added. Construction guides are checked through their existing owner. Missing
member and cycle codes survive earlier conformance rejection; generated boundary
helpers retain receipts instead of becoming fake authored shapes. The new owner
introduces no build warning. Markdown links/fences and `git diff --check` pass.

## Deliberate limits

- Predicate arguments are scalar Length, Angle, Int, Float, Bool and String.
  Whole Record, shape, frame and selector parameters need their semantic value
  owners consolidated first. No missing geometry capability is implemented.
- Concept names retain part-wide resolution in the expanded mechanical source.
  This does not introduce namespaces, nested type scopes, runtime methods or
  inheritance of material construction.
- Boolean Require is admitted in a mechanical module, Model or construction
  Struct. A primitive field block cannot silently consume it. Other domain
  frontends and the legacy dimensional constraint owner are not universally
  migrated by this pass.
- Template/Feature/Pattern source expansion still exists. This is production
  admission work, not a claim that the complete frontend has one AST lowerer.
- A 2D guide creates no material. Profile closure, supported consumers and
  existing placement/capability limits still apply. Arbitrary sketch solving,
  2D Boolean algebra and general shape-valued predicates are not added.

No unresolved policy choice requires the user's approval. The remaining limits
are explicit implementation boundaries.

## Verdict

**Success for this pass.** Named predicates, labeled requirement bundles and
structural composition work through production validate/build admission. The
qualified 2D families accept legitimate Concept compatibility spellings with
identical geometry and reject the reproduced unknown-field and ignored-rotation
cases. Existing geometry, tolerance/PMI artifacts and canonical projects remain
qualified by the evidence above. This is not whole-frontend or universal
cross-domain semantic-consolidation acceptance.
