# Firmament lowercase AST migration and spelling reconciliation

Date: 2026-10-14. This continues the production migration described in
[FIRMAMENT-DOMAIN-FRONTEND-MIGRATION](FIRMAMENT-DOMAIN-FRONTEND-MIGRATION.md).

## Production ownership

The shared recursive-descent parser now represents historical `units mm`,
`solid name: Primitive`, Box `with` derivation and selector alias associations.
The primitive and Hole binders are shared with native mechanical source. There
is no new whole-document regex dispatch or source-rewrite pass. The obsolete
solid scanner, direct/derived primitive parsers, exposure line parser and
Modify/Hole/region/cut regex readers are removed.

`UnifiedCompatibility` identifies documents whose primitive, derivation,
exposure, Hole and region/cut scopes have AST admission. Both normal validate and build
consume their resulting engineering records; unknown statements and fields fail.
Dimensional defaults are explicitly limited to the historical millimetre context.
Native declarations retain explicit length units. Unresolved face aliases retain
the existing semantic resolver and engineering diagnostics.

`CompatibilityExtensions` identifies documents containing existing typed let,
tolerance, PMI, manufacturing/feature concepts, process Templates, imported-face
recognition/replacement, lattice fill adapters. Their primitive
and Hole inputs now bind from AST, but their remaining semantic scopes are still
source-owned. They are not claimed to have whole-document AST admission. The
old `LegacyLowercase` route name is retired; explicit V1 serialization remains
separate. No legacy semantic owner is deleted before its consumers migrate.

## Spelling decisions

New authoring uses PascalCase constructs/types, camelCase built-in fields,
lowercase linking words (`using`, `as`, `on`, `over`, `with`) and preserved
engineering symbols. User identifiers, recipe parameter labels and published
members remain case-sensitive. PascalCase field and linking aliases remain
compatible at their established vocabulary positions; arbitrary case folding
is not introduced. Uppercase `MODIFY` remains a bounded existing compatibility
alias and still rejects unsupported imported-body edits.

`as` now works in the shared expression parser, Profile pipeline binder,
convention formatter and language-service token classification. Formatting finds
alias nodes in the shared AST; it does not guess from an identifier's spelling.
Profile source adapters retain bounded `As|as` recognition while their remaining
semantic grammar is migrated. `From`/`To`, `Reverse`, `Close` and `TraceLoop` are
unchanged; this does not invent a global lowercase operator grammar.

The public [syntax reference](../public/firmament/syntax.md) now begins with the
compile-time Model/Struct/Feature/Pattern mental model, vocabulary rules and a
preferred mechanical example. Historical grammar has a distinct compatibility
section. Feature delegation examples live with Feature. Existing source is not
mass-renamed, and legacy syntax is not presented as a second preferred style.

## Diagnostic changes

Migrated scopes close their fields and statements at the actual AST owner.
Syntax diagnostics keep their shared `firmament-syntax-*` codes and original
spans rather than being renamed to match old code prefixes. In particular,
`@front:` in a Box override is a located syntax error; this previously invalid
fixture remains invalid. Corpus checks must verify actual V2 disposition and
absence of V1 fallback, not assume every error starts with `firmament-v2-`.

## Qualification

Qualification is in progress; final counts and geometry evidence are recorded
when the complete serial regression lane and CLI witnesses finish.

## Runtime resource checks

The interrupted work left no test hosts or CLI processes alive. Existing MSBuild
and compiler servers belonged to the open Visual Studio session and were left
untouched. Subsequent builds disable node reuse, MSBuild server reuse and shared
compilation. Test assemblies and collections run serially, with process/memory
snapshots between lanes. A prior one-off dotnet hostpolicy launch error did not
recur in the explicit-project retry; it is not attributed to the parser without
reproduction. No runtime memory leak has been established by these observations.

## Remaining boundary

The remaining lowercase families are the explicit compatibility extension
owners above; region/cut now binds its existing side-hole route policy from AST. Native Concept/profile
adapters, expansion, Wire/Sweep, Assembly/Scene and other domain frontends also
remain as listed in the preceding migration report. This is a production cutover
of the stated families, not whole-language migration completion.
