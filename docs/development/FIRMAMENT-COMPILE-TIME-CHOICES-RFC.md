# Firmament compile-time choices: RFC assessment

Date: 2026-10-16. Baseline evaluated compiler: `c8f48b5b`.

Implementation status: implemented in the production admission path. The baseline
findings below are historical; current qualification and compatibility decisions
are recorded in the implementation section at the end.

## Decision

Proceed with a bounded, fully typed compile-time choice feature. Use lowercase
`match` as the preferred expression keyword; retain `Match` for compatibility.
Accept `Options` as a contextual declaration alias for `Enum`, with exactly the
same nominal type, cases, scope, diagnostics and host representation. `Enum`
remains valid. The alias must not introduce a second kind of choice.

At the baseline, the existing implementation was **not safe to advertise as
exhaustive across Firmament**. The initial assessment preserved executable
requirements and corrected the documentation. The approved implementation now
extends those requirements into production binding; it does not add a new parser
route or a runtime branch engine.

## Intended authoring model

```firmament
// Approved and implemented syntax.
Options MountSize { Compact Standard }

Template<size: MountSize>
Struct MountingPlate {
    Box stock {
        size: [match size {
            Compact => 40mm
            Standard => 80mm
        }, 20mm, 5mm]
    }
}

Struct SmallPlate = MountingPlate<size: Compact>
```

This expresses a finite product choice without duplicating the construction
recipe. A Record can bundle the choice with other requirements; a Table is often
clearer for a large matrix of standard dimensions. Pattern constructs every
entry in a collection; `match` produces one value. `Options` means **choose one**,
not a bit mask or a set of enabled alternatives.

Use PascalCase for declaration kinds, type names and case names; camelCase for
value names and fields; lowercase for the expression keyword `match`. Neither
alias should change authored symbol casing.

## Baseline production-path experiment

The initial [26 source specimens](../../fixtures/Regression/CompileTimeChoicesRfc/README.md)
run through the repository CLI's normal `validate` and `build`, followed by STEP
`analyze` for accepted sources. These are requirement witnesses, including negative
cases; they are not speculative kernel fixtures. Explicit references prove that
the Feature's Hole return type and the Concept's Box/Modify/EdgeFinish consumer
already construct geometry without choice binding.

| Requirement | Baseline observation | Initial disposition |
| --- | --- | --- |
| Compact / Standard Template variants | Both validate and build | Useful existing capability |
| Reorder Template arms | Same result | Preserve |
| Boolean Template choice | Builds chosen dimensions | Preserve and consolidate |
| Enum-valued Record passed into Template | Builds chosen dimensions | Preserve and consolidate |
| Concept chooses Box3 bounds | Builds chosen bounds | Preserve |
| Missing unselected Template case | **Validates and builds** | Safety defect |
| Missing selected Template case | Rejects with generic unsupported-construct | Rejection is honest; diagnostic incomplete |
| Duplicate selected Template arm | **Validates and builds first arm** | Safety defect |
| Unknown Template arm | **Validates and builds** | Safety defect |
| Wrong dimensional type in unselected Template arm | **Validates and builds** | Safety defect |
| Unresolved name in unselected Template arm | **Validates and builds** | Safety defect |
| Duplicate enum case in Template lane | **Validates and builds** | Safety defect |
| Unknown selected enum value | Rejects with typed argument diagnostic | Preserve |
| Missing / duplicate Concept arm | Rejects with coverage / duplicate diagnostic | Preserve without cascades |
| Incompatible Concept arm type | Rejects with arm-type diagnostic | Preserve |
| Lowercase `match` | Rejected later as malformed primitive | Missing syntax/binding integration |
| `Options` declaration | Rejected as unknown parameter type | Missing alias integration |
| Inline semicolon-separated Template arms | Reject after specialization | Expansion extraction depends on line layout |
| Scalar Static choice expression | Rejects as unsupported expression | Missing shared value binding |
| Feature choice in Hole diameter | Rejects as invalid Diameter | Missing shared value binding |

All 26 cases have matching validate/build acceptance outcomes. That agreement
does not establish correctness: six invalid definitions are accepted by both.
The RFC gate meets 14/26 requirements: six false acceptances, five unsupported
proposed forms, and one rejection without the required coverage diagnostic remain.
The proposed gate also requires the missing-selected-arm case to report
non-exhaustiveness, rather than accepting an unrelated generic failure.
There is a diagnostic presentation gap as well: validate includes detail suffixes
in several `code` values, while build separates the canonical code from that
detail. The runner checks the code family and records this normalization mismatch
separately; it does not claim the JSON codes are identical. Concept failures also
include secondary spatial-derivation and generic Phase3 errors at enclosing anchors.

Six accepted choice specimens (two Template variants, reordered arms, Boolean,
Record argument and Concept bounds) reproduce their explicit reference STEP
bytes. Imported world-space bounds, surface families and topology also match.
The ordinary compact stock has bounds `[-20,-10,0]..[20,10,5]` mm; standard stock
has `[-40,-10,0]..[40,10,5]` mm. These asymmetric bounds verify the selected result,
not just the presence of a diagnostic. Concept parity uses a chamfered reference.
No general volume, PMI, browser or animation qualification is claimed here.

## Root causes and correct ownership

The structural parser already has `MatchSyntax`, scrutinee expressions, arms and
source spans. `FirmamentV2TemplateExpansion.ResolveTemplateMatches` nevertheless
extracts a selected arm from text before downstream value checking. It checks
neither the complete declared case domain nor unselected arms; its scalar arm
extraction ends at a newline. `ParseEnums` in that owner also collapses case names
into a set without rejecting duplicates.

`ConceptIr` has a separate evaluator with coverage, duplicate and arm-type checks.
Its enum reader has different separator behavior from the Template reader.
Scalar Static and Feature binding do not gain these semantics merely because the
parser recognizes a Match node. Alias-only edits would leave all of this intact.

The implementation should extend the existing compile-time value/dependency binder
and lexical data-symbol owner. Bind a nominal enum domain and typed Match node
once; let Template, Record, Static, Feature and Concept consume that result.
Remove the text-selected Template Match path when qualified. Do not introduce a
new expression engine, source preprocessing pass or diagnostic allowlist.
`Options` differs in length from `Enum`, so routing it through the existing
equal-length casing normalizer would also break that normalizer's span contract.
Handle the alias structurally and retain original source spelling/spans.

## Safety contract for implementation

1. A choice type is finite, nonempty and nominal. Reject duplicate cases and
   invalid/unknown cases. Respect lexical scopes and module exports; two types
   sharing a case name do not become interchangeable.
2. A scrutinee must bind to a compile-time enum or Bool. Support existing typed
   Boolean expressions through their owner; reject unresolved or cyclic inputs.
3. Require every enum case, or both Boolean cases, exactly once. Initially omit
   wildcard arms, guards, payload enums, fallthrough and implicit defaults. Adding
   a case must invalidate incomplete consumers rather than choose a fallback.
4. Resolve names and type-check **all** arms before selecting. Arms must satisfy
   one contextual result type, including dimensional and nominal Record types.
   Match is an expression, not an arbitrary statement-block executor.
5. Evaluate only the selected value after admission. Do not build unselected
   geometry or require unselected kernel construction to succeed. Type checking
   all arms is distinct from materializing all arms. Rejecting undefined names
   in every arm does not imply executing every arm.
6. Preserve the selected value's tolerance, semantic identity and source
   provenance. A matching nominal number alone is insufficient. PMI remains an
   explicit projection; selecting a toleranced value must not export PMI by itself.
7. Use the same admission result for validate and build. Diagnostics must identify
   the scrutinee, enum and missing/duplicate/unknown case or conflicting arm type,
   with the original arm span. Avoid the present generic error and Concept cascade.
8. Erase selection before engineering lowering. No runtime branch, state machine
   or viewer-owned interpretation is needed. Preserve existing named application
   identities and selected-reference geometry.
9. Carry `Options` through language-service, schema, host/SDK and formatting
   projections as the same Enum type. The parser must accept both Match spellings
   at expression positions without globally rewriting user identifiers or strings.

The minimum useful complete implementation covers Static initializers, Record
fields, Template arguments/defaults/bodies, Feature value arguments and Concept
values using this shared contract. Statement-level conditional construction,
switching Feature recipe types and arbitrary conditional Assembly statements are
separate extensions; this RFC does not silently promise them.

## Qualification and remaining gate

Reproduce the RFC gate with:

```powershell
dotnet build Aetheris.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
python fixtures/Regression/CompileTimeChoicesRfc/run.py
```

The initial runner intentionally exited nonzero while requirements remained unmet.
The implementation gate now requires every specimen to pass.
Do not weaken negative cases to make the RFC green. Raw CLI JSON, STEP artifacts,
geometry comparisons and test logs stay in ignored
`artifacts/local/compile-time-choices/`.

Qualification completed for this evaluation:

- Release solution build: zero errors, 26 warnings (including WebAssembly
  SQLite native-signature warnings); `build.log` retains the output.
- Core fast lane: 1,076 passed; `fast.log`.
- Full solution lane: 5,244 passed across 20 projects, zero failures or skips;
  includes all 2,752 Firmament tests and all seven Editable V8 tests; `full.log`.
- RFC gate: 14/26 requirements met, exit 1 as intended; `qualification.log`
  and `results.json`. All six available choice/reference geometry pairs also
  have identical STEP bytes. The failed requirements are listed above.
- Repository content-location guard and diff whitespace checks passed.

These existing-suite results establish the evaluation assets' compatibility;
they do not certify the unimplemented feature. Compiler semantics and existing
goldens were not modified. .NET lanes ran serially with node reuse disabled.

The implementation gate must additionally cover nominal-type confusion, scoped
shadowing/imports, empty enums, nested/dependent matches, cycles, selected-only
evaluation, typed-tolerance/PMI provenance, stable identities and SDK diagnostics.
Those are unqualified here, not claimed successes. Re-run the full existing suite
and canonical geometry qualification when compiler semantics change.

## Initial assessment verdict (historical)

**RFC evaluation complete: worth implementing, with one shared typed admission
path. Existing implementation not accepted as exhaustive.** The geometry witnesses
show practical value, and the negative cases isolate the semantic work needed.
Lowercase `match` and the `Options` alias are appropriate surface decisions once
that contract is implemented. No further user language decision is required for
this bounded design.


## Implementation

The shared finite-domain contract now owns AST Enum/Options declarations and
Match arm admission. Template specialization no longer selects a branch with a
regex or cuts a scalar arm at a newline. Existing scalar dependency binding owns
selected arithmetic, Boolean comparisons and tolerances; existing spatial and
engineering lowerers still own geometry. Record schemas and spatial-owner field
metadata type-check arms without evaluating unselected geometry.

Static enum bindings retain their nominal type. Scalar Static and Record values,
Template arguments/defaults/bodies, Feature arguments/defaults/locals and Concept
values consume the same choice admission. Typed Record results, including checked
`with` overrides, stay nominal. Binding preserves tolerance receipts and explicit
PMI projection. Source and host expansion APIs reject invalid choices instead of
erasing them, and preserve explanatory diagnostics at their existing API boundary.

Additional repairs discovered during qualification:

- Concept member references such as Bounds in a selected Grid bind in their
  enclosing AST scope. Nested Template and Feature definitions retain their own
  selection owner rather than being evaluated by an outer specialization.
- Checked enum arguments are qualified internally so a case cannot be captured by
  a callee parameter with the same name. Explicit Template arguments resolve in
  caller scope; formal-parameter alias resolution applies to defaults.
- AST declaration-name spans protect `let diameter` from being capitalized as the
  Hole field Diameter. Actual construct fields still use existing casing aliases.
- The source-expansion and Forge host boundaries carry the same error messages;
  the public Enum schema is identical for Options.
- Claude's upstream `941aabe7` inspector copy update was fast-forwarded without
  conflicts; its reader-facing read-only explanations are retained.

### Compatibility decisions

`Enum` and `Options` are one nominal declaration kind. Both Match spellings remain
admitted, while the formatter prefers lowercase `match` at expression positions.
A callable authored name `match(...)` remains usable. No global token replacement
changes authored names, strings, Record fields or domain symbols.

The existing StandardProducts flange uses `Pattern ... Over Match P.BoltCount`
to select the 4-, 6- or 8-point collection. This is legitimate shipped source.
Its explicit finite Pattern-source context retains bounded numeric selection;
all arms bind to compatible collections, duplicate/unknown cases reject, and an
unlisted value fails with the admitted cases. This compatibility decision does
not admit ordinary Int value matching or assert exhaustive coverage of integers.
The existing 8-bolt geometry witness and an unlisted-count rejection cover it.

The original static-choice specimen omitted required Model units. Added
`units: mm`; the baseline unsupported-expression error had hidden that independent
source defect. This corrects the witness, not unit admission. Original negative
specimens and their required code families remain intact. The corpus additionally
covers nested matches, scoped/private data, defaults and locals, enum identity,
cycles, qualified cases, spatial field/name errors, selected-only evaluation,
Record results, typed tolerances and PMI.

### Diagnostic contract

Choice failures have stable codes, error severity, explanatory messages, source
file projection and spans. Missing/duplicate/unknown cases identify the domain
and case; type errors identify the arm or field and conflicting types. Static
and retained Template arm locations use original spans. Feature and Concept
adapters can project an enclosing original declaration instead of an exact arm;
the diagnostic location note identifies that enclosing context. Expanded Static
values disclose unavailable original-expression provenance rather than inventing
an exact location. This pass does not introduce a general source-map debugger.

No failed admission retries V1. Existing warnings remain warnings; a nominal
scalar comparison does not manufacture a tolerance-drop arithmetic warning.

### Final qualification

Detailed logs and STEP artifacts remain under ignored
`artifacts/local/compile-time-choices/`.

- Production CLI gate: 57/57 requirements met, 34 accepted and 23 rejected;
  validate and build agree for every specimen. All 29 accepted/reference pairs
  have matching imported geometry and byte-identical STEP. See `final-cli.log`
  and `final-cli/results.json`.
- Original FrontendX4 authoring corpus: 32/32 pass, including the chimera and its
  recorded geometry, with no reference updates; `frontend-x4.log`.
- Both new complete documentation examples validate and build; `doc-examples/`.
- SDK build and all 14 SDK transport tests pass; `sdk-build.log`, `sdk-tests.log`.
  A real headless Edge WebAssembly worker builds Options, rejects a missing arm
  with an explanatory original-source diagnostic, retains byte-identical last
  valid STEP after that failed draft, and rebuilds restored valid source;
  `browser.json` and `browser.log`. This is SDK/browser qualification, not a new
  Helios UI acceptance run.
- Final Release solution build: zero errors (30 warnings, including existing
  nullable/analyzer and WASM SQLite interop warnings); `final-build.log`.
- Choice tests: 65/65; `final-choices.log`. Fast Core lane: 1,076/1,076;
  `final-fast.log`. Complete solution lane: 5,309 passed across 20 projects,
  zero failures or skips, including all 2,817 Firmament tests and all seven
  Editable V8 tests; `final-full.log`. The FrictionLab project reports no tests
  available; it is not counted as a passing test project.
- .NET lanes ran serially with node reuse and shared compilation disabled.
  Final process inventory contains no remaining dotnet/testhost processes.
  The tests' temporary legacy assembly migration output was removed.

The decoded invalid enum argument is anchored to its original Template application
(line 9 in the unknown-choice witness), with an enclosing-location note.

The existing CLI validation adapter still emits a colon-suffixed legacy Template
argument code alongside its canonical structured diagnostic for the unknown-choice
specimen. Both commands reject it and preserve the useful message. The corpus
records that normalization difference explicitly rather than claiming universal
CLI code normalization. A general cleanup of legacy diagnostic adapters is outside
this choice-binding change.

### Code-quality review and verdict

The review checks the parser/spelling seam, shared type admission, selected-value
transport, scalar evaluator reuse, scoped binding, diagnostic provenance and the
existing Template/Feature/Concept adapters. No new regex, whole-document dispatch,
error-code fatality allowlist, source macro system or kernel implementation was
introduced. Removed Template and Concept Match arm scanners are replaced by the
same AST contract; generated source remains at existing lowering boundaries.
Per-compilation state owns choice dependencies. Ordinary Enum dispatch does not
use JudgmentEngine. Diff whitespace, changed-file UTF-8 validation and the
repository content-location guard pass; all new source evidence lives under
`fixtures/Regression/CompileTimeChoicesRfc/`.

**Success.** The bounded compile-time choice capability works through production
admission, engineering build and the browser SDK, with matching reference geometry. Statement branching, payload
enums, wildcard/guard syntax, general Int matching, arbitrary geometry-result
switching and exact expanded-source maps remain outside this pass. No further
user language decision is required for the implemented surface.
