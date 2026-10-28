# Final-body SDF volume fields — 2026-10-28

## What now works

Native and imported rectangular plates with separated circular Z-through holes
can use bounded SDF volume through the real compiler/CLI. Authority comes from
complete recognition of the **final exported boundary**, rather than from a
display mirror or a field describing the unedited stock.

`BrepSdfVolumeLowering` reuses `BrepRectangularPlateQuery`; it does not duplicate
its recognition schema. The existing query accounts for every face, loop and edge.
The field contains the translated stock and every admitted through-hole subtraction.
Tools extend past both stock caps so the field does not leave spurious zero-valued
disks at hole mouths. One qualified closed outer material shell is required.
Uncovered edits reject the complete field rather than producing a partial model.

The existing query's numerical matching tolerances remain unchanged (1e-7 mm
linear matching and 1e-12 axis alignment). Qualification is conditional on that
analytic boundary-recognition contract and the integrator's floating-point guards;
it is not a formally outward-rounded certificate for arbitrary noisy BReps.

`FirmamentMeasurements` caches this recognition per measurement object and uses
the existing JudgmentEngine volume policy. Exact measures retain precedence;
`--sdf` requests the bounded field explicitly. Existing exported-primitive authority
remains available where the compiler already qualifies it. No legacy Boolean mirror
was promoted wholesale.

JSON adds `fieldQualification`, `fieldCoveredFaceCount` and
`fieldSourceStepSha256`. The hash identifies the exact UTF-8 final STEP artifact,
including metadata; it is not advertised as a geometry-only identity.

## Actual CLI evidence

All cases request ±20 mm³ integration uncertainty and finish without exhaustion:

| Case | Analytic volume mm³ | Reported interval mm³ | Regions | Covered faces |
|---|---:|---|---:|---:|
| Native 80×50×8 plate, one Ø8 hole | 31597.876 | [31561.277, 31601.270] | 57,373 | 7 |
| Native plate, two Ø8 holes | 31195.752 | [31162.263, 31202.258] | 69,437 | 8 |
| Re-imported one-hole STEP | 31597.876 | [31561.277, 31601.270] | 57,373 | 7 |

The one/two-hole intervals are disjoint, so omission of the second hole cannot
pass this comparison. Tests also supply a stale stock mirror and require the field
to follow the final perforated body. Hole-center and cap-mouth point checks detect
accidental retained material. Unsupported fillets remain unavailable.

The imported specimen uses the existing dependency lock and geometry-only source
pinning rules. The actual CLI `build → lock → measure --sdf` sequence succeeds;
the input STEP hash remains unchanged. Tests retain the final STEP text before and
after measurement and require equality. Reproduction is in
`fixtures/Regression/Assertions/README.md`.

## CTC-01: concrete remaining blocker

The actual imported source builds. Explicit SDF measurement still exits 1 with
`Unavailable`, identifying **face 4's cylindrical stock/boss support**, which cannot
be treated as a through-hole wall by the complete rectangular-stock recognizer.
No stock-only fallback is returned.

Current CLI analysis reports 117 faces: 56 planes, 57 cylinders and four cones;
there are no unrecovered spline surfaces. Fourteen edge curves are BSplines.
This is not a recurrence of stricter surface-recovery admission.

The qualification tool now runs the existing analytic ray-consensus point query
with one reused query context. On a deterministic 5×5×5 probe:

- 65 points return Outside, one Inside, and **59 Unknown**.
- The point-query loop takes approximately 395 ms on this machine.
- The output explicitly says `PointProbeNotVolumeQualification`.
- Vertex extents choose the sample domain only; they are not certified enclosing
  bounds. No sample-derived volume or independent sign-accuracy claim is made.

Reproduce with:

```powershell
dotnet tools/Aetheris.SdfVolumeQualification/bin/Release/net10.0/Aetheris.SdfVolumeQualification.dll --imported-occupancy-probe testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp
```

The smallest general imported-body prerequisite is in the existing spatial/trim
query owner: resolve the unknown point cases, establish conservative trimmed-solid
bounds, and provide conservative cell classification (including Unknown/Cut).
Point consensus is not Euclidean distance, and it does not establish absence of a
thin feature between samples. Once that capability is qualified, adapt the existing
volume integrator to it. A fitted field built from the same suspect STL would not
be an independent check and was not added.

## Qualification and quality

Final qualification:

- Release solution build: zero errors (38 existing nullable/analyzer/WASM warnings).
- Focused Continuum lane: 13 passed; focused compiler/assertion lane: 21 passed.
- Fast Core lane: 1,168 passed; full Core corpus lane: 1,356 passed.
- Full solution gate: **6,323 passed, zero failed, two optional external PDF-renderer
  skips**, across twenty test assemblies. Existing canonical/interchange and
  editable V8 qualification remain green.
- Managed/WASM SDK rebuilt; all nineteen JavaScript SDK tests passed.
- Production native/imported CLI measurements succeed; CTC-01 reports explicit
  refusal; source STEP hash is unchanged; `git diff --check` passed.
- Task-owned build, test and probe hosts exited; no .NET/test hosts remained.
- Serial Release builds used `-m:1 -nr:false -p:UseSharedCompilation=false`;
  the full test gate used `--no-build -m:1 -- RunConfiguration.MaxCpuCount=1`.

Focused tests cover complete final-body provenance, stale mirrors, every hole, empty mouths,
imported source preservation, unqualified shells, uncovered fillets and CTC-01
refusal. CTC-01 tests reuse the existing memoized `Step242Corpus` helper; repository
root discovery was made independent of the manifest runner so that helper can be
linked into Continuum tests without pulling in the corpus-report implementation.

No geometry builder, exporter, parser, display tessellator or public syntax changed.
There is no new global cache, alternate volume engine or permissive admission flag.
Generated CLI JSON, source STEP, dependency lock, point probe and test logs live
under ignored `artifacts/local/sdf-final-body/`.

**Verdict: Meaningful progression.** Complete final-boundary SDF admission now
works for an edited and imported engineering family. General CTC-01 SDF volume
remains explicitly unqualified, with measured point-query gaps and the missing
conservative trimmed-cell capability identified.
