# X1 — Standard Library qualification

## Executive verdict

**Meaningful progression, not full X1 acceptance.** Aetheris now ships one authoritative embedded catalog containing eight recognizable native product families. Forge Protocol v1 discovers and describes all eight, invokes seven qualified mechanical/electronics variants through their real Firmament → BRep → AP242 path, and serves the same returned STEP to Cadmata preview and download. The existing Sheet Metal electronics enclosure remains discoverable with formed STEP, flat STEP, SVG, and DFM.

Three language/host gaps prevent an honest full-success verdict: standalone Firmament has no public shipped-module import syntax; Record field defaults are not projected from `Static` into Forge schemas; and Pattern-generated variable product layouts plus nested Template PMI do not survive the current host-specialization route cleanly. Canonical consumer fixtures therefore use Forge requests, gallery defaults remain presentation data, repeated layouts are finite explicit semantic holes, and AP242 reinspection reports no PMI for the new native families. These are recorded rather than hidden behind copied geometry or a parallel schema.

## Product families

| Namespace | Policy / default | Main capability | Output |
| --- | --- | --- | --- |
| `Standard.Products.Office.Paperclip` | `PaperclipPolicy` / `StandardPaperclip` | Path, circular Sweep, material | STEP |
| `Standard.Products.Mechanical.MountingPlate` | `MountingPlatePolicy` / `StandardMountingPlate` | plate, four counterbores | STEP |
| `Standard.Products.Mechanical.BearingBlock` | `BearingBlockPolicy` / `StandardBearingBlock` | base, Boss, bore, mounts | STEP |
| `Standard.Products.Mechanical.MachinedAngleBracket` | `MachinedAngleBracketPolicy` / `StandardMachinedAngleBracket` | L profile, holes | STEP |
| `Standard.Products.Mechanical.ShaftCollar` | `ShaftCollarPolicy` / `StandardShaftCollar` | circular stock, bore | STEP |
| `Standard.Products.Mechanical.FlangedAdapter` | `FlangedAdapterPolicy` / `StandardFlangedAdapter` | flange, bore, fixed six-hole layout | STEP |
| `Standard.Products.Electronics.RackPanel` | `RackPanelPolicy` / `StandardRackPanel` | panel, symmetric mounts | STEP |
| `Standard.Products.Mechanical.Standoff` | `StandoffPolicy` / `StandardStandoff` | circular spacer, bore | STEP |
| `Standard.SheetMetal.ElectronicsEnclosure` | `EnclosureSpec` | formed Sheet Metal, flat pattern, DFM | STEP, flat STEP, SVG |

The authoritative native source is `Aetheris.Kernel.Firmament/Standard/StandardProducts.firmament`, embedded in `Aetheris.Kernel.Firmament.dll`. Forge isolates one Template from that catalog before native geometry-route selection; stable public IDs do not depend on the source file path.

## Architecture and Forge.Host

Families follow typed `Record → Static default → Template<Policy> → Require → semantic geometry`. Material catalog identities are policy values. Forge `list`, `describe`, and `invoke` are the only protocol operations; X1 adds no product-specific RPC. `describe` exposes Record fields, types, metric units, named constraints, output kind, and artifacts. Template versions are deterministic content hashes (`1+<12 hex>`); repeated builds with unchanged catalog source retain IDs and versions.

Canonical language-neutral invocations live under `fixtures/Canonical/Integration/standard-products/`. The invalid mounting-plate fixture isolates `HoleSpacingXFitsPlate` and returns `firmament-template-require-failed` before BRep materialization.

## Demo gallery

Cadmata exposes a Product Gallery tab. It discovers the catalog from `/api/v1/gallery/templates`, derives controls from Forge fields/types/units/enums, invokes `/api/v1/gallery/templates/{id}`, imports the returned `StepAp242` text into the shared viewport, and downloads that exact content. Sheet Metal artifact buttons include formed STEP, flat STEP, and SVG. Diagnostic messages from Forge are displayed without collapsing them to “Generation failed.” Metric units are the only X1 UI.

The Viewer remains the default tab to preserve the established file-opening workflow; the gallery is a sibling first-class surface. Paperclip retains its separate `MAXIMUM PAPERCLIPS` action and copy.

## Geometry and determinism evidence

All rows below were invoked twice by `StandardProductLibraryTests`, produced identical specialization identities and SHA-256 values, reimported successfully, and were independently inspected with `Aetheris.CLI inspect --json` as one enclosed manifold body.

| Family | Faces | Plane / cylinder | Bounds mm | SHA-256 |
| --- | ---: | ---: | --- | --- |
| Mounting Plate | 46 | 14 / 32 | `120 × 80 × 10` | `410b0e65f38fbc614a8cd5ffaaa45d1ed71de411500f551521bc0032d288b0ef` |
| Bearing Block | 27 | 7 / 20 | `100 × 50 × 32` | `845ef9d729f7fe5d8abcc3d94579959ac2f1fc37e8599f0ad29d1205998edb93` |
| Machined Angle Bracket | 16 | 8 / 8 | `80 × 60 × 10` | `a70a28a94b6c7e828b1ab62a8b9d0f12d79cd28ca622c343179fb6c63002639c` |
| Shaft Collar | 10 | 2 / 8 | `32 × 32 × 12` | `4fdf4905c3b3609ec52691398bea1701d0283c69e126a6b7f4c8e44e1b010311` |
| Flanged Adapter | 34 | 2 / 32 | `80 × 80 × 12` | `e2ed047e3774464d341019ac241a8b9b7e91280cd05d5c9f9db1790b6af7cdf9` |
| Rack Panel | 14 | 6 / 8 | `482.6 × 44.45 × 3` | `c3ac407c2982e295ee16fb33b353eec2506f0acaa0d1a75d28e5bb7a84f43849` |
| Standoff | 10 | 2 / 8 | `10 × 10 × 25` | `93c4e755bd2289cfac223c6f5434a5cba3a80c1968168ea15381d6f0cfbd939b` |

The inspected native STEP files reported `datumCount=0` and `dimensionCount=0`; source-level nested PMI is therefore not claimed as qualified output.

## Capability coverage

| Family | Template | Boss | Hole / Counterbore | PMI reimport | Material policy | Sheet Metal | DFM | Sweep |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Paperclip | ✓ |  |  |  | ✓ |  |  | ✓ |
| Mounting Plate | ✓ |  | ✓ / ✓ | no | ✓ |  |  |  |
| Bearing Block | ✓ | ✓ | ✓ | no | ✓ |  |  |  |
| Angle Bracket | ✓ |  | ✓ |  | ✓ |  |  |  |
| Shaft Collar | ✓ |  | ✓ |  | ✓ |  |  |  |
| Flanged Adapter | ✓ |  | ✓ | no | ✓ |  |  |  |
| Rack Panel | ✓ |  | ✓ |  | ✓ |  |  |  |
| Standoff | ✓ |  | ✓ |  | ✓ |  |  |  |
| Electronics Enclosure | ✓ |  | openings |  | existing | ✓ | ✓ |  |

## Fresh-agent tests

The selection outcomes were reviewed against the published index, but no isolated fresh-agent run was executed in this qualification:

- `120 × 80 mm aluminum plate with four counterbored mounting holes` → Mounting Plate policy; no manual geometry.
- `60 mm outer diameter, 20 mm bore, six mounting holes` → Flanged Adapter is the closest family, but its currently qualified fixed bolt layout is disclosed. A caller can change outer diameter and bore policy; it must not infer standards compliance.
- `helical auger` → no catalog family. Current X1 must reject family abuse and identify general helical geometry as unsupported.

The absence of Firmament module import prevents the requested literal fresh-agent standalone source test; Forge request fixtures are the qualified consumer form. Fresh isolated-agent first-attempt rates remain an acceptance gap.

## Reusable abstraction findings

| Candidate | Disposition | Reason |
| --- | --- | --- |
| four-hole layout | `FutureCandidate` | repeated semantics exist, but variable Pattern capture is not yet clean across host specialization |
| circular bolt layout | `KeepLocal` | current qualified flange uses one fixed six-hole layout; general bolt count is not implemented |
| material policy | `KeepLocal` | identity is repeated, but material attachment behavior differs by modeling route |

## Friction and bugs

| Finding | Classification | Disposition |
| --- | --- | --- |
| Multi-Template native module confuses geometry-route detection | `MustFixX1` | fixed by deterministic one-family catalog isolation |
| no public Firmament module import | `LibraryDesignIssue` | documented; blocks standalone namespace invocation fixtures |
| `Static` Record defaults absent from Forge field schema | `LibraryDesignIssue` | gallery presentation defaults are centralized; schema still owns controls/types |
| variable arithmetic in composed-hole centers and complex `Require` expressions | `LibraryDesignIssue` | bounded families use concrete finite layouts and simple semantic gates |
| direct cylindrical Hole uses Box-oriented modification path | `DocsFix` | circular parts use profile composition |
| nested Template PMI absent after AP242 reinspection | `MustFixX1` unresolved | claims removed; exact `0/0` inspection evidence recorded |
| Pattern intent not retained in new product variants | `PostX1` | no opaque or fake Pattern metadata emitted |

## Deferred work

General weldments, piping, surfacing/Loft/Shell/Draft, injection molding, arbitrary Boolean, and helical products remain outside X1. No new modeling domain or solver architecture was introduced.
