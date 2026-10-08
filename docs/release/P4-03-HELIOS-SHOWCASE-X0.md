# P4-03 — HELIOS-SHOWCASE-X0

Date: 2026-10-07. Product implementation: sibling `HeliosCAD` checkout. Runtime and distribution repairs: `Aetheris.Web.Runtime`.

## Verdict

**Accepted** for the qualified local production experience. All five canonical projects compile and render through the real browser runtime; browser coverage comprises 16 passing checks and the full serial .NET gate passes. The latest hero revision passes its three affected production checks. Public deployment and the separate cloud/backend qualification remain outside this acceptance.

## Product direction and audit

Helios opens as an anonymous, human-facing 3D design studio and leads into its source-and-geometry workspace. Founder review replaced the initial descriptive SaaS-style welcome page with a raw object catalog: a naturally proportioned Inter HeliosCAD title with a light CAD suffix, a single-line vertical statement roll-up, prominent New part / New assembly actions, thick outlines, flat square cards, large black-and-white SVG model views and short names. Cards use warm paper with a subtle grain overlay in both themes. Introductory slogans, the workflow strip, card descriptions and reassurance copy were removed. New browsers default to white Sirius; dark Mars restores the original dark green and gold palette across the welcome screen, shell, Monaco and viewport. The shared theme preference persists across reloads and editor entry.

The audit found useful systems already working: Monaco with compiler-owned language intelligence; a dedicated geometry Worker; retained last-valid snapshots; shared Three Telos WebGPU, topology and picking; existing cloud ownership; and development-only PowerShell terminal support. They were kept. The previous first launch depended on community authentication, the editor split and docks were fixed, the model hierarchy was not mounted, placeholder Git/LLM tabs suggested unavailable functionality, and the green/orange viewport competed with the model. Existing README language-service claims were also behind the implementation.

The first launch presents five genuine examples, search and categories. ATLAS, guitar and house lead the three-column catalog, followed by the mounting plate and threaded bolt. **New part** opens the existing simple box starter; **New assembly** opens the existing two-component assembly starter. These are editable browser-local workspaces with Build, source download and STEP export. The gallery initializes neither Monaco nor the geometry runtime until a model opens. No account or backend is required. Community and saved-account flows retain their existing owner at `/discover` and `/projects`.

The workspace keeps source on the left, geometry beside it, a collapsible utility dock and a compact bottom pane. Source/viewport and bottom separators support pointer and arrow-key resizing. Files exposes intact documents and the compiled hierarchy; Inspector presents semantic identity, occurrence, definition, parent, transform and available source references. Examples is available in the dock and a focus-managed modal. Problems and Output show real runtime information. Terminal is offered only on the local development host and starts when opened. Production has no terminal tab.

Typography uses self-hosted Inter variable fonts throughout the workspace, including Monaco, terminal, numeric and identity values and branding text. Workspace text is 10–14 px, source is 14 px, and larger headings are confined to the welcome screen. Weights establish hierarchy. Spacing tokens use 4/8/12/16/24 px. Surfaces, control states, visible keyboard focus and 100 ms feedback are shared; reduced-motion preferences are respected. New reusable components own gallery cards, example dialog, pane separators, workspace status and AI Author presentation. There is no general UI framework replacement.

## Real examples and source ownership

| Project | Source documents | Shared definitions | Occurrences | Browser result |
| --- | ---: | ---: | ---: | --- |
| Mounting plate | 1 | 1 | 1 | Part, source-linked hole, STEP |
| CODEX threaded bolt | 1 | 1 | 1 | Threaded stock, hex head and maker-mark source, STEP |
| Industrial ATLAS | 10 | 24 | 113 | Reusable components, shared geometry, STEP |
| Sunburst electric guitar | 24 | 56 | 171 | Included components, materials, strings and hardware, STEP |
| Warm-modern house | 8 | 94 | 254 | Scene hierarchy, authored materials and three cameras |

All five compile in the production AOT Worker with zero diagnostics. The gallery ships 44 canonical source documents and five monochrome SVG wireframes, approximately 1.86 MB of uncompressed SVG total. `HeliosCAD/scripts/sync-showcase.mts` copies upstream bytes and records origins and SHA-256 hashes in `fixtures/showcase/provenance.json`. Source imports are lazy. Project documents remain separate; Aetheris resolves includes and resources. The frontend has no Firmament parser or geometry solver.

The SDK accepted `projectDocuments` but Assembly compilation previously ignored them. The repair routes that request to the existing retained `CompileProject(FirmamentProjectSnapshot)` owner, including the effective root source. Project-aware language analysis now uses the existing compiler overload instead of producing false missing-resource diagnostics for ATLAS's housing files. A real two-file browser witness builds shared instances, edits a supporting module, switches files without losing changes, rebuilds the root, and diagnoses a missing include while retaining valid geometry.

Scene opening defaults to its first authored camera. This was a direct visual-dogfood improvement over framing a closed house as an exterior box. The house's Hero view immediately shows its living, kitchen and dining composition. Manual and automated orbit and coffee-table selection were exercised.

Current thumbnail provenance and reproduction instructions live beside `HeliosCAD/public/previews/showcase/wireframes/`. The native asset generator reuses Aetheris compilation, occurrence transforms, topology-edge projections and the existing BRep SVG view/framing renderer. Parts use exact STEP edges and trim-aware isolines; Assemblies and Scenes use placed sampled topology edges. No triangle diagonals or handmade model outlines are substituted. The house uses an explicit presentation cutaway through the existing Scene projection. Room/window rectangular definitions previously supplied faces but no edges; the owning Scene constructor now supplies its twelve real boundary edges. The wireframes intentionally show through geometry and are not hidden-line drawings. The browser runtime does not participate in asset generation. Regenerating all five SVGs produced byte-identical SHA-256 hashes; evidence is in `HeliosCAD/artifacts/local/p4-03/wireframe-reproduction.log`.

The latest ATLAS card uses `AssemblyKinematics.Evaluate` with Shoulder -65°, Elbow 100° and both grippers at 4 mm, recorded in thumbnail provenance. The canonical source opens at its authored zero pose. The earlier hero-only revision regenerated all five warm-paper SVGs byte-identically, recorded in `HeliosCAD/artifacts/local/p4-03/hero-reproduction.log`.

The subsequent [ATLAS modernization](../public/demos/industrial-atlas-modernization.md) replaces individual decorative seating relationships with reusable components and keyed mounting sites, repairs cover standoffs, jaw roots and inward pad seating, and retains 60 bodies and 24 geometry definitions. Its current card and copied source modules were regenerated together. Published mounting-frame origins now accept dimension-checked length arithmetic, and assembly GLB export preserves authored occurrence materials. Fresh qualification passed: 4,391 full serial native tests, 1,058 fast core tests, 37 Helios unit tests, and eight production browser workflows covering all five examples, gallery/theme behavior, hero motion and the assembly starter. Evidence is under `Aetheris/artifacts/local/atlas-modernization/`.

Earlier real raster presentation images remain available for documentation. AI-generated front-page concept images informed composition only and are not application assets. Factory/warehouse and sheet metal were not added to this bounded gallery; the active SDK does not advertise sheet-metal support.

## Build, inspection and export

Build and Ctrl+Enter compile the project root, including supporting-file edits. Runtime initialization, building, failure and ready states are distinct. Actual elapsed time replaces fabricated progress. Heavy requests remain serialized through the existing Worker client, and late results cannot overwrite newer source. Source and displayed revisions identify stale geometry. Invalid source retains the last valid model and opens precise Problems with source navigation where available.

Monaco keeps undo, formatting, completion, hover, semantic tokens, markers and source navigation. Semantic-token results are discarded if the document changed while the request ran. Dirty indicators belong to each file; reverting to its saved source clears its indicator. New-document commands clear the previous project snapshot. Local Save source downloads the active file; supporting files must also be downloaded before leaving. This is a browser-session workspace, not multi-file cloud persistence. Some compiler assembly-instance nodes have no source span; Inspector reports that limitation and disables Go to Source for them.

Founder feedback added consistent Inter and an editor wrapping option. `@fontsource-variable/inter` is pinned to 5.3.0, includes the OFL-1.1 license, and ships local normal/italic webfonts. Monaco remeasures after Inter loads and uses its advanced wrapping strategy for proportional glyph widths. The vertical scrollbar gutter now has an opaque editor surface, preventing long lines from showing through the scrollbar. **Wrap** and **Alt+Z** toggle a persisted preference. A real browser regression checks loaded fonts in the editor, line numbers, shell, output and development terminal, plus text bounds beside the scrollbar at 1920 and 1280 widths, keyboard toggling and preference persistence. See [Inter distribution and license](https://fontsource.org/fonts/inter/use).

Three Telos remains the display authority. Camera presets, projection, shaded/edge/wire views, fit, fit selection, BRep surface inspection and authored cameras remain available. The viewport's effect depends on model/display changes rather than the entire changing props object, so elapsed build ticks do not repeatedly reconstruct the scene. Workspace disposal terminates the owned Worker; in-flight initialization cannot leave an orphan Worker. Device failure retains the model and offers the existing viewport restart path.

The production first-user witness also checks the actual GPU display path: the holed plate renders one mesh surface with a named `mesh-fallback` reason; the corrected qualified Box renders one CIR field, zero visual mesh surfaces and a bound shader artifact. Its retained mesh proxy remains for picking. `display-paths.json` records both packets without putting implementation controls in the normal UI.

Only active supported exports appear: STEP AP242 for Parts and Assemblies, with explicit download feedback and filenames. Scene STEP is omitted. Browser GLB and USD are omitted because the SDK exposes no such methods; the native CLI remains their owner. The downloaded bracket recovery witness was re-inspected through the repository CLI: one valid closed body, six faces, twelve edges, eight vertices, 50 × 40 × 8 mm. It is the corrected solid used by walkthrough E, not the initial holed plate.

AI Author provides an intentional empty state, provider/composer placement and the propose → review → apply/build contract. Controls say no provider is connected and are disabled. P4-04 can attach provider-neutral proposals, diffs and approved tool actions to the existing source/build workflow. No provider calls, streaming responses or generated source were fabricated.

## Browser walkthroughs and evidence

These checks use the actual SDK and installed Edge WebGPU. Mock compiler responses are used only in focused component tests. The real application was repeatedly opened and operated with browser tools during design, including camera presets, surface selection, gallery navigation, ATLAS Base selection, house Hero/orbit/coffee-table inspection and layout review. Browser captures drove further revisions to file labels, diagnostics, Scene framing and thumbnails.

| Walkthrough | Qualified behavior | Screenshot under `HeliosCAD/artifacts/local/p4-03/` |
| --- | --- | --- |
| A | Anonymous first launch, plate source, real Build and geometry | `first-launch.png`, `mechanical.png` |
| B | ATLAS and guitar sources, real assembly geometry, hierarchy and component inspection | `robot.png`, `robot-inspector.png`, `guitar.png`, `guitar-inspector.png` |
| C | House project files, default Hero camera, interior materials, orbit and coffee-table inspection | `house.png`, `house-inspector.png` |
| D | Invalid source, actionable diagnostics, retained geometry, corrected Ctrl+Enter Build | `diagnostics.png` |
| E | Actual STEP download, native reinspection | `export.png`, `bracket.step` |
| Additional | Gallery, selected face, honest AI state, smaller desktop and Inter with word wrapping | `gallery.png`, `selection-inspector.png`, `ai-author.png`, `narrow-desktop.png`, `editor-word-wrap.png` |
| Founder visual revision | SVG catalog, original green/gold dark mode, theme persistence into the real editor, narrow and mobile browsing | `wireframe-gallery/light.png`, `wireframe-gallery/dark.png`, `wireframe-gallery/narrow.png`, `wireframe-gallery/mobile.png`, `wireframe-gallery/studio-dark.png`, `wireframe-gallery/studio-light.png` |

Product captures use 1920 × 1080, with the narrow layout at 1280 × 800. The local shell and Monaco language walkthroughs explicitly set 2560 × 1440 and produce `HELIOS-MVP-UX-X0-2560x1440.png` and the language capture. No browser devtools or runtime overlays are included in product screenshots. Generated evidence stays ignored. Shipped thumbnails and README media are traceable product assets.

## Measured observations and AOT distribution

Measurements are local loopback, fresh test contexts on this workstation; they are not internet cold-download or cross-device benchmarks.

| Operation | Observed |
| --- | --- |
| First launch → ready plate and explicit rebuild | approximately 2.8 seconds |
| ATLAS Worker build / ready observation | 20.1 s / 24.5 s |
| Guitar Worker build / ready observation | 55.1 s / 66.1 s |
| House Worker build / ready observation | 5.6 s / 8.9 s; separate live build 4.8 s |
| Bolt Worker build / ready observation | 16.6 s / 18.9 s |
| Monaco completion after startup | 24 ms then 3 ms |
| Hover / formatting | 5 ms / 40 and 35 ms |
| Language analysis | initial 2.0 s; subsequent 18 and 15 ms |

Full canonical browser tests take longer than the ready observation because hierarchy, GPU presentation, selection and captures are also qualified. The initial development runtime and stale September AOT package were not accepted as current production evidence. A fresh AOT publish exposed `ConstructorContainsNullParameterNames, Aetheris.Semantics.SemanticProvenance` on Assembly/Scene snapshot serialization. Rooting the owning Semantics assembly preserves required constructor metadata, consistent with the existing reflection bridge. After rebuilding and installing through the SDK distribution workflow, both projects compile in the real production browser.

The build script also clears its owned generated AOT publish directory before publishing; obsolete fingerprints had inflated the tarball. Current SDK package: 120,069,885 bytes, approximately 226.9 MB unpacked, 758 files. Current native AOT WASM: 68,359,662 bytes; Brotli 12,563,985 and gzip 20,088,901 bytes. The SDK's existing package version remains `2.0.0-preview.3`; this milestone does not silently change release/version policy. The welcome route avoids initializing either runtime until an example opens. The editor bundle and AOT download remain substantial; production compression and delivery remain distribution considerations.

## Validation and reproduction

- Helios Vitest: 37 passed across 10 files.
- Canonical/multi-file/Inter, wrapping and SVG catalog production browser coverage: ten passing checks, zero page errors. The HeliosCAD revision passes three affected checks: responsive catalog/theme/Part entry, Assembly editing/build/STEP export, and statement roll-up/pause/reduced motion. Inter/wrapping and the six unchanged canonical/project checks retain their preceding qualification.
- Local language, 1440p terminal/edit/build/export, surface inspection and Worker lifecycle browser regressions: 6 passed. The Scene camera and first-user/gallery paths were rerun after visual revisions.
- Three Telos unit tests: 14 passed.
- Public SDK selection/language unit tests: 6 passed.
- Production TypeScript/Vite build passes, with the existing large-editor-chunk warning.
- Final .NET solution build: passed with 10 warnings and zero errors (existing obsolete test API and WebAssembly SQLite warnings). Fast Core lane: 1,058 passed. Full serial solution gate: 4,383 passed across 21 suites, zero failures and skips. Three new tests cover SVG projection and the twelve unique Scene box boundary edges.

Reproduce from HeliosCAD:

```powershell
node scripts/sync-showcase.mts
npm run sdk:install:production
npm test
npm run build
npm run preview -- --host 127.0.0.1 --port 4174
# In another terminal:
$env:HELIOS_SHOWCASE_URL = 'http://127.0.0.1:4174'
npx playwright test --config playwright.showcase.config.ts tests/showcase-x0.spec.ts tests/project-loading.spec.ts tests/editor-presentation.spec.ts tests/wireframe-gallery.spec.ts
# Development-only terminal/language/witness regressions use port 4173:
Remove-Item Env:HELIOS_SHOWCASE_URL
npx playwright test --config playwright.showcase.config.ts tests/language-x2.spec.ts tests/mvp-ux-x0.spec.ts tests/surface-inspection.spec.ts tests/worker-sdk.spec.ts
```

Reproduce the native gate from Aetheris with `dotnet build Aetheris.slnx -c Release -m:1`, the documented fast lane and `dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1`. Logs and unique TRX files live in `artifacts/local/p4-03/`. A competing AOT build caused an earlier CLI export test to exceed its five-second tessellation budget at 5,269 ms. The quiet final gate passed that case without changing its budget or tests.

## Preview 4 boundaries and founder review

The implementation is qualified locally through the built production frontend. It has not been publicly deployed. Account/cloud integration, PostgreSQL/S3, production HTTPS and the separate Leviathan test suite were not rerun in this bounded task. Existing cloud API/auth/ownership semantics were kept; no other backend repository was changed. Multi-file cloud saving, recent-workspace persistence, source-span improvements for every assembly instance, pose editing and the provider gateway remain separate work.

The current dependency audit reports pre-existing DOMPurify/Monaco moderate advisories and a source-map-js build-tooling high advisory. The pinned font adds no dependencies. These findings are recorded in ignored `HeliosCAD/artifacts/local/p4-03/dependency-audit.json`; this product change does not silently upgrade Monaco or the toolchain. Dependency remediation and public deployment qualification remain release follow-up work.

Founder feedback can now focus on the monochrome object catalog, bold framing and scale, green/gold dark mode, and the transition into the working editor. The chosen tradeoff prioritizes the human-facing 3D objects and immediate editing experience, while retaining intact source examples and the existing compiler/runtime owners. CLI and Cadmata remain the ordinary LLM-facing paths. Static Scenes communicate spatial authoring and interchange; they do not claim manufacturing simulation or autonomous physical AI.
