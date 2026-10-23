# Imported resources, metadata and dependency locks

Date: 2026-10-21

## Result

**Success.** The real module, snapshot and CLI paths admit the new import forms,
enforce optional dependency receipts, and preserve the qualified geometry. The
complete final solution lane passed after the root-identity hardening.

This pass separates product composition from local imported-body editing. Typed
short includes and Inventory members share the existing AST module graph. No
new whole-document parser, source macro system, geometry strategy or UI validator
was introduced.

## Authoring decisions

- `include bearing: Part = Step("vendor/bearing");` is the short named form.
  `Inventory Record Components { bearing: Part = Step("vendor/bearing") }` is the
  organized catalog form. Native `Firmament("path").Export` supports both.
- Part, Subassembly, Assembly and Scene remain product/module kinds. An import
  alone does not create an occurrence, placement or mate.
- `Part = Step(...)` consumes one body. `Assembly = Step(...)` explicitly requests
  product structure, retaining occurrence hierarchy, local transforms and shared
  leaf geometry. Ambiguous multiplicity-to-flat-assembly fallback rejects here.
- `InlineStep` is the local annotation/surgery binding. `from: Components.part`
  selects a STEP Part; `path` remains compatible. Both fields together reject.
  A hash is source identity, not an instruction to perform surgery: an unchanged
  qualified pinned body now re-exports without a fictitious replacement.
- The conventional optional receipt is `firmament.lock.toml`, one compilation
  root per sidecar. Ordinary compilation without a receipt remains supported.
  An existing receipt is enforced; `validate/build --locked` require it.
  Compilation never refreshes it. `aetheris lock root.firmasm [--update]` writes it.
- Locks contain dependency identity only. STEP is hashed as exact bytes;
  Firmament source as decoded UTF-8 text. Import tolerance and explicit source-PMI
  exclusion remain authoring policy. A changed STEP used by Recognize/Replace/
  Remove refuses even an explicit refresh; reinspect and bind a reviewed revision.
- Existing `sha256` and normalized `textSha256` remain compatible. CLI recovery
  now emits hash-free source plus a generated lock, then compiles that saved pair.
  A failed qualification restores previous output source and receipt. The SDK's
  source-only plan keeps its explicit text pin for existing consumers.
- `metadata:` selects an ordinary typed Static Record. There is no parallel
  metadata field schema. Import receipts retain type/flattened fields separately
  from geometry identity. They do not invent materials, fits, PMI or vendor AP242
  properties.
- Assembly `provenance: Release;` now uses the ordinary Record binder. Canonical
  release fields use camelCase. Nested author Records are supported via their
  `name: String`; legacy PascalCase fields retain compatibility. Supplied author,
  dates, version, description and extra typed metadata remain inspectable.
- `.firmasm` can contain modern textual Assembly DSL. The extension alone is not
  a V1 marker; existing explicit JSON assembly compatibility is unchanged.

## Ownership and diagnostics

The unified syntax parser owns the typed include production. A short include is
adapted to the same Inventory binding operation in memory, not a second resource
resolver. The existing module graph owns dependency resolution, source snapshots,
locks, asset hashes and InlineStep reference projection. Core STEP import owns
BRep admission and product-structure interpretation. Imported assemblies reuse
the existing hierarchy transport with retained BReps, without temporary STEP parts.

Snapshot compilation now consumes supplied `.step`/`.stp` text and lock documents.
It never reads a missing resource from the host filesystem. File-backed import
hashes and import text come from the same byte snapshot. Existing parse caching
is bypassed for import-dependent sources.

New source/resource failures retain stable codes, severity and readable messages.
Lock CLI failures use the existing structured frontend diagnostic record. AST
locations point at the authored import or field; lock-wide mismatches identify
the lock document. Generated STEP hierarchy failures identify the enclosing import
and disclose the absence of an authored Firmament span. Existing downstream
engineering adapters still use enclosing Part locations where exact provenance
is unavailable.

Imported PMI passthrough remains unqualified. New Step bindings reject detected
annotation families unless `geometryOnly: true` explicitly excludes them. The
receipt retains that decision. InlineStep keeps its stricter explicit exclusion
contract for foreign pinned inputs, and reports exclusion. Original files remain
untouched. New annotations still follow the existing explicit PMI owner.

TOML uses Tomlyn 2.10.1 rather than a new hand-written configuration parser.
Unknown keys, malformed receipts, wrong roots, missing entries and mismatched
hashes reject. Empty dependency sets are admitted. Unused old entries are allowed
until an explicit lock command prunes them.

## Regression classification

The first complete run exposed historical negative-test expectations, not accepted
geometry differences:

| Specimen | Former assertion | Current contract |
| --- | --- | --- |
| Missing include in standalone file compilation | Includes unsupported | Includes resolve through the module graph; missing file rejects at the original directive |
| Snapshot ExternalStep with `C:/secret.step` | All STEP resources unsupported | Provided text assets are supported; absolute/root-escaping paths still reject before disk access |
| Imported Part snapshot with absolute InlineStep path | All InlineStep unsupported | Invalid path rejects at the original imported file |
| Pinned source with no replacement | Pin implies mandatory surgery | Unchanged qualified body can re-export; actual surgery still uses its bounded owner |

The original failing source specimens were retained. Tests now assert the correct
owner's rejection, without converting missing resources or invalid paths into
success. The old malformed `file:` field in the filesystem-leak test was replaced
with the legitimate `path:` field so the test exercises path isolation itself.

## Qualification evidence

Run-local logs and generated artifacts: `artifacts/local/imported-resource-modules/`.
Recovery CLI witnesses also use `artifacts/local/imported-feature-assistance/tests/`.

Commands:

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build -m:1 -nr:false --filter "Category!=SlowCorpus" -- RunConfiguration.MaxCpuCount=1
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
```

Focused evidence covers typed include/catalog equivalence, shared geometry
identities, nested STEP transforms, AP242 re-import, explicit annotation exclusion,
snapshot lock presence/mismatch/root/unknown fields, InlineStep path/direct/catalog
bindings without authored hashes, and metadata-only geometry parity. Real CLI
tests generate receipts, validate/build with `--locked`, demonstrate stale-source
refusal, and prove that a hash update cannot rebind CTC-01 surgery.

Manual CLI witness:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll lock artifacts/local/imported-resource-modules/witness/revision.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate artifacts/local/imported-resource-modules/witness/revision.firmament --locked --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build artifacts/local/imported-resource-modules/witness/revision.firmament --locked --output artifacts/local/imported-resource-modules/witness/revised.step --json
```

This source combines an Inventory STEP Part, typed `ImportedAssetInfo` metadata,
`InlineStep from: Components.ctc`, and the bounded CTC-01 recovered-hole revision.
It contains no authored source hash. Validation reports `Built` / `UnifiedMechanical`
/ `V2`. Build retains 115 faces and 312 edges, with diameter 25 mm, depth 50 mm and
the expected two local safety regions. The exact-byte source receipt is
`85a5752da05f53c456ca3a9e038c90358e1d5a3141d1f0d6e5f0970f2356e821`.

The new witness and the separately qualified source-only recovery CLI specimen
emit byte-identical STEP:
`9841594caec194ae1e349476da20b54178190697cf95455e1576ce1c36541863`.
Resource organization and metadata did not change that engineering artifact.

Final qualification:

| Lane | Result |
| --- | --- |
| Release solution build, including WASM runtime | Passed, 0 errors; existing nullable and SQLite WASM import warnings remain |
| Core fast lane | 1,088 passed |
| Final focused resource/directive/snapshot checks | 35 passed |
| Focused CLI lock/recovery/directive checks | 8 passed |
| Final full solution | 5,855 passed, 0 failed, 0 skipped across 20 nonempty test assemblies |
| Full Core corpus | 1,230 passed, included in full total |
| Full Firmament suite | 3,104 passed, included in full total |
| Editable V8 | 7 passed, included in full total |
| CTC-01 CLI witness | Locked validation at Built, successful export, byte-identical STEP parity |
| Diff whitespace check | Passed |

`Aetheris.FrictionLab.Tests` reports no discoverable tests; it contributes no
tests to the total. The canonical fixture, assembly, sheet-metal, interchange,
PMI and scene coverage in the complete lane remained green, including the
Guitar provenance/geometry tests. No test source was rewritten to conceal a
geometry difference. The historical diagnostic reclassifications above and the
empty-lock serialization repair are the explicit qualification changes.

## Code-quality review

Imports, receipts and InlineStep reference binding reuse one module dependency
owner. Metadata uses ordinary Record checking. New frontend work adds no regex
dispatch or broad text rewrite; source projection uses existing AST spans. Root
identity availability is an explicit context flag, not a magic-filename exemption.
A real file named `__provided_source.firmament` still receives normal root checks.
No global mutable lock or resource state was introduced. The compiler's existing
engineering lowerers and kernel planners retain their authority.

Build/test lanes ran serially with shared compilation and node reuse disabled.
The large Firmament test host was observed around 1.9–2.56 GB during corpus
execution and exited between lanes. Final process inspection found no remaining
dotnet, testhost, MSBuild or VBCSCompiler hosts. This is bounded run evidence,
not a general memory-leak proof.

## Explicit boundaries

- No general source-PMI migration, foreign mate recovery or imported modeling
  history reconstruction. Non-leaf foreign product-definition entity IDs are not
  promised as an AP242 byte-for-byte roundtrip contract.
- STEP snapshot text has UTF-8 byte identity; arbitrary original byte encodings
  and BOMs require byte-identical representation or file-backed compilation.
  Portable committed locks require stable dependency encoding/line endings.
- One conventional sidecar per root. Custom lock paths and composition of several
  independently locked workspace roots are not implemented. Directory-only
  `CompileSource` has no real root-document identity; exact root checks use the
  file/project entry points. Its STEP identity is still checked.
- Native concrete Parts with other file-only adapters retain their explicit
  unsupported snapshot-context diagnostic. This pass does not invent context for
  every future external format.
- Import metadata is retained in compiler receipts; automatic arbitrary-vendor
  AP242 property projection is not claimed. The qualified Assembly release
  header/annotation path is separate.
- .NET snapshot and CLI qualification plus WASM compilation do not constitute
  browser interaction or multi-file persistence qualification.

No new user decision is required for these bounded defaults.
