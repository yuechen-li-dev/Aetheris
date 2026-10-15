# Firmament compile-time values: Record → Template → Feature → PMI

## Contract and motivating case

A dimensional requirement retains its nominal, type, tolerance and authored
provenance when passed as a Record member, scalar Template argument, whole
Record argument or Feature-local alias. Geometry consumes the nominal. Only
an explicit `Pmi dimension:` projects the selected requirement to AP242.

The complete production witness is
[`record-template-feature-tolerance.firmament`](../../fixtures/Canonical/PMI/record-template-feature-tolerance.firmament).
Its diameter is 8 mm with upper allowance +0.10 mm and lower allowance −0.05 mm.
Before this pass the equivalent literal construction/PMI built, while the
Record projection failed with PMI dimension-type/diameter errors. Original
evidence and a pre-pass literal STEP baseline are preserved under ignored
`artifacts/local/firmament-value-binding/`.

## Repairs and ownership

The per-compilation value environment reuses the existing typed scalar graph
binder and tolerance IR. Record types remain owned by the existing Record
schema; this pass does not introduce another field schema or expression
evaluator. Scalar/Record inputs bind before specialization, newly specialized
Model scalars bind before Feature expansion, and mechanical admission consumes
those checked values without evaluating them a second time.

Existing Template/Feature expansion carries opaque references to immutable
checked values. The mechanical AST projects nominal literals into its existing
primitive/Modify owners; the PMI binder receives the original typed value.
Record views retain their authored identities when forwarded through nested
specialization. Internal transport allocation does not determine Template
specialization identity.
Already flattened Record views are not expanded again, preserving nested
parameter receipts. Cyclic Record references reject before argument flattening
can recurse indefinitely; supported Record nesting is bounded to 128.

Template and Feature value substitution now uses shared AST declaration/scope
spans and lexer token spans. Field labels, named argument labels, declaration
names, local Feature binders, strings and comments are protected. This repairs
a reproduced collision where a lowercase Template parameter could replace a
Hole field label or a shadowing Feature parameter. It replaces the existing
value-substitution implementation; it adds no whole-document parser dispatch
or global source rewrite pass. Full Template/Pattern/Feature AST expansion is
still separate work.

Record initializers retain the full AST expression rather than a regex-matched
literal prefix. Duplicate fields reject. Existing Version/Date field symbols
retain their complete spelling using the Record owner's type metadata and
bounded token boundaries. Existing wide Int literals in Record/Template data
are preserved; Model Static's existing narrower integer policy is unchanged.

## Qualified language behavior

The compatibility review also reproduced an ignored `tol` in a legacy Record
array: normal build succeeded with no diagnostic after the array reader kept
only `8mm`. The source is preserved as
[`collection-tolerance.firmament`](../../fixtures/Regression/CompileTimeValues/collection-tolerance.firmament),
with its pre-repair CLI result under `collection-before.json`. A bounded AST
consumption check now rejects tolerance nodes in Static values that have no
checked scalar owner before collection erasure. Collection/Table support is
explicitly deferred; its unsupported annotations cannot silently disappear.

* Record Length/Angle field initializers may declare `tol`.
* Direct Record members feed admitted primitive/Modify fields and explicit PMI.
* Scalar and whole-Record Template/Feature parameters preserve requirements.
* Nested Records, checked `with` overrides and nested Templates retain metadata.
* Scalar Template defaults and specialized Model Static aliases use the same
  checked environment; Feature-local direct aliases retain allowances.
* Nominal `Require` comparisons do not certify tolerance intervals.
* Arithmetic retains nominal-only semantics and warns when an allowance is
  dropped. An explicit new `tol` introduces a new requirement and retains its
  input provenance.
* Replacing a Record field with an untoleranced value removes its allowance.
  PMI cannot invent the missing requirement or override a selected one.

The authoring contract and complete example are maintained in
[`syntax.md`](../public/firmament/syntax.md#typed-tolerances-and-pmi), with
corresponding [Template](../public/firmament/templates.md) and
[PMI](../public/firmament/pmi.md) updates.

## Diagnostic and provenance contract

Bound values carry structured origin receipts: source file, source span,
binding name and an optional location limitation. PMI retains the authored
projection identity and its requirement's binding chain. CLI validation adds
`origins` to value rows and `dimensionOrigins` to PMI rows; existing fields
remain available. These are compiler receipts, not extra AP242 annotations.

The original Record initializer is precisely located. Parameter/local binding
steps identify their original enclosing declaration. Unavailable expanded or
imported consumer locations disclose the limitation and anchor to the original
requirement or source module. This is not a claim of complete expansion maps.
Located compile-time diagnostics survive later expansion and normal build
failure; compilation does not retry them through the legacy expansion path.
Original-source diagnostics are not overwritten by unrelated unbound syntax
errors from an otherwise admitted domain symbol.

## Qualification

Focused tests exercise acceptance/rejection, nominal dimensions, hole identity,
real AP242 reinspection and geometric face association, topology, exact STEP
parity, provenance, nested forwarding, overrides, defaults, local shadowing,
and validate/build/parse-only agreement. The invalid source witness is
[`record-unit-mismatch.firmament`](../../fixtures/Regression/CompileTimeValues/record-unit-mismatch.firmament).

The Release solution builds with zero errors (19 existing nullable, analyzer,
platform/WASM warnings). Focused compiler tests pass 106 cases and focused CLI
tests pass 10. The Core fast lane passes 1,068 cases. Final full-suite totals
are **4,840 passed across 20 test assemblies, zero failed and zero skipped**.
This includes 2,381 Firmament, 507 CLI and 1,201 full Core cases; fast-lane
results are not used as a substitute for that complete run.

The CLI witness reimports as one body with seven faces and bounds
`[-20, -15, 0]..[20, 15, 8]` mm. Semantic AP242 inspection recovers one diameter
with nominal 8 mm, allowances +0.10/−0.05 mm and a nonempty cylindrical face
association. The typed export is byte-identical to the pre-pass literal STEP.
Twelve earlier STEP specimens also retain their exact bytes. These cover
symmetry, profile links, primitive holes, Template/Pattern construction and
existing literal/typed PMI.

Eight complete documentation examples compile. Fifteen real CLI checks pass,
including all three admission commands for an invalid Record requirement,
rejection of unsupported collection annotations, threaded bolt, GuitarX0,
Industrial ATLAS, WarmModernHouse, FactoryX0, sheet metal and existing PMI.
Assembly/Scene checks use their existing inspection commands; they are not a
new assembly STEP byte-parity claim or a live Helios UI qualification.

Reproduction commands:

```powershell
dotnet build Aetheris.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Canonical/PMI/record-template-feature-tolerance.firmament --json --explain
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/PMI/record-template-feature-tolerance.firmament --output artifacts/local/firmament-value-binding/typed.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll analyze artifacts/local/firmament-value-binding/typed.step --json
```

Detailed machine-local evidence remains in ignored
`artifacts/local/firmament-value-binding/`, including `qualify.ps1`, build/test
logs, the before/after specimens' CLI output, topology/PMI inspection,
documentation receipts and STEP comparisons.

## Code-quality review

The new lane reuses the existing scalar evaluator, Record field metadata,
diagnostic objects, mechanical lowerers and PMI exporter. Both admission
commands consume the checked mechanical document. No diagnostic-code fatality
allowlist, new parser route, global rewrite pass, product-side validator or
geometry operation is added. The only value-substitution changes replace the
two existing owners' regex substitutions with shared token/AST span handling;
Record Version/Date recognition is local and schema-directed. The environment
is per compilation, with no global mutable value registry or background host.
Source-located receipts survive specialization, and missing consumer maps are
disclosed. `git diff --check` passes; no repository formatting pass is included.
Builds/tests ran serially with MSBuild node reuse and shared compiler hosts
disabled. Final process inspection found zero dotnet, MSBuild or testhost
processes remaining. This is runtime hygiene evidence, not a general memory
profiler certification.

## Remaining boundaries

This pass does not implement interval/tolerance-stack arithmetic, angular or
general distance/GD&T PMI export, toleranced Table/collection elements, arbitrary
nested scalar environments or every material domain's expression positions.
Existing Profile/Compose and other domain owners retain their established
typed inputs and explicit admission limits. Enum-valued scalar Static and
universal Match exhaustiveness are not added. Existing numeric-policy seams
are not declared globally reconciled.

Complete AST expansion/source maps and the remaining domain owner migrations
remain tracked in the [decision register](../development/FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md).
No user syntax or engineering-policy decision blocks the qualified value lane.

## Verdict

**Success for the compile-time value-binding pass.** The motivating source now
builds through normal unified mechanical admission and exports its requirement
correctly. Scalar/Record transport preserves nominal geometry, allowances and
authored receipts; unsupported collection annotations and cyclic Record data
reject rather than disappearing or recursing indefinitely. Existing qualified
geometry retains its bytes. This is not a claim that all remaining frontend
owners, expressions or expansion maps have migrated.
