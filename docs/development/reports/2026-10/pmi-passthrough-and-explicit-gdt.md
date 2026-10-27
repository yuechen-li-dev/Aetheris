# Semantic PMI transfer and explicit GD&T

Date: 2026-10-27

## Behavior and ownership

An unchanged single-solid `InlineStep` now carries its semantic AP242 annotation
graph into the emitted part. Transfer retains formal dimensions, allowances,
controls, modifiers, ordered datum systems, zone definitions and source geometry
associations. Unique imported datum labels can be used by added author controls.
Assembly components still require explicit geometry-only import when annotated;
part requirements are not implicitly promoted into assembly requirements.
An explicit geometry-only dependency contributes its exclusion policy to the
module receipt identity, so it cannot alias an annotation-retaining binding of
the same source bytes. This changes the semantic receipt, not the imported shape.

Firmament's existing PMI parser/binder admits twelve explicit controls:
Straightness, Flatness, Roundness, Cylindricity, Parallelism, Perpendicularity,
Angularity, Position, LineProfile, SurfaceProfile, CircularRunout and TotalRunout.
These are annotations, not geometry constraints. A shared registry owns control
kinds and field admission. Unknown fields, unresolved/duplicate datum references,
inapplicable datums and contradictory modifiers reject. Source GD&T can retain
advanced forms beyond the bounded authoring syntax.

`Step242PmiTransfer` owns dependency closure, unit normalization and topology
rebinding. It requires an exact-source import receipt and unchanged-body witness.
It rejects missing dependencies, unknown incoming semantic owners, ambiguous
solid ownership and unmapped/split source topology. Auxiliary datum products and
shells retain their own ownership rather than becoming material-body faces.
Native emitters now associate GISU directly with the actual annotation feature.
Vendor-independent readback follows formal AP242 relationships; unnamed shape
aspects no longer collapse into a shared empty-name target.
Inspection also exposes direct usage-item identities separately from inferred
face adjacency. The regression oracle compares physical edge witnesses, not
temporary edge allocation numbers that change during reimport.

Length measures use their own declared unit, independently of geometry units.
`0.001 inch` becomes `0.0254 mm`; no authoring grid or fixed-decimal formatter is
applied to PMI. Derived units preserve length exponents. Angular units remain
separate. The geometry number writer and engineering lowerers are unchanged.

Foreign graphical glyphs are excluded with `pmi-source-presentation-excluded`:
their text can still contain inch values. Common supported labels regenerate
from semantic records. Unsupported or incomplete advanced 3D presentation rejects;
it does not silently remove datum or zone parameters. Source camera/font/layout
preservation, graphical-only transfer, topology-edit migration and assembly-wide
PMI transfer remain outside this capability.

## Regression witnesses

- [Explicit controls](../../../../fixtures/Canonical/PMI/explicit-gdt.firmament):
  a practical plate/hole with three datums and three controls; removing PMI emits
  the same geometry.
- [Imported CTC-01 plus an authored control](../../../../fixtures/Canonical/PMI/nist-ctc01-passthrough.firmament):
  retains source semantics and binds the added surface-profile control to existing
  A/B datums. This is a transfer witness, not an approved manufacturing revision.
- FTC-11's unqualified trims still reject through normal compilation, with or
  without added PMI. The interchange graph tests do not waive that admission gate.
- The old flatness refusal fixture is now a valid legacy-spelling fixture with
  real STEP round-trip and unchanged-volume evidence.
- Independent unit specimens cover inch geometry, mixed geometry/PMI units,
  derived length exponents and very small nonzero tolerances. Tiny tolerances are
  numerical precision tests, not suggested manufacturing requirements.

## Qualification method

The core corpus tests use `Step242Corpus` for all original imports. Each source
graph is transferred, reparsed and reimported. Checks cover dependency closure,
semantic-root counts, normalized values and allowances, modifiers, datum order,
zone forms and individual target correspondence. Auxiliary targets are tracked
separately from the material body. Geometry emission before/after adding PMI must
have an identical STEP geometry prefix; topology counts and surface families must
also survive reimport. This is not a claim of byte-identical vendor STEP output.

A separate real-CLI run validates every NIST specimen, builds and analyzes each
admitted source, and compares rejected files against explicit geometry-only
admission. Existing geometry refusals are classified separately from PMI transfer
failures. Tessellated-only source is not silently treated as an exact solid.

### NIST results through the real CLI

The run covers all 17 stored specimens. Five pass validation, build and analysis;
the remaining twelve reproduce the existing geometry admission refusal with
`geometryOnly: true` as well. There are no PMI-specific admission failures or
validate/build disagreements in this run.

| Specimen | Normal compilation | Datums / dimensions / controls |
|---|---|---|
| CTC-01 e1 | Admitted | 3 / 12 / 6 |
| CTC-02 e2 | Existing geometry admission refusal | — |
| CTC-03 e2 | Admitted | 6 / 10 / 13 |
| CTC-04 e1 | Existing geometry admission refusal | — |
| CTC-05 e1 | Admitted | 4 / 6 / 10 |
| FTC-06 e2 | Existing geometry admission refusal | — |
| FTC-07 e2 | Existing geometry admission refusal | — |
| FTC-08 e1-tg | Tessellated source; existing geometry admission refusal | — |
| FTC-08 e2 | Existing geometry admission refusal | — |
| FTC-09 e1 | Admitted | 8 / 22 / 31 |
| FTC-10 e2 | Existing geometry admission refusal | — |
| FTC-11 e2 | Existing geometry admission refusal | — |
| STC-06 e3 | Existing geometry admission refusal | — |
| STC-07 e3 | Existing geometry admission refusal | — |
| STC-08 e3 | Existing geometry admission refusal | — |
| STC-09 e3 | Admitted | 8 / 22 / 27 |
| STC-10 e2 | Existing geometry admission refusal | — |

All sixteen exact-source semantic graph round trips pass at the core interchange
layer, including the files rejected by normal engineering geometry admission.
Those core results do not qualify their trims for production compilation.
The combined CTC-01 fixture builds through the CLI with 3 datums, 12 dimensions
and 7 controls, including its new control. The explicit authoring fixture builds
with 3 datums and 3 controls.

Raw source specimens, STEP outputs, per-command JSON and corpus classification
are retained under ignored `artifacts/local/pmi-qualification/`.

### Build and regression evidence

Windows, .NET SDK 10.0.401; serial builds with node reuse and shared compilation
disabled. Final results:

| Check | Result |
|---|---|
| Complete solution Release build | 0 errors; 38 existing warnings |
| Focused core transfer tests | 25 passed, including 16 exact NIST specimens |
| Core fast lane | 1,136 passed |
| Complete solution regression | 6,192 passed, 0 failed, 2 skipped across 20 suites |
| Firmament suite within the full run | 3,283 passed |
| Browser SDK managed/WASM rebuild | Passed |
| SDK bridge contract tests | 19 passed |
| `git diff --check` | Passed |

The two skips are existing optional Drawing Notes rasterizer tests requiring
`AETHERIS_TEST_PDFTOPPM`; no .NET suite was omitted. Native tests exercise annotation
projection and annotated GLB geometry-buffer retention. This pass did not perform
a live Helios/WebGPU UI playtest or external STEP-tool certification.

The final gate was:

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build -m:1 -nr:false --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
```

Logs are under ignored `artifacts/local/pmi-*.log`. The CLI corpus snapshot uses
the same kernel DLL hash as the final build. No task-owned .NET hosts remained
after qualification.

## Code review and verdict

Reviewed parser/binder/export ownership, diagnostic propagation, geometry-number
formatting, source identity, module exclusion policy and browser consumption.
No new Firmament parser route, source rewrite system, geometric constraint solver
or native dependency was introduced. Source semantic readback, authoring admission
and emission share the existing AP242/PMI owners and the common control registry.

**Success for the bounded capability.** Semantic transfer and explicit GD&T work
through normal compilation for admitted geometry, with precision and association
evidence. Remaining NIST geometry admission refusals, advanced presentation,
topology-edit rebinding and assembly-wide transfer are explicit limits rather
than successful-looking omissions.

## References

The test data comes from the
[NIST MBE PMI validation and conformance project](https://www.nist.gov/ctl/smart-connected-systems-division/smart-connected-manufacturing-systems-group/mbe-pmi-0).
Schema interpretation was checked against
[STEP Tools' tolerance model](https://downloads.steptools.com/docs/stp_aim/stix_tolerance.html)
and the
[CAx-IF semantic PMI recommended practices](https://www.mbx-if.org/home/wp-content/uploads/2024/06/rec_pracs_pmi_v408.pdf).
This is corpus qualification, not third-party AP242 or GD&T certification.
