# P2-CADMATA-M1 — Cadmata application-shell refactor

## Outcome

Cadmata is now a warm, sharp application shell with an explicit document
machine, authored shell structure, application-facing selection records,
separate shell and viewport themes, and a bounded logarithmic grid. Vite remains
the browser bundler, but TSPack owns dependency resolution, synchronization,
policy checks, formatting, typecheck, tests, build, and the grid profile.

The Preview 1 launch boundary is unchanged: `Cadmata.exe <absolute.step>` and
`aetheris view` still feed startup intent through the host endpoint and the same
document import path used by browser file-open.

## Architecture before and after

Preview 1 mounted one `App.tsx` controller that jointly owned document creation,
STEP import/export, demo modeling, active occurrence, selection/picking,
diagnostics, shell geometry, and viewport presentation. The useful boundary was
already below it: server DTOs lower to `DisplayScene`, pure bounds helpers fit
the camera, and React Three Fiber consumes the renderer model.

M1 preserves those clean seams and introduces these ownership boundaries:

| Responsibility | Owner after M1 |
| --- | --- |
| Document intent and Empty/Loading/Ready/Failed transitions | `application/documentMachine.ts` |
| Stable command/workspace/viewport/inspector composition | `application/shellLayout.ts` |
| Future-capable semantic selection union | `application/selection.ts` |
| Shell palette, spacing, typography, and radius tokens | `theme/shellTheme.ts` |
| Renderer presentation configuration | `viewer/viewportTheme.ts` |
| API DTO to renderer input | Existing `buildDisplaySceneData` boundary |
| Scene, camera, materials, lights, picking, overlays, and grid drawing | `AetherisViewport` |

`App` remains the integration controller for existing modeling-demo actions and
API calls. M1 deliberately did not create symmetric wrapper layers around the
already-clean display mapper or bounds helpers. Extracting the experimental
modeling tool into a routed workspace is deferred.

## MachinaLayout.JS API audit and use

The repository's actual 0.7 authoring surface was inspected. The intended high
level is `M` from `machinalayout/machina`: `M.root`, stacks, grids, sizing,
responsive records, guides/layers/screens, text records, and Deus-oriented
`M.machine`, `M.state`, `M.on`, scopes/workflows and utility choices. Tables,
query, Dispatch, runtime Deus helpers, match, and framework bindings remain
purpose-specific subpath imports rather than members of `M`.

Cadmata uses the toolbox as follows:

- `M.machine`, `M.state`, and `M.on` author the document lifecycle; the Deus
  runtime creates/steps snapshots and the React binding supplies the hook.
- `M.root`, `M.vstack`/`M.hstack`, `M.fixed`, `M.fill`, and `M.rows` author the
  stable shell and inspector structure. Lowered region IDs are attached to the
  real semantic DOM as `data-machina-*` attributes and asserted in tests.
- `Table.define` and `Table.toObjects` author dense inspector property rows.
- `matchKind` exhaustively maps renderer discriminants.

Plain React remains responsible for accessible DOM controls; R3F remains
responsible for WebGL. Ordinary deterministic transformations remain plain
TypeScript. No utility-scored choice exists in this milestone, so JudgmentEngine
or `M.choose` would add ceremony rather than clarify a bounded strategy choice.

## Document and selection models

The document board retains source intent, server document ID, error, and
revision. Events are `Open`, `LoadSucceeded`, `LoadFailed`, `Close`, and
`Reload`; states are `document/empty`, `document/loading`, `document/ready`, and
`document/failed`. Browser file, startup file, and generated-document sources
are explicit variants. Process arguments remain at the host boundary.

Selection is no longer conceptually limited to a Three.js mesh. Its union has
`none`, `body`, `brep-face`, `brep-edge`, `semantic-feature`, `pmi`, and
`template-instance`. Current picking and compiler overlay adapters can produce
the supported cases while richer Preview 2 inspectors get stable future tags.

## Shell theme and visual changes

Shell tokens cover background, panel/elevated panel, primary/secondary text,
border, accent, warning/error/success, selection, disabled, spacing,
typography, and radius. Inter remains primary. The default radius is 1px.

The refactor replaces the Preview 1 white page with warm beige and desaturated
gray surfaces, retains dark viewport contrast, tightens the wordmark and status
hierarchy, converts the inspector to a real property table, makes action failure
visible in the header, and gives viewport plus inspector the full remaining
desktop height. The rail scrolls independently. Controls remain rectangular and
restrained; there are no blue primary accents, rounded cards, glass, or gradient
decoration.

## Viewport theme architecture

`ViewportTheme` independently owns scene background, object and selected
materials, edge style, grid style, light rig, shadows, environment/tone mapping,
fog, post-process intent, camera presentation, and axes. Theme consumption is
centralized in `AetherisViewport`; renderer components do not embed a second
palette.

The default Atelier theme uses a charcoal-brown field, warm stone material,
gold selection/edges, hemisphere plus key/fill directional lighting, ACES filmic
tone mapping, sRGB output, restrained fog, and soft shadow-ready configuration.
The cheap Monument proof switches to a pale atmospheric field, coral/pastel
material relations, softer grid, and stylized lighting through configuration
only. Both use the same R3F scene and renderables.

Post-process and edge fields are explicit future intent; M1 does not add a heavy
screen-space composer merely to prove the type. Larger future modes can add
outlines, grading, environment maps, and shadow variants at this boundary.

## Logarithmic grid profile and redesign

Preview 1 recomputed on camera motion over 0.01 world units or zoom over 0.1%.
Each logical line was a Drei `Line` rendered above and below the plane, with a
core and halo stroke. At spans 20, 200, and 2000 the structural profile is 48
logical lines but 192 React line components, geometries, and draw calls.

M1 retains adjacent powers-of-ten and their blend weight, but computes a
camera-bounded ground patch, caps lines per axis, separates major/minor lines,
and packs them into at most four `BufferGeometry` line-segment draws. It rebuilds
only after movement or zoom exceeds 4% of visible scale. Geometry is memoized
between revisions and disposed on replacement.

The deterministic profile at those same spans is 48 lines, four geometries,
four draw calls, and 1,152 bytes of position buffers. Live results at 1440x900:

| Scene | Avg frame ms | Total draws | Triangles | Geometries | Grid lines | Grid draws | Grid bytes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Empty | 7.047 | 10 | 24 | 10 | 60 | 4 | 1,440 |
| Simple plate | 6.961 | 26 | 144 | 26 | 78 | 4 | 1,872 |
| L-bracket STEP | 7.015 | 18 | 44 | 18 | 73 | 4 | 1,752 |
| CTC-01 X4 | 6.992 | 191 | 348,796 | 191 | 65 | 4 | 1,560 |

Frame values are browser presentation intervals, not GPU timestamp queries. The
strong conclusion is structural and renderer-visible: grid calls stay at four
while complex-scene work grows elsewhere. The remaining obvious viewport hot
spot is one render object/geometry per BRep face or overlay entity; batching and
display LOD belong in a later renderer milestone.

## TSPack integration and fixes

`manifest.tsx` and `ts-lock.toml` declare the browser application target,
runtime dependencies, tools, security acknowledgments, and run targets. The
supported gates are `sync`, `check`, `check --format`, `run typecheck`, `run
test`, `run build`, and `run profile-grid`. Vite 7 remains the appropriate
bundler. Vite development proxy activation no longer depends on npm's
`npm_lifecycle_event`. The legacy npm lockfile was removed; the Visual Studio
JavaScript project disables npm install/audit and both Visual Studio and the
ASP.NET SPA proxy launch `tspack run dev`.

Normal use exposed three TSPack defects, fixed directly with regression tests in
the sibling TSPack repository:

1. Biome 2 config generation used removed keys and unscoped formatting scanned
   the materialized `.tspack` store. Generation now uses Biome 2 `includes` and
   Assist syntax; default format/lint paths use TSPack's derived project paths.
2. The import scanner's side-effect regex crossed a newline from JSX text such
   as `Step Import</h2>` into the next quoted attribute. Horizontal whitespace
   and a single-line specifier are now required.
3. Manifest IR explicitly permits app targets without declaration output, but
   lock validation and pack did not. Empty app `types` now round-trips and pack
   skips a nonexistent type output.

TSPack reports acknowledged lifecycle scripts and transitive version conflicts
as warnings; lifecycle execution remains blocked.

## Real-path corrections and compatibility

Browser QA found the server publishing `conceptPlanes` and
`constructionPlanes` while the frontend X1 validator rejected both. The layer
contract and tests now match the server. CTC-01 X4 also repeated shared world
plane IDs for each Profile; server artifact construction now emits each stable
ID once, and API integration tests assert uniqueness.

Startup compatibility remains guarded by host startup tests, CLI view tests,
and package-relative asset tests. The renderer consumes only ready display and
theme inputs; neither startup paths nor process arguments enter it.

## Evidence, tests, and deferred work

Visual and performance evidence is under
`docs/development/history/preview2/evidence/cadmata-m1/`. Behavioral coverage includes document
transitions, selection tags, shell lowering, theme switching/application,
bounded grid/LOD behavior, package-relative assets, startup handoff, and unique
Cadmata fixture IDs. Public Cadmata X1 documentation now names the published
plane layers and the user-selectable viewport proof themes.

Deferred work:

- split the experimental modeling demo and remaining API orchestration out of
  `App` once routing/multi-workspace requirements are concrete;
- add cancellation/replacement semantics before multi-document behavior;
- batch face/overlay draws and add renderer LOD only in a measured viewport
  milestone;
- implement the semantic inspector over the new selection and property-table
  seams rather than adding more ad-hoc panels.

The strongest next milestone is **P2-CADMATA-M2 semantic inspector**. The shell,
selection tags, table authoring, document lifecycle, and compiler artifact
contract are now the natural seam; HexBolt can follow without using a Preview 1
inspector architecture as its first UI.
