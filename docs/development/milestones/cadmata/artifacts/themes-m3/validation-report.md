# P2-CADMATA-THEMES-M3 validation report

## Automated validation

- `tspack --version`: `v0.1.8`.
- `tspack sync`: passed.
- `tspack check`: passed with the repository's acknowledged lifecycle-script inventory and existing multi-version lockfile diagnostics; no dependency was added by this milestone.
- `tspack check --format`: passed after `tspack format` fixed three milestone files.
- `tspack run typecheck`: passed.
- `tspack run test`: 14 files, 75 tests passed.
- `tspack run build`: passed; production Three.js chunk 724.48 kB, inside the existing 750 kB project budget.
- `tspack run lint`: passed.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed, 0 warnings and 0 errors.
- `dotnet test Aetheris.Server.Tests/Aetheris.Server.Tests.csproj -f net10.0 --no-restore /m:1`: 53 passed, 0 failed.
- `git diff --check`: passed.

## Rendered assembly QA

Fixture: `fixtures/Canonical/Assembly/template-block-pair.firmament` loaded through the real Cadmata startup/assembly display path.

- Same two-definition/two-occurrence assembly and fitted orthographic camera captured under all six themes.
- Selecting `Fixed` in the product tree produced `active-row`; that exact selection remained active through all six immediate theme switches.
- Direct canvas selection after orbit/pan/zoom selected `Moving` in the product tree, confirming shader backgrounds do not intercept pointer events.
- Orbit, Shift-drag pan, wheel zoom, direct mesh selection, product-tree selection, and theme switching completed without browser errors.
- Mate `Seat` remained `valid`; tolerance stack `TemplateFitTransition` remained `PASS` at 2 mm [2, 2].
- Draw calls, triangle counts, and geometry counts changed only by the decorative full-screen plane and theme grid policy. Engineering occurrence transforms and semantic identity did not change.

## Selection, semantic overlay, and PMI QA

Fixture: `pmi-projected-hole-diameter`, loaded through the real Cadmata fixture endpoint and R3F/HTML annotation path.

- Both `DATUM A` and `⌀8 mm +0.05 mm/-0.02 mm A` callouts were present under Atelier, Monument, Mars, Sirius, Singularity, and Aeons.
- Dark worlds used opaque/near-opaque dark callout backgrounds with white selected text; Monument used pale paper with black selected text.
- Theme overlay palettes supply separate concept, profile, compose, selection, ancestor, and diagnostic accents.
- Selected face material and selected edge width/color remain stronger than normal presentation in every descriptor.

## Renderer and fallback QA

- Output color space remained sRGB; ACES filmic tone mapping and exposure are explicit per theme.
- No WebGL or shader compilation errors appeared in browser diagnostics. The only warning was the existing upstream Three.js `Clock` deprecation.
- Procedural backgrounds use no textures and write no depth. The opaque scene clear remains behind the shader as a graceful failure fallback.
- All procedural themes are static by default, satisfying reduced-motion without a separate animation branch.

## Remaining visual limitations

- Background glow and vignette are shader-local composition effects, not depth-aware post-processing; CAD highlights do not bloom into screen space.
- The current orthographic viewer has no shadow-catching plinth, so Monument remains a soft architectural presentation rather than a physically grounded studio render.
- Evidence uses the native TemplateBlockPair assembly and projected-PMI fixture. Imported OCCT assembly semantics are covered by existing server paths/tests but were not recaptured for every theme in this milestone.
