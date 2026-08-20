# X1a — Standard Library semantic closure

## Executive verdict

Accepted

The four X1 semantic gaps are closed through the real compiler, Forge, client, BRep, AP242, and packaged execution paths. No product family or modeling domain was added.

## Gap 1 — Library resolution

Canonical standalone consumption uses `Use Standard.Products.Mechanical` (or `.Electronics` / `.Office`) plus fully qualified exported identities. `FirmamentStandardLibraryResolver` links only named exports from the one embedded `StandardProducts.firmament` authority. It admits no path, current-directory lookup, remote package, or arbitrary textual include.

Resolution precedence for X1a is deterministic: explicit qualified local references remain local; selected shipped exports are linked only by their qualified identity; a collision with the selected compiler-local export name is an error. Unknown modules, declarations, missing `Use`, malformed packages, and collisions have library-specific diagnostics. User modules and cycles are intentionally not introduced.

The canonical witness `fixtures/Canonical/Integration/standard-products/mounting-plate-library-use.firmament` uses the shipped mounting-plate default, derives a policy with `with`, and builds AP242 without copying implementation source. A freshly packed and installed `Aetheris.CLI` tool built that fixture after only the consumer file was copied into a temporary directory outside the repository; reinspection found one enclosed 46-face body and 1 datum / 1 dimension.

## Gap 2 — Static defaults

`FirmamentTemplateHostBridge.InspectModule` now returns resolved immutable Static Record metadata from the existing Template binder. It therefore supports direct Record literals, `with`, table rows, nested records, and current deterministic compile-time values without raw-source default parsing or a Forge interpreter.

For `StandardMountingPlate`, Forge describes `Width.Default = 120mm`, `Width.Unit = mm`, and `Material.Default = Standard.Materials.Aluminum.6061_T6`; fields with defaults are not required. Product Gallery removed its product-specific engineering-default table and initializes controls from `describe`. The five existing Sheet Metal families now follow the same authoritative `Static` default path, including a zero-argument Electronics Enclosure invocation.

```firmament
Static StandardMountingPlate: MountingPlatePolicy = MountingPlatePolicy {
    Width: 120mm Height: 80mm Thickness: 10mm
    HoleDiameter: 6.6mm HoleSpacingX: 90mm HoleSpacingY: 50mm
    CounterboreDiameter: 11mm CounterboreDepth: 4mm
    Material: "Standard.Materials.Aluminum.6061_T6"
}
Template<P: MountingPlatePolicy = StandardMountingPlate>
```

The packaged Forge projection reports `P` as optional with default `StandardMountingPlate`; its nine fields are ordered deterministically, typed (`Length` or `string`), carry `mm` where applicable, and are all optional because their resolved Static values exist.

## Gap 3 — Pattern retention

The Flanged Adapter policy now includes bounded `BoltCount` values from 4 through 8, with qualified variants for 4, 6, and 8 selected by compile-time `Match`. Default six-hole coordinates are unchanged. Specialization produces one `BoltPattern` over the selected Static array and the canonical generator `FlangeBolt`.

The retained semantic report contains Pattern name, source array, generator, count, distribution, and generated instance IDs. Both CLI `inspect --json` and `build --json` expose it. Canonical static lowering materializes explicit hole features for the BRep plan; topology is necessarily explicit after that boundary. An eight-hole build reports `BoltPattern`, source `Bolt8`, generator `FlangeBolt`, count 8, and nine total holes including the bore.

| Stage | Before X1a | After X1a |
| --- | --- | --- |
| Specialized semantic IR | explicit generated holes only | `Pattern(BoltPattern, Bolt8, FlangeBolt, 8)` plus `BoltPattern[0..7]` relationships |
| Feature AIR | holes usable by construction | holes retain generated IDs and Pattern provenance |
| BRep plan | explicit topology | explicit topology (deliberate expansion boundary) |
| Public report | no Pattern inventory | name, source, generator, count, instances, distribution, retention status |

## Gap 4 — Nested PMI

Template specialization now recursively expands finite nested Template applications. Each level retains its own `ConceptIrTemplateInstantiation`; nested concrete declarations flatten before Feature AIR, while PMI blocks are lifted to the canonical model boundary after value substitution. This keeps PMI bound to specialized feature identities rather than post-export name search.

Both one-level Standard Mounting Plate PMI and a two-level `Outer → Inner → geometry + PMI` witness export and reinspect as one datum plus one diameter dimension. The build parity check parses specialized source, so silent loss now fails.

The nested witness inventory is stable across the boundary: authored `Datum A` + `HoleDiameter BoreDiameter`; specialized inventory `1 + 1` attached to the inner specialized hole; AP242 inventory `1 + 1`; reinspection inventory `1 + 1`. The Standard Mounting Plate integrated path reinspects Ø6.6 mm with +0.1/-0.0 tolerance and eight associated `ADVANCED_FACE` IDs through `GEOMETRIC_ITEM_SPECIFIC_USAGE`. Binding uses specialization identities and materialization correspondence, not an exporter-side feature-name search.

## Integrated witness

`fixtures/Canonical/Integration/standard-products/flanged-adapter-eight-pattern.firmament` combines shipped import, `Static with`, Template specialization, a variable Pattern, repeated holes, datum/diameter PMI, BRep materialization, and AP242 reinspection. `fixtures/Canonical/Templates/nested-template-pmi.firmament` isolates two-level PMI preservation.

## Existing products

All eight native Standard Products and all five existing Sheet Metal families remain registered under their stable public IDs. Mounting Plate, Bearing Block, and Flanged Adapter STEP bytes change because AP242 now contains their authored PMI. Flanged Adapter default geometry remains six holes but now carries Pattern provenance. Sheet Metal geometry is unchanged; its former gallery defaults moved into named `Static` records.

| Family | Final specialization | Final STEP SHA-256 | X1 hash disposition |
| --- | --- | --- | --- |
| Paperclip | `template:e4037e9f05e7e73a` | `6cdcbfbb407cbcf86d26f0e59482e776e8fd6fa02158df67c800fc4afc32c305` | requalified; X1 report had no hash baseline |
| Mounting Plate | `template:14168b831dfb927d` | `88b68d29deef6a4f0b0cfbdf806e90ccd56c0a36c037169bdc60a637b92952fe` | expected change from `410b…`: authored PMI now exported |
| Bearing Block | `template:750d56ab4980c95e` | `d552a9bfa966885a165034bb9821122607ffa1905cd5328c6e774a72f0e6f558` | expected change from `845e…`: authored PMI now exported |
| Machined Angle Bracket | `template:b8132daafe2d49d3` | `a70a28a94b6c7e828b1ab62a8b9d0f12d79cd28ca622c343179fb6c63002639c` | unchanged |
| Shaft Collar | `template:8ab75bb46f5ac6df` | `4fdf4905c3b3609ec52691398bea1701d0283c69e126a6b7f4c8e44e1b010311` | unchanged |
| Flanged Adapter | `template:8e94f03dc017bd7d` | `e854a7b398f19051f98dd3f281a6a788f52abb1ec5571ade5ecb00835e3d82a6` | expected change from `e2ed…`: Pattern provenance and PMI |
| Standoff | `template:72374172dd3b36cf` | `93c4e755bd2289cfac223c6f5434a5cba3a80c1968168ea15381d6f0cfbd939b` | unchanged |
| Rack Panel | `template:a67e7089b42dd413` | `c3ac407c2982e295ee16fb33b353eec2506f0acaa0d1a75d28e5bb7a84f43849` | unchanged |

All rows invoked successfully with canonical defaults through the final published host. Existing Standard Product tests repeat each invocation, compare specialization/hash stability, and reimport STEP. The eight-hole non-default Flanged Adapter hash is `1941b7aa20df3a2a7640cb192c0c1847fcc42aa8184c7a391227ac2c6c24d390`.

## Fresh-agent tests

- A — source import: passed using only public docs and canonical fixtures. The independently authored qualified consumer built an enclosed 120 × 80 × 10 mm plate with 46 faces and datum/diameter PMI.
- B — packaged schema defaults: passed from a fresh directory against the NativeAOT host alone. `list → describe` exposed all nine mounting-plate field defaults; zero-argument invocation produced a 60,799-byte AP242 artifact in 22.3442 ms.
- C — variable Pattern: passed. `BoltCount: 8` produced eight Ø6.6 bolt holes, one bore, and a STEP identical between Forge and the canonical fixture (`1941b7aa20df3a2a7640cb192c0c1847fcc42aa8184c7a391227ac2c6c24d390`). Public CLI inspection now exposes the retained generator and count directly.
- D — PMI parity: passed. Standard Mounting Plate AP242 reinspection returned datum `A`, Ø6.6 +0.1/-0.0, and eight associated `ADVANCED_FACE` entity IDs connected through `GEOMETRIC_ITEM_SPECIFIC_USAGE`.

## Performance

Resolution is a bounded in-memory catalog lookup and declaration link. Static metadata uses the existing binder pass. The five focused semantic witnesses complete in under one second on the qualification machine; a packaged default Mounting Plate invocation reported 22.3442 ms. No material regression requiring caching was observed.

## Bugs found

- Canonical advanced Concept fallback did not parse PMI; it now shares the canonical PMI binder.
- Build parity parsed pre-specialization source; it now validates the specialized semantic source.
- Nested applications were incorrectly discovered inside Template declaration spans; they now expand recursively at their containing specialization.
- Compact Standard Product PMI had never been admitted because it remained nested; it is now canonical multiline source and is preserved.
- NativeAOT `describe` attempted reflection-based JSON string decoding; the host now uses its source-generated JSON context.
- CLI `inspect` and `validate` bypassed shipped-library resolution; both now share the build resolver and have a CLI regression witness.
- Pattern inventory existed in the compiler result but was omitted from public CLI JSON; `inspect` and `build` now expose it.
- Removing the client default map revealed that the five legacy Sheet Metal descriptors had no defaults; their values now live in authoritative Firmament `Static` records.

## Qualification matrix

- Release solution build: 0 warnings, 0 errors.
- .NET: 3,066 passed, 0 failed; the existing empty FrictionLab test assembly reports no discoverable tests.
- Canonical corpus: 72/72 fixtures passed.
- Client: 16 files / 82 tests passed; production build and lint passed.
- VS Code extension: typecheck, 13 tests, build, and VSIX package passed.
- Packaged paths: a freshly installed CLI tool built the standalone consumer outside the repository; a final NativeAOT Forge host described and invoked canonical defaults outside the repository.
- Invalid witnesses: unknown shipped module returned `firmament-library-unknown-module`; flange `BoltCount: 9` returned `firmament-template-require-failed` for `BoltCountMaximum`.
- Public documentation qualification passed in the full CLI suite; the Standard Library entry points also passed a relative Markdown-link scan.
- Determinism: two packaged Mounting Plate invocations were byte-identical, 60,799 bytes, SHA-256 `88b68d29deef6a4f0b0cfbdf806e90ccd56c0a36c037169bdc60a637b92952fe`.
- Repository: layout guard, Markdown link scan, and `git diff --check` passed.

NativeAOT publication emits existing cross-project trim/AOT analysis warnings in optional reflection/EF/drawing paths. They were collected rather than hidden; the final executable's `list`, both product and Sheet Metal `describe`, and zero-argument AP242 invocation paths all ran successfully outside the repository. No new warning appears in the ordinary Release solution build.

## Scope

No family, geometry feature kind, Pattern kind, PMI kind, solver, package manager, remote resolution mechanism, or gallery redesign was added.
