# Firmament directive admission and AST casing

## Result and scope

This pass repairs the ignored mechanical include and schema-formatting failures
from the [October syntax review](../development/FIRMAMENT-SYNTAX-REVIEW-2026-10.md).
It reconciles Profile endpoint links and symmetry fields through the shared AST,
while retaining the existing engineering transforms and materializers.
Qualification evidence is recorded below; this is not whole-language migration
acceptance.

## Language decisions

| Role | Preferred source | Compatibility / limits |
| --- | --- | --- |
| Constructs and types | PascalCase: Model, Feature, Point2 | Existing admitted grammar aliases remain. No arbitrary case folding. |
| Built-in fields | camelCase, including symmetry `across/source/about/count/angle` | PascalCase aliases read the same AST field; duplicate alias pairs reject. |
| Linking words | Lowercase, including pipeline `from/to` and mirror `from` | From/To remain admitted aliases. Rewriting applies only at AST linking positions. |
| Geometry operations | `Reverse`, `Close`, `TraceLoop` | Kept PascalCase as operations, not connecting words. |
| Authored names / domain symbols | Preserve exact spelling | Parameters, Record members, published geometry names, XY, +Z, ThroughAll and schema names are not folded. |
| Schema | One leading document directive | Comments, newlines, a trailing comment and same-line declarations do not disable selection. Unknown, late, nested, duplicate and mismatched selectors reject. |
| Include | Resolved by the documented project/domain consumer | Standalone part compilation rejects any unconsumed include, even if its target exists. No new mechanical module loader. |
| Shipped Profile import | `Use Profile.Modifications` | Existing owner retained; AST-recognized imports are consumed there. Unknown/unowned Use directives reject. |

No decision in this pass requires user input. Point/list policy, Pattern binder
unification, general units/root rules and broader expression consolidation remain
separate work in the syntax review. They are not being deferred because an
approval or product choice is missing.

## Repairs and ownership

- Schema selection reads DirectiveSyntax from the shared parser instead of
  matching whole lines. The original token spans survive selector erasure.
  A native material declaration also cannot be compiled under an incompatible
  specialized selector. Validation carries the authored schema through normal
  engineering preflight rather than compiling only the header-stripped source.
  Direct Loft/SectionChain entry points consume the same selector before material
  binding, including file-backed and source-snapshot assembly definitions.
  The selected schema is retained as compilation context across specialization,
  and the bound engineering owner identifies its admitted domain independently
  of debug parser-route names. The same domain rule gates expanded input before
  geometry execution. A Template-produced Revolve under WireForm rejects, including
  parse-only validation; it succeeds when explicitly selected as Mechanical.
  Parse-cache keys include the schema context, so the same resolved source cannot
  reuse admission from another frontend.
- Part admission requires consumption of every directive before expansion.
  Unsupported includes fail with `firmament-v2-directive-unconsumed`, a source
  span and an explanation of the project-loader boundary. The existing Assembly
  and source-snapshot loaders retain their authority.
- Profile.Modifications imports use AST spans. Comment/string contents cannot
  trigger the import. This removes its old source-wide import regex.
- Mirror/radial headers, fields, Plane/Axis inputs, seed centers and feature
  overrides bind from AST nodes. Consumed scopes close against unknown and
  duplicate fields. Transform math, instance identities and engineering
  capability limits remain with their existing owners.
  Concept-derived planes retain Concept IR resolution and enter it through the
  same established spelling boundary, rather than a second plane evaluator.
- Profile/Path pipeline expressions are consumed from the shared AST. Endpoint
  links and alias positions are recognized structurally; calls and arithmetic
  are still rejected as pipeline stages. Inline guide extraction and other
  unmodified profile adapters retain their existing bounded ownership.
- Feature overrides replace the exact parsed value span. Dense adjacent seed
  fields no longer let an override consume a following termination field.
- CLI schema failures use the structured validation diagnostic contract in both
  validate and build; the language service projects original directive spans.

The source formatter projects the symmetry owner's field vocabulary and AST
link positions. It does not maintain a second symmetry field schema or rename
authored members. In particular, adding a lowercase `from` link does not reserve
an existing authored name such as `Box from`. Remaining regex use in the modified symmetry owner is only
the preexisting generated-identifier sanitization; header/field extraction and
brace scanning have been removed. Negative string fast paths only avoid parsing
when the relevant keyword is absent; they grant no admission.

## Qualification

Expanded focused Release checks: 144 compiler/parser tests and 19 CLI tests passed.
These include normal validate/build rejection, parse-only directive rejection,
comments/spacing, all six existing symmetry fixtures, alias geometry parity,
published identities, unknown/duplicate fields, malformed directives, direct
Loft/snapshot compatibility, authored `from` names and the dense override witness.
The specialization regression verifies both rejection under WireForm and a real
successful build under Mechanical; it also checks schema-specific cache admission
and rejection of a caller context that conflicts with an authored header.
See the [compiler regressions](../../Aetheris.Kernel.Firmament.Tests/FirmamentDirectiveAndCasingTests.cs)
and [CLI regressions](../../Aetheris.CLI.Tests/FirmamentDirectiveCliTests.cs).

All ten pre-change Release STEP hashes match byte-for-byte after rebuilding:
the six canonical symmetry files, preferred-profile-links, through-hole,
generic-mounting-plate and pattern-feature-map. The
[baseline](../../artifacts/local/firmament-directives-casing/baseline.json) and
[comparison](../../artifacts/local/firmament-directives-casing/step-parity.json)
are ignored local evidence. Alias tests additionally compare the new preferred
spellings to their retained PascalCase sources. All five complete examples in
syntax.md and revolve.md compile through the normal in-memory engineering path.

The required fast lane passed 1,068 tests. The solution Release build succeeded;
its existing nullable/analyzer/platform and SQLite WebAssembly warnings remain.

The full solution lane passed **4,775 tests across 20 test assemblies, zero
failures or skips**, including all 2,326 Firmament tests. Commands ran serially:

```sh
dotnet build Aetheris.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build -m:1 -nr:false --filter "Category!=SlowCorpus" -- RunConfiguration.MaxCpuCount=1
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
```

MSBuild node reuse and the CLI build server were disabled for these runs. The
existing FrictionLab test project reports no discoverable tests; it is not
included among the 20 executed test assemblies. The full lane includes the
corpus tests omitted by the fast lane. Final logs are under ignored
`artifacts/local/firmament-directives-casing/`.

Seven [real CLI showcase checks](../../artifacts/local/firmament-directives-casing/cli-showcases.json)
also passed, using each domain's documented consuming path:

| Source | Command / evidence |
| --- | --- |
| Threaded hex bolt | `validate`, valid / Built. |
| GuitarX0 | `asm inspect`, success through the Assembly project loader. |
| Industrial ATLAS | `asm inspect`, success through the Assembly project loader. |
| Warm-modern house | `scene inspect`, success. |
| FactoryX0 | `scene inspect`, success. |
| Four-wall sheet-metal tray Template | `validate`, valid. |
| Hole-diameter / datum PMI example | `validate`, valid / Built. |

Assembly includes are intentionally exercised through `asm inspect`, rather than
the standalone-part `validate` command. Scene inspection is not a new claim of
manufacturing simulation or a pre/post hash comparison for every Scene artifact.
The byte-identical geometry claim is bounded to the ten recorded STEP files;
the full regression suite supplies broader existing behavior coverage.

Documentation relative links, balanced fences and `git diff --check` pass.
Runtime inspection after qualification found zero remaining dotnet, testhost,
MSBuild or VBCSCompiler processes. No background service was created.

## Remaining boundaries

This is not a new AST expansion engine, a universal include system, a global
capitalization migration or an additional parser route for geometry. Existing
Template/Feature/Pattern expansion and other domain frontends retain their
documented owners. Explicit serialization-shaped V1 compatibility remains
separate. The historical review witnesses remain preserved as pre-repair evidence.

## Verdict

**Success for this pass.** The known ignored-include and schema-formatting
witnesses fail truthfully, schema trivia cannot bypass selection, and preferred
Profile/symmetry spelling binds through AST ownership with retained aliases.
The recorded accepted STEP outputs remain identical. No deferred item awaits a
user decision; the wider syntax review remains the register for subsequent work.
