# FIRMAMENT-UX-SURFACE-X0 — Helix admission and topology selector audit

## Verdict: Meaningful progression

`Helix` is admitted as the canonical WireForm operation. `AxisCoil` remains a source compatibility spelling. Both invoke `WireCoilAuthoring.CreateAxis`, produce the same `WireAxisCoilAir`, stable operation ID, and evaluable geometry. Public build/inspect metadata now calls this operation `Helix`.

Source-valid selectors for arbitrary picked topology remain incomplete. The Web runtime compiles a part to STEP, reimports that STEP, tessellates the imported body, and emits `face:<FaceId.Value>` on display ranges. The compiler does not carry an authored face/edge correspondence through that boundary. Formatting the displayed number as `face(#id)`, `face(+Z)`, or an imported STEP selector would assert identity that is not established. The runtime therefore labels these ranges `RuntimeOnly` with a null selector and an explicit reason; `ModelSession.describeSelection` exposes the classification.

## Admitted Helix surface

- `Helix Name { Radius; Turns; Pitch or Height; Handedness; StartPhase }` inside `WireForm` uses normal Firmament length/angle parsing. Radius, Turns, and pitch/height must be positive. If Pitch and Height are both present, Height must equal Turns × Pitch. Handedness is `RightHanded` or `LeftHanded`. The winding axis is derived from the incoming wire state.
- The canonical fixture is `fixtures/Canonical/WireForm/helix.firmament`; `axis-coil.firmament` remains the compatibility witness. Public WireForm documentation teaches Helix first.
- Helios has a `New Helix Model` command palette entry that opens a complete one-turn WireForm source. This is a bounded witness, not a language completion service. The WebAssembly material resolver reads the same checked-in immutable catalog seed through the existing validation/mapping code because the EF/SQLite compiled model cannot initialize in the browser. Helios retains its page-lifetime WebAssembly transport across project sessions; the local SDK installer refreshes a changed preview tarball instead of silently keeping an old package.

## Selector identity findings

- `face(+Z)` is a typed `FirmamentV2FaceSelector` for bounded authored box-face uses. Imported STEP has a separate qualified `Body.face("#entity")` target and an imported topology map. Those identities do not currently map to Web display face IDs.
- The V2 exposure parser explicitly rejects `edge(...)` and has no general body selector. Face, edge, and body formatter/parser/binder roundtrips therefore cannot be claimed.
- Web source references are currently attached to root/body source ranges; generated display faces have no originating feature range. Assembly display ranges also lack a source-selector correspondence.
- The next blocker is an authoritative export/reimport correspondence from compiler-owned semantic face and edge identity to displayed topology, followed by a bounded parser/binder contract for each admitted selector kind. Numbering by tessellation or reimport order would be unstable.

## Verification

- `dotnet test Aetheris.Kernel.Firmament.Tests --filter FullyQualifiedName~WireFormTests --no-restore`: 30 passed, including Helix/AxisCoil AIR and stable-ID parity, handedness, derived pitch, invalid dimensions, and STEP build.
- `dotnet run --project Aetheris.CLI -- build fixtures/Canonical/WireForm/helix.firmament --json`: success; enclosed manifold, STEP reimport manifold, 1 Helix operation, zero faceted fallback; generated fixture STEP was removed after inspection.
- `dotnet test Aetheris.Kernel.Firmament.Tests --filter 'FullyQualifiedName~FirmamentV2InlineStepTests|FullyQualifiedName~FirmamentV2CanonicalSafetyAndLabelingTests' --no-restore`: 62 passed; existing imported STEP selectors remain intact.
- `dotnet build Aetheris.Web.Runtime --no-restore` and Web SDK `npm run build`: succeeded with existing WebAssembly/analyzer warnings.
- Material catalog tests: 5 passed. Helios `npm run build` and focused `HeliosApp.test.tsx`: succeeded, 9 tests passed.
- Helios Playwright `tests/helix-palette.spec.ts`: passed against the installed SDK/WASM and real project UI; the palette opened Helix source, produced 1 display definition, and finished with zero diagnostics (48.7 seconds). The one-turn snippet bounds browser compile cost; the eight-turn fixture is qualified by CLI, not by this browser run.
- SDK `node --test tests/selection.test.mjs` passed: `face:5` returns `RuntimeOnly` and no selector; an out-of-range triangle returns null.
- A fresh agent independently used CLI validate/inspect/build on the Helix fixture: 0 validation diagnostics, 1,026 manifold faces, successful STEP reimport, and no faceted fallback. A source probe using a previously displayed `face:5` failed in parsing; changing the face target to `+Z` built successfully. The separate browser witness above verifies the Helix palette after the fresh agent's initial URL probe was unavailable.

The current result is deliberately short of selector acceptance and Copy Selector. No editor-specific selector spelling was introduced.
