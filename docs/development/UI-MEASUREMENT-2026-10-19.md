# Cadmata / Helios measurement queries and fillet assertion cleanup

## Scope and ownership

The viewport now exposes a read-only measurement tool in both Cadmata and Helios.
`Aetheris.Geometry.BrepMeasurementQuery` owns BRep reference resolution and numerical
interpretation. It reuses `BrepPicker`, `AnalyticDisplayQuery`, native analytic
surfaces, and rigid occurrence transforms. The public browser SDK and Cadmata HTTP
endpoint call that owner; neither frontend implements CAD distance calculations.
The shared Telos tool owns input state, labels and non-pickable witness guides.

The admitted modes are picked surface-point distance, parallel support-plane
spacing, and infinite analytic-axis spacing. The latter modes disclose their
scope instead of labeling supporting geometry as finite-body clearance. Angles
are acute and explicitly labeled. Cylinder diameters are projected directly from
the native surface. World ΔX/ΔY/ΔZ and millimetre/inch presentation share the same
result, and Copy result preserves the underlying millimetre data.

This is deliberately not a general trimmed-face minimum-distance solver. The
existing bounded parametric closest-point engine does not, by itself, admit that
claim for arbitrary BRep trims. Edge/vertex snapping and general material clearance
remain separate follow-ups; no speculative solver was added here.

## Integrity and interaction

- A/B input references are independently replaceable. Rapid clicks stage the pair
  while the backend resolves it; generation checks discard superseded replies.
- Rebuilds, source drift, loading transitions, document placement edits and motion
  playback clear or disable results. Server and WASM revision checks independently
  reject stale requests. Failed builds retain the model through the existing app
  path but do not retain an apparently current measurement.
- Pick errors clear the previous number. A failed replacement cannot leave a
  previous value looking like a newly accepted result.
- Dragging or orbiting does not replace a reference. Witnesses are excluded from
  engineering picking in Telos and WebGL.
- Native analytic picks must pass the existing trim support policy. Approximate
  display points require explicit opt-in, apply only to point mode, and label the
  whole result approximate. They are not tolerance-verification evidence.
- The frontmost unsupported surface cannot be skipped to find a supported back
  face. Ray-only requests on an incomplete pick proxy require an explicit face.
- Double-sided candidate picking matches the viewport, including visible inward
  hole walls. The final analytic point still comes from the BRep query owner.
- Document measurement caches are invalidated by the document revision. Browser
  geometry is retained from the successful compilation, not rebuilt from text
  during a query. Assembly definitions are paired with solved occurrence matrices.

## Runtime correction

Real-browser testing exposed the native bounded tessellation wrapper's blocking
Task/monitor wait, which browser WASM cannot execute. Measurement pick preparation
now follows the existing WebMeshBuilder policy: synchronous tessellation inside
the browser Worker, bounded partial preparation on the native host. A browser
proxy preparation failure is a named `measurement-display-unavailable` error.
The browser path does not claim native hard-timeout preemption. Geometry and
measurement interpretation remain in the same kernel owners.

## Supported contexts and remaining boundaries

Helios retains query targets for Parts and static Assemblies. Cadmata retains
document bodies and imported STEP assembly definitions with their world occurrence
placements. Both viewports integrate the shared tool into Telos and WebGL paths.
Scene/mesh-only builds, animated poses, and Cadmata stateless Firmament gallery
previews are explicitly unavailable. They need a retained geometry/pose context,
not a renderer-only calculation. No new Firmament syntax or engineering geometry
construction was introduced.

## Historical fillet assertions

The six `CanonicalGeometry` curved-fillet specimens previously asserted volumes
that the admitted volume authority could not prove. Those assertions are replaced
with `Assert Topology Body { shells: 1 }`. Normal compilation now admits them;
their detailed existing topology, surface-family and deterministic STEP tests
remain. A separate negative witness still requires an appended unprovable volume
assertion to reject with `assert-volume-measurement-unavailable`.

The CLI built controls from the original Git sources with only their unsupported
Volume assertion removed, then built the revised fixtures. All six STEP outputs
were byte-identical. Raw source controls, CLI JSON, STEP files and SHA-256 evidence
are under ignored `artifacts/local/measurement-parity/`.

## Qualification

Focused geometry tests exercise world-space point deltas, parallel-plane spacing,
nonparallel angles, rotated/repeated instances, parallel and skew axes, diameters,
stale topology identities, invalid rays, hole voids and unsupported foreground
surfaces with explicitly labeled display approximation. Server integration tests
exercise the actual HTTP contract, document revision invalidation, and imported
STEP assembly instance spacing. SDK transport tests check queueing, build revision
and preserved backend errors. Shared interaction tests cover A/B replacement,
rapid picks, late replies, failed replacements, units, clearing and orbit drags.

Browser evidence is stored under ignored `artifacts/local/measurement-*.jpg`.
The native Cadmata browser workflow measured a 10 × 20 × 6 mm box as 6 mm across
opposite support planes after orbiting, showed 90° for perpendicular faces, and
cleared the result after translation. This is browser-host qualification; it does
not claim a separately packaged WebView2 desktop release or an AOT production SDK
qualification.

### Geometry parity digests

Control sources were taken from baseline `d462d911cbf54f4343da2e732ab58753fcbe9bec`.
Only the unsupported Volume assertion block was removed from each control.
The following SHA-256 values match both the control and revised STEP bytes.

| Fixture | Matching STEP SHA-256 |
| --- | --- |
| `profile-edgefinish-chimera-fillet.firmament` | `4d4fbc833ae4ffaa1102160fa0823900acb2ee7dc8daa64bec340d41f8e79339` |
| `profile-edgefinish-chimera-reflex-sphere-compat.firmament` | `7e15adbd0025f6c1bb231f0c076e705d6d45f03039e971502719ea0bcc4b69d9` |
| `profile-fillet-convex-two-segment-bottom.firmament` | `16b51d19d299994a727dce81ad887bc3dea128f185324a38145330167d02b53d` |
| `profile-fillet-convex-two-segment-top.firmament` | `dbe3b1336a1ab791abef4b6c6558efd58a3d00c1af95da7049a0f5394b63901f` |
| `profile-fillet-reflex-two-segment-bottom.firmament` | `cfae62660893a9e2ddb413f1edbaf6b997e8750e6c16d5884ec2b1fb601a3bf6` |
| `profile-fillet-reflex-two-segment-top.firmament` | `6d2a3425db82f571b559f2db8374841340aab1bcd3726531a4417efb229a89e5` |

### Browser results on the final SDK

- Helios mounting plate: two rapid surface picks produce 27.612275 mm with
  ΔX 24.619761 mm, ΔY 12.502203 mm and ΔZ 0 mm. Gold witnesses track both picks.
- Selecting the cylindrical hole wall reports its authored 8 mm diameter.
- Editing the source clears the measurement and disables new queries while the
  last valid model remains visible.
- Industrial ATLAS builds with 24 definitions, 113 total hierarchy occurrences,
  and no diagnostics. A query distinguishes runtime face 2 on
  `AtlasIndustrial.UpperArm.ShoulderDrive.Cap` from runtime face 2 on
  `AtlasIndustrial.UpperArm.ElbowDrive.Cap`: 202.495056 mm between the picked
  world points (not axis centres). Inch presentation is 7.972246 in.
- Native Cadmata, Helios and ATLAS screenshots are retained at
  `artifacts/local/measurement-cadmata.jpg`, `measurement-helios.jpg`, and
  `measurement-atlas.jpg`. Browser workflows exercised WebGPU; WebGL integration
  was compiled and covered through component/shared-controller tests, without
  claiming a separate live fallback-browser qualification.

### Final gate

| Check | Result |
| --- | --- |
| Serial Release solution build | Passed; existing nullable/analyzer/WASM warnings remain |
| Core fast lane, Category != SlowCorpus | 1,087 passed |
| Final full solution lane, serial | 5,706 passed across 20 test suites; zero failures/skips |
| Cadmata frontend | 96 tests passed; production build passed |
| Helios frontend | 101 tests passed; TypeScript and development bundle build passed |
| Shared Telos | 15 tests passed; build passed |
| Public SDK transport | 16 tests passed; actual browser WASM package rebuilt and installed locally |
| Historical fillet geometry | Six byte-identical STEP comparisons; 44 focused assertion/fillet tests passed |
| Workspace whitespace checks | `git diff --check` passed in Aetheris and HeliosCAD |
| Host cleanup | Temporary browser tabs, native Cadmata host and Helios Vite host closed; no dotnet/testhost/MSBuild/VBCSCompiler hosts remained |

The full suite was repeated after the browser execution fix. Logs are retained in
ignored `artifacts/local/measurement-final-build.log`, `measurement-fast.log`,
`measurement-final-full.log`, and `measurement-fillet.log`. No AOT production SDK
publish or packaged desktop release was performed. The ordinary Helios production
build correctly requires that separate AOT package; the qualified bundle here uses
its supported development/interpreted-WASM lane.

The code-quality pass checked shared geometry authority, closed query modes,
revision invalidation, disposal, stale asynchronous replies, analytic versus
approximate labeling, field identity conversion, non-pickable overlays, preserved
geometry and compatibility of the additive SDK/HTTP contracts. The small product
bindings adapt their existing session owners; the shared tool contains no parser,
geometry solver or independent semantic validator.

## Verdict

**Success for the bounded measurement UI pass and assertion cleanup.** The actual
Cadmata and Helios paths now expose useful BRep-backed measurements with explicit
interpretation, reference identities, witnesses and stale-state handling. The
unprovable historical fillet volumes no longer block otherwise valid fixtures,
and their compiled geometry remains unchanged. General trimmed-face clearance,
retained Scene/motion measurement contexts and live WebGL/AOT/native desktop release
qualification remain explicit follow-ups rather than implied capabilities.
