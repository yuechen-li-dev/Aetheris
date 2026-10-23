# Concept Struct visualization qualification — 2026-10-19

## Result

**Success for retained geometric Concepts.** Helios and Cadmata have an opt-in
**Concepts** button. It draws gold construction wires through the material, with
a faint static halo in WebGPU and crisp wires in the WebGL fallback. The toggle
does not compile, change topology or intercept engineering selection.

The source witness is
`fixtures/Regression/Visualization/concept-structs.firmament`: an 80 × 50 × 25 mm
chamfered box with a Concept Box3, top plane, center axis and two mount points.
Its 20 guides are separate from material meshes and STEP output.

## Ownership and compatibility

- The compiler projects resolved Box3, Plane, Axis, Region2 and 3D point/set values.
  Assembly occurrences also publish guides from exact semantic frame/plane/axis/
  point bindings. No browser source parser or mesh-edge inference is involved.
- A focused test exposed unused Concept Structs disappearing from Template
  display metadata outside the older Box/Modify route. The existing AST and
  spatial value resolver now retain those values for presentation before erasure.
  This does not change source admission, materialization or public port binding.
- Display definitions share immutable local guides; occurrences provide placement
  and sampled motion transforms. Existing compilation-session input keys retain
  the same cache ownership. The SDK carries additive `concepts` metadata.
- Shared Three Telos code owns gold styling and WebGL projection. Guides are
  explicitly non-pickable. WebGL resources are disposed when hidden/replaced.
- Scalar and predicate requirements have no invented geometry. Unsupported spatial
  value kinds and unframed local 2D points are not synthesized. Infinite planes/
  axes use finite 40 mm guides, frame axes 20 mm and point marks 2 mm.
- STEP-only input cannot recover erased Concept Structs. Open Firmament source;
  the button is disabled when supported retained guides are unavailable.

## Evidence

Generated evidence is under `artifacts/local/concept-wireframes/`.

| Check | Result |
| --- | --- |
| Complete Release solution build | Passed; zero errors |
| Complete .NET regression | 5,663 passed, zero failures/skips; FrictionLab discovers no tests |
| Core fast lane | 1,087 passed |
| Focused compiler projection | 2 passed: exact bounds/value types, private Template retention, shared definition and translated occurrences |
| Three Telos tests | 15 passed, including gold transform/picking/disposal coverage |
| Cadmata frontend | TypeScript and bundle passed; 19 files / 92 tests passed |
| Helios frontend | TypeScript and development bundle passed; 20 files / 96 tests passed; final shell test rerun passed |
| CLI source admission/build | `validate.json` and `build-part.json` succeed |
| Existing FrontendX4 geometry corpus | 32/32 passed without recording baselines |
| Real application browser checks | Both products pass WebGPU and WebGL toggle checks; each reports exactly 20 guides |
| Failed build retention | Helios keeps all 20 guides after the same source receives unknown `distnace` |
| Independent image checks | Gold pixels increase by thousands when enabled in both renderer paths and return to baseline when hidden |

The failed-draft check reopens an invalid revision of the same source through the
normal project build path, independent of Monaco keyboard timing. Helios's WebGL
check starts a fresh application because its
adapter probe is intentionally cached per startup; capability mutation after
startup is not a renderer-selection contract.

`cadmata-application.png` and `helios-application.png` were visually inspected.
The four renderer images and `pixel-qualification.json` verify visible output,
not just debug counters. `cadmata-browser.json` / `helios-browser.json` record no
page errors. Browser scripts close their isolated browser profiles.

The native desktop host builds with the updated Cadmata frontend. Qualification
exercised its application view through the real server; a separate WPF/WebView2
launch and a new production AOT release were not part of this pass.

The code-quality pass found no new diagnostic allowlists, regex dispatch, source
rewrites, rendering engine or geometry planner. Public usage and guide limits are
documented in `docs/public/three-telos.md` and the Helios README.

Temporary application servers were stopped after qualification. No dotnet,
testhost, aetheris, MSBuild or VBCSCompiler hosts remain; the test browser profiles
are closed. `git diff --check` passes in both repositories.
