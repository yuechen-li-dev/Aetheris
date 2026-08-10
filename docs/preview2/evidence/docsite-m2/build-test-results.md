# P2-DOCSITE-M2 build, test, and rendered QA results

Validated 2026-08-09 on Windows with .NET SDK 10.0.203, TSPack 0.1.8, Vite 6.4.3, and MachinaLayout.JS 0.7.0.

## Aetheris

- `dotnet restore Aetheris.slnx`: passed.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed, 0 warnings, 0 errors.
- `Aetheris.Kernel.Firmament.Tests`: 1,077 passed.
- `Aetheris.Semantics.Tests`: 9 passed.
- `Aetheris.Forge.Sdk.Tests`: 8 passed.
- `Aetheris.FEA.Tests`: 12 passed.
- `Aetheris.CLI.Tests`: 355 passed.
- Total targeted tests: 1,461 passed, 0 failed, 0 skipped.

The exact CLI built all five public geometry sources: bare box, Table/Template/Concept Path, HexBolt, profile bracket, and InlineStep recognize/replace. SurfaceMeshIR OBJ export passed and produced a connected, outward-oriented, watertight mesh. Both public assembly showcases passed `asm inspect`. The plate-with-hole native solve and Abaqus deck export passed. The negative stackup exited 1 with the expected typed diagnostic.

## Public site

- `tspack --version`: 0.1.8.
- `tspack sync`: passed.
- `tspack run docs-sync`: passed; 33 language features, 17 platform features, 11 source fixtures, and 2 real visuals.
- `tspack check`: passed. TSPack reported its standing security notice that 17 acknowledged dependency lifecycle scripts remain blocked; no lifecycle script ran.
- `tspack check --format`: passed.
- `tspack run typecheck`: passed.
- `tspack run test`: 3 passed.
- `tspack run lint`: passed.
- `tspack run build`: passed.
- `tspack run docs-check`: passed; 43 static routes, 10 aliases, 50 feature rows, and 11 canonical fixtures.
- direct `npm run build`: passed.
- Production assets: CSS 13.33 kB (3.86 kB gzip); JS 351.56 kB (106.29 kB gzip).

## Rendered QA

In-app Chromium inspection covered the landing page, definitive language reference, assembly guide, tolerance guide, plate-with-hole showcase, Forge host guide, and feature explorer at the default 1280 px viewport. The same shell/reference/explorer/Forge pages were checked at 390×844.

- Required headings, navigation groups, source links, tables, and code blocks rendered.
- Feature query/status controls updated the MachinaLayout-backed table.
- No browser console warnings or errors.
- No document-level horizontal overflow at desktop or mobile widths.
- Code and tables remain contained and horizontally scrollable.
- Mobile Contents control exposes the otherwise hidden sidebar.
- Skip navigation, focus-visible styling, semantic landmarks, labels, and alt text are present.

One defect was found and fixed during QA: C# code blocks did not inherit the Firmament block overflow rule and widened the Forge page. The general code-card rule now contains all languages.
