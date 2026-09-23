# Firmament LX-X0 — language experience and semantic selector contract

**Verdict: Blocked.** The requested accepted contract cannot be implemented within the mission's own constraints against new Firmament syntax, a second language definition, and selectors derived from transient mesh IDs. This report records the precise boundary; no compiler, Web SDK, or Helios behavior was changed.

## Decisive witnesses

| Required witness | Current authoritative behavior | Evidence |
| --- | --- | --- |
| `Helix H { ... }` completion and valid snippet | The Mechanical parser rejects `Helix` as an unknown canonical declaration. | `dotnet run --project Aetheris.CLI -- validate artifacts/local/lx-x0/helix-probe.firmament --json` returned fatal `firmament-v2-canonical-declaration-unknown:Helix`. `FirmamentV2Parser.ScanCanonicalTopLevelDeclarations` has no Helix admission. |
| Existing helical authoring | `AxisCoil` is valid inside a `WireForm`, with Radius, Turns, Pitch or Height, Handedness, and optional StartPhase. It is a different admitted construct, not a `Helix` alias. | `dotnet run --project Aetheris.CLI -- validate fixtures/Canonical/WireForm/axis-coil.firmament --json` returned `status: valid`, 0 diagnostics, 1 operation. See `docs/public/firmament/wire-form.md`. |
| Runtime face → source selector | The Web SDK emits `face:<FaceId.Value>` from tessellated/reimported B-rep faces and returns it from `resolveSelection`. It has no compiler-owned correspondence from that ID to an authored axis/name/selector. | `Aetheris.Web.Runtime/Program.cs`, `WebMeshBuilder.Build`; `Aetheris.Web.Runtime/sdk/src/index.js`, `ModelSession.resolveSelection`. |
| Runtime edge → source selector | The V2 parser explicitly rejects `edge(...)` exposure selectors. | `FirmamentV2Parser.ParseExposures` adds `SelectorUnsupported` for `edge(...)`; `FirmamentV2ParserTests` qualifies that rejection. |
| Geometry → precise source | The Web part snapshot gives the root and body the entire source range. Engineering feature nodes have `source: null`; mesh ranges carry the same body entity ID. | `Aetheris.Web.Runtime/Program.cs`, `WebModelSession.CompilePart`. |
| Precise diagnostics | The Web runtime maps `KernelDiagnostic.Source` to `details` and assigns every diagnostic a synthetic `SourceRef(sourceName, 0, 0)` (line 1, column 1). `KernelDiagnostic` has no range field. | `Aetheris.Web.Runtime/Program.cs`, `Diagnostics`; `Aetheris.Kernel.Core/Diagnostics/KernelDiagnostic.cs`. |

## Language intelligence

The existing V2 parser and specialized authoring parsers remain authoritative, but there is no public partial-source language-service API. `FirmamentV2Parser.Parse` is a whole-document parse and returns diagnostic codes and a semantic document after expansion. Its canonical construct admission list is internal to the parser. Many constructs are governed by separate specialized parsers and frontend schemas. A separate TypeScript completion table would drift immediately; a new C# table that merely restates these parsers would also be a second language definition.

Current Web SDK methods are `compile`, session rebuild/property/source operations, tree and mesh inspection, selection resolution, and STEP export. It exposes no completion, hover, signature help, symbol/definition/reference query, or partial-source diagnostic request. No LX result shape or latency is claimed. There is no measured completion/hover/selector latency because the APIs do not exist.

## Selector semantics

The parser accepts semantic axis forms such as `face(+Z)` in supported contexts, and imported STEP uses `Body.face("#entity")` only where an imported topology map resolves that entity. These are source expressions. A browser `face:5` identity is neither form. Mapping a selected face to `face(+Z)` would require compiler-owned evidence that the selected topology is the unique source-addressable +Z face for the relevant authored body and revision. The Web export currently retains no such mapping. Mapping an edge needs an admitted source selector form first; the parser currently rejects it.

Therefore source-addressability cannot be asserted from the current Web selection result. A conservative API could report `Ambiguous` or `RuntimeOnly` with no selector, but it would not satisfy the mandatory face/edge acceptance witnesses or the fresh-agent Copy Selector task. Returning fabricated text would make the user paste invalid Firmament.

## Helios implication

Helios should keep **Copy Semantic ID** distinct from **Copy Selector**. The prior DFH-X1 UI-only walkthrough confirmed why: a selected top face displayed `face:5`; using that value as a Hole `On` expression failed. Go to Source selected model-level line 1 rather than the Box declaration at line 3. Helios's hardcoded Box/Hole snippets are a narrow interim authoring aid, not an LX contract. Replacing them requires authoritative snippet/field metadata and a partial-source analysis path in Aetheris.

## Required upstream decisions and work

1. Decide whether LX-X0 targets the currently admitted **AxisCoil** helical workflow or authorizes a separate **Helix** language and geometry milestone. The present mission asks for Helix while prohibiting the syntax/geometry work needed to make it legal.
2. Retain authored declaration spans and generated semantic-to-topology provenance through Firmament AIR, B-rep, STEP reimport, and the Web mesh. This must support one source construct to many faces/edges and distinguish addressable from generated/runtime-only topology.
3. Define and admit source-valid selector forms for the intended face and edge classes. The formatter must consume the compiler's stable identities, not reimported face numbers or mesh indexes.
4. Add parser-adjacent authoring metadata or reusable parser queries for fields, value kinds, snippets, and partial-source contexts; expose revision-tagged JSON-safe results through the Web SDK. Preserve the actual parser/binder as the only language authority.
5. Carry precise source spans through diagnostics instead of assigning line 1 to every Web diagnostic. Then qualify incomplete-source completion and diagnostics without B-rep rebuild.

This is upstream semantic/compiler work, not a safe Helios-only patch. The existing single-source Web contract and missing project context remain additional limitations. No multi-file work, language redesign, or geometry test weakening was introduced.

## Qualification performed

- Aetheris CLI help inspected before assumptions, per repository guidance.
- Canonical AxisCoil fixture validated through the real CLI: valid, 0 diagnostics.
- A Mechanical `Helix` probe under ignored `artifacts/local/lx-x0/` validated through the real CLI: fatal unknown declaration.
- Existing parser test source confirms `edge(...)` rejection. No implementation files changed, so full solution/browser/Helios regression was not run for this blocked audit.

**Decision boundary:** Resume implementation after the Helix versus AxisCoil target and stable face/edge selector semantics are explicitly admitted by the owning compiler. An editor contract can then project those authorities without duplicating Firmament.
