# Firmament typed tolerances and language roles

## Contract

`Static drillDiameter: Length = 8mm tol +0.10mm -0.05mm` is an immutable
compile-time dimensional requirement. Geometry consumes 8 mm. A named PMI
record with `dimension: drillDiameter` explicitly projects the nominal and
allowances through the existing semantic AP242 exporter. Nothing is implicitly
annotated. `tol 0.1mm` means symmetric allowances; Angle uses deg. Non-dimensional,
negative, mismatched and nonfinite allowances reject.

The qualified source is
[`typed-tolerance.firmament`](../../fixtures/Canonical/PMI/typed-tolerance.firmament).
The negative CLI witness is
[`unit-mismatch.firmament`](../../fixtures/Regression/TypedTolerances/unit-mismatch.firmament).

## Before and repair

The previous tolerance IR and exporter already existed. Source inspection
found `tol` bound only through lowercase let's text parser, while the unified
mechanical binder supplied empty value environments to PMI. Scalar Static
declarations had no consuming owner. Path's construction owner required
`Concept Path`, even though mechanical recognition admitted bare Path.

The shared recursive-descent AST now represents a tolerance suffix with its
own span. Native Static scalars lower to the existing typed dependency binder
and tolerance IR. Nominal values are projected into actual primitive/Modify
AST fields; no source replacement pass or parser route is added. Remaining
Static declarations in unsupported construction/domain scopes reject. The
canonical symbol table includes scalar identities. A direct alias preserves
the tolerance; arithmetic produces a nominal result, with the existing
tolerance-drop warning unless the result supplies a fresh allowance.
The existing `Static unused = 4mm` shorthand remains valid with inferred
Length, and produces the same geometry as before. Integer overflow and
nonfinite scalar results reject rather than entering geometry as wrapped values.

Native typed PMI cannot override its source requirement's nominal or allowance.
For direct named holes, the actual diameter must satisfy the projected
requirement. Existing explicit Value/Tolerance and Require-based projections
retain their separate admitted contracts. Lowercase let's compatibility reader
remains for existing source; this pass does not claim to have migrated every
remaining compatibility extension.

AP242 inspection also confirmed an existing omission: the native size exporter
has no datum-reference member, although legacy literal PMI can bind DatumRefs.
The existing PMI export-admission gate now rejects that unsupported relationship
for every spelling, including typed projection and legacy literal records. The
validation report consults the same capability owner and identifies the record
as export-deferred during parse-only analysis. Normal validate/build reject it.
The witness exports Datum A independently and requests a hole-wall-associated
size. No kernel operation or invented datum association is introduced.

The preserved failing source is
[`legacy-datum-reference.firmament`](../../fixtures/Regression/TypedTolerances/legacy-datum-reference.firmament).
Before repair it requested `DatumRefs: [A]`, while AP242 reinspection returned
an independent Datum A and a diameter with empty datumReferences. Nine shipped
canonical/regression examples, three embedded standard-product recipes and the
nested-template positive witness now omit the unimplemented field and retain
their independent Datum records. The nested-template test preserves the bad
request as a rejection check. Construction is unchanged.
The actual future datum-referenced control remains a separate PMI capability,
explicitly rejected until its semantics and exporter are qualified. Original
build/inspection evidence is kept as `legacy-datum-build.json` and
`legacy-datum-analysis.json` beside the run logs.

Diagnostic propagation now retains spans when the bound diagnostic source is
identical to the authored source after span-preserving spelling/schema handling.
Expansion without a source map still discloses unavailable provenance. Both
error and fatal severities count as admission failures. CLI build recovers
structured compiler diagnostics without another diagnostic-code allowlist.
The language service projects located errors and tolerance-drop warnings.
The old line-substring comment stripper is replaced by shared-lexer trivia
masking: offsets and line breaks survive, quoted URLs are not truncated, and
malformed comment tokens remain visible to syntax admission. Native String
constants decode their AST literal through the existing JSON string codec.

The unsupported-consumer regression exposed a remaining Sweep text reader that
ignored an unused scalar requirement. Sweep declaration/header fields now bind
through the shared AST and existing field-consumption owner. Unknown/duplicate
fields, multiple Sweeps and unsupported PMI cannot disappear. Its regex header,
property and quantity readers are removed; its analytic geometry lowerer is
unchanged. WireForm/WireRoute explicitly reject residual Static declarations
through the shared statement owner. Their existing operation grammars and
implicit-unit compatibility are retained; this is not a claim of complete
WireForm AST migration. Sweep and WireForm CLI validation now use actual normal
compilation for `Built`, distinguish `ParsedNotBuilt`, and present structured
diagnostics instead of bare strings.

## Language decisions

| Construct | Decision |
| --- | --- |
| Static / tol | Canonical named compile-time requirement; PascalCase type, camelCase authored example names, lowercase tol. |
| Pmi | Explicit semantic export selection; no automatic annotation of all toleranced values. |
| Semantic | Typed, named engineering interface for published datums, axes, points and dimensions; not material construction or arbitrary metadata. |
| Path | Preferred unqualified ordered guide declaration; Concept Path remains a compatibility spelling of the same owner. |
| Profile | Closed planar material boundary consumed by extrusion or another feature; a Path can remain open. |
| Region | Domain-specific selection/region vocabulary. The lowercase mechanical sideHole region/cut form remains narrow compatibility, not a universal native Region type. |
| Enum | Mutually exclusive compile-time tags, especially typed Template choices. |
| Match | Select one result in the consuming binder's admitted compile-time expression positions. |
| Set / Pattern | All named entries / finite repetition over them; neither supersedes Enum or Match. |

Manual and pipeline Paths now enter through shared AST declaration boundaries.
Their established segment resolution, source identities and geometry lowerers
are retained. Native unused malformed Paths are also checked rather than
silently ignored.

## Explicit boundaries

Scalar Static environments currently belong to a mechanical Model/module and
feed direct primitive/Modify fields. Other construction owners retain their
existing typed Template inputs; unsupported scalar consumers fail explicitly.
Inline field tol annotations are rejected with a named-Static repair. General
Record-field tolerances, Template argument propagation, automatic interval
arithmetic, angular PMI export and arbitrary distance/GD&T export are not
implemented here. Assembly Dimension/stackup behavior is retained, not unified
with Model scalar arithmetic. Match is not enabled in arbitrary scalar Static
initializers merely because the syntax tree can represent it.

These are technical qualification boundaries; no unresolved user syntax or
engineering-policy choice is required to use the qualified lane.

Match exhaustiveness across unselected enum alternatives remains a semantic
consolidation follow-up. Each current specialization must resolve an admitted
arm; the guide does not promise universal total matching.
Standalone enum-valued scalar Static bindings likewise remain outside the
primitive scalar binder; Enum values retain their existing Template/Record uses.

## Code-quality review

The pass reuses the shared lexer/AST, dependency/type binder, tolerance IR,
field/statement consumption and AP242 exporter. No new parser route selection
architecture, source macro system, independent expression evaluator, diagnostic
fatality allowlist, global parser state or product-side validator is introduced.
Sweep's obsolete regex readers are removed. The only expanded compatibility
regex is the existing local Profile declaration mask recognizing bare Path.
Legacy manual path-step and WireForm operation readers remain explicitly outside
the completed AST field migration.

## Evidence

Qualification is recorded under ignored
[`artifacts/local/firmament-typed-tolerances`](../../artifacts/local/firmament-typed-tolerances).

| Check | Observed result |
| --- | --- |
| Release solution build | Passed; zero errors, existing nullable/analyzer/platform warnings remain. |
| Focused compiler iteration | 490 passed, including V2 parsing, typed values, mechanical migration, paths, wires and language-service checks. |
| Focused CLI iteration | 21 passed, including structured diagnostics, parse-only levels and documentation links. |
| Core fast lane | 1,068 passed. |
| Full solution lane | 4,810 passed across 20 test assemblies; zero failures/skips. |
| Existing baseline STEP outputs | Ten byte-for-byte SHA-256 matches across symmetry, Profile links, Hole, Template and Set/Pattern specimens. |
| Cleaned canonical PMI example | Byte-for-byte identical to its original STEP artifact: SHA-256 `9F2F24975267BF425D16A2ECDF863F4A94C0FAD172C932F9868BBB836BD46F5D`. |
| Documentation | Seven complete examples from syntax, PMI and revolve guides compiled. |
| Real repository CLI | Thirteen expected outcomes, including typed validate/build/analyze, invalid source, the preserved legacy-PMI failure, bolt, guitar, Industrial ATLAS, house, factory, Sheet Metal and existing PMI. |
| Runtime hygiene | Serial build/test execution with node reuse disabled; final .NET/MSBuild/testhost inventory empty. |

The typed witness reimports as one body with seven faces and bounds
`[-20,-15,0]` to `[20,15,8]` mm. Semantic inspection recovers an 8 mm diameter,
upper/lower allowance magnitudes 0.10/0.05 mm, and a nonempty BRep-face
association. Changing only tolerance metadata without PMI leaves STEP bytes
unchanged; replacing the compile-time references with their nominal literals
also leaves STEP bytes unchanged. Parser and actual compilation reject the
invalid type/unit/dependency cases; normal CLI validate/build agree on known
capability failures. No separate live Helios UI session was run; language-service
and server/Cadmata API projections are covered by the full .NET lane.

The full gate was run after the Release solution build:

```powershell
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

The existing FrictionLab test project has no discoverable tests; it contributes
no cases to the totals. Logs preserve intermediate failures and their repairs,
including inferred Static compatibility and the standard-library PMI cleanup.

## Verdict

**Success for this pass.** Typed tolerances bind through production unified
mechanical admission and explicitly export real hole-size PMI with preserved
allowances and a BRep face association. The discovered legacy datum-reference
omission is closed by shared capability admission, rather than left as a
spelling-dependent bypass. Geometry, source diagnostics and the named language
roles have concrete regression witnesses. This does not declare every language
family or the original global X0 checklist fully qualified. No outstanding user
policy decision is needed for the typed-tolerance syntax.
