# FTC-09 direct STEP editing - 2026-10-21

## Result

The bounded round through-hole path now moves and removes a real FTC-09 hole
through ordinary Firmament validation, build, dependency locking and qualified
STEP export/reimport. This is retained-BRep editing, with no inferred stock box,
whole-model rotation, Boolean surgery or mesh fallback. Slots and countersinks
remain explicit capability refusals. They are not admitted as partial round holes.

Final qualification is recorded below.

## Source and initial blockers

Input: `testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp`.
UTF-8/LF text pin:
`daad981598736d2ee6a104a7213b8d7813deaaabd7f683bc8cf8bbbf401a579a`.
The original source file is unchanged. Geometry-only revisions explicitly exclude
vendor annotation semantics; they do not claim PMI passthrough or preservation.

The plate lies in XZ, with material at Y=0..3.03784000001215 mm. Its nominal
world bounds are X=-95.25..95.25, Z=-139.7..139.7 mm. Source accuracy is 0.000001 mm;
qualification uses the default 0.001 mm numerical budget, not a manufacturing tol.

Initial discovery found 59 cylinder groups and admitted none. Two independent
blockers were reproduced:

1. `AppendSupportedOrphanPlanarFaces` grafted #6064 into the material solid even
   though it belongs to #6072 OPEN_SHELL / #6073 SHELL_BASED_SURFACE_MODEL. The
   actual MANIFOLD_SOLID_BREP #6015 selects CLOSED_SHELL #6014 with 158 faces.
   The extra face added four boundary edges and a detached component: 159 faces,
   458 edges, 304 vertices, degraded qualification.
2. The existing editor admitted only Z-axis holes. FTC-09's round through-holes
   run along Y. Reorienting or reconstructing the residual part would have been
   the wrong repair.

The importer now respects shell ownership when applying orphan compatibility.
A genuinely unowned supported planar face retains the historical compatibility
behavior; a regression source variant proves that distinction. The corrected
solid has 158 faces / 454 edges / 300 vertices and qualifies at 0.001 mm.

## Production changes

- Principal X/Y/Z axes share the existing round-hole recognition certificate.
  Only the selected wall geometry receives radial displacement. Retained support
  surfaces, topology identities, vertices, edges, unrelated loops and pcurves
  remain identical. Oblique axes require a qualified frame and still reject.
- `Remove Source.Region { within: { min: Point3(...); max: Point3(...) } }`
  shares recognition, local stock exclusion, suppression and qualification with
  Replace. It removes complete void walls and their two support inner loops;
  no caps are guessed. Retired faces have no fictitious successor mapping.
- `Source.Region.diameter`, `.depth`, `.center`, `.entry`, `.exit`, and Point3
  components bind as typed source-derived AST values inside imported Replace and
  Remove fields. They are currently local edit facts, not global Static values.
- New in-plane positions round to the 0.1 mm authoring grid. Imported diameter
  and support coordinates remain source-derived. A 6.35 mm hole remains 6.35 mm;
  numerical float noise is not copied into source or fixed by resizing the hole.
- CLI `imported-features plan --remove` produces a normally compiled, locked
  revision. Radial `--offset du dv` means YZ/XZ/XY for X/Y/Z-axis holes.
- Build JSON distinguishes `importedHoleRemoval` from `importedHoleRevision`.
  Migration reports separately count planned/verified/emitted removals and retired
  faces; removal is not reported as an emitted replacement.

Exactly one move or removal per single-body Model is admitted. Source PMI must be
explicitly excluded with `geometryOnly: true`. Moves generate new diameter/depth
PMI attached to successor walls. Removal generates no hole PMI and currently
rejects explicit authored PMI until successor annotation rebinding is qualified.

No new parser route, source rewrite, geometry engine or diagnostic allowlist was
introduced. AST statement/field consumption remains closed. The code-quality pass
checked owner boundaries, source spans, immutable inputs and actual report counts.

## FTC-09 witnesses

The pinned candidate `through-hole:step-1255+1282` comprises wall faces 31/32.
It is nominally diameter 6.35 mm, depth 3.03784 mm, centered at
X=-85.09, Z=19.05 mm. The upper support is STEP #5088 (face 124); the opposite
support is face 121. The cylinder axis points -Y; entry facts use the maximum
principal-axis station.

| Independent revision | Result | Faces / edges / vertices |
|---|---|---|
| Move | New center X=-70, Z=19.1; exact source Y and diameter retained | 158 / 454 / 300 |
| Remove | Original opening filled, no new hole | 156 / 448 / 296 |

For both revisions, all 156 residual surfaces and 448 residual edges retain their
bindings and geometry. Surviving vertices and pcurves remain identical. The source
body is unmodified. World bounds are unchanged. Independent trimmed-domain
queries on **both** supports prove old stock restored and new opening present only
for the move. Strict pcurve checks, enforcing export and qualified reimport pass.
Whole-body ray containment returns Unknown on this mixed-spline body; it is not
used as affirmative occupancy evidence or weakened to make these tests pass.
No exact whole-part volume claim is made.

Two further independent CLI specimens also planned and built with normal locked
admission and qualified export/reimport:

| Candidate | Operation | Source size retained | New in-plane center |
|---|---|---|---|
| `through-hole:step-3157+3184` | LargeHole move | diameter 19.05 mm | X=-31.8, Z=66 mm |
| `through-hole:step-1107+1134` | SmallHole removal | diameter 3.96875 mm | no new hole |

Their sources, receipts, build JSON and STEP files are under the local
`large-move/` and `small-remove/` evidence directories. These are independent
revisions of the original plate, not a claim of qualified edit batching.

Discovery now reports 25 admitted round through-holes among 59 cylindrical groups.
That is recognition, not a promise that all 25 have clearance at an arbitrary new
position. The remaining 34 groups have explicit family/refusal explanations.
Representative rejected specimens include corner breakout #198, countersunk shaft
#811/#838 and a slot end wall #3558. Their partial cylindrical surfaces cannot be
removed independently. Countersinks need a complete coaxial cylinder/cone/support
certificate; slots need a complete line/arc wall-and-rim certificate. Both must
qualify suppression and reinsertion of the whole void family before admission.
These are the next bounded extensions, not a reason to implement general Booleans.

## Import compatibility correction

Seven NIST snapshots previously included auxiliary surface faces. Source-shell
ownership tests prove that each corrected material face set exactly equals its
selected CLOSED_SHELL. Only these baselines were updated; input files were not.
Counts below are faces / edges / vertices.

| Source | Previous mixed ownership | Correct material shell |
|---|---|---|
| nist_ctc_02_asme1_ap242-e2.stp | 637/1416/876 | 635/1410/870 |
| nist_ctc_04_asme1_ap242-e1.stp | 484/1110/664 | 483/1108/662 |
| nist_ftc_07_asme1_ap242-e2.stp | 306/871/588 | 304/865/582 |
| nist_ftc_09_asme1_ap242-e1.stp | 159/458/304 | 158/454/300 |
| nist_ftc_10_asme1_ap242-e2.stp | 268/813/572 | 264/799/560 |
| nist_stc_07_asme1_ap242-e3.stp | 306/871/588 | 304/865/582 |
| nist_stc_08_asme1_ap242-e3.stp | 271/709/446 | 270/705/442 |

Canonical hashes for all corrected bodies were measured on Windows and Linux
(WSL archlinux, .NET 10.0.13 runtime installed under ignored artifacts). The
Linux-specific FTC-09 hash was regenerated from the actual Linux exporter; the
other platform overrides remain unchanged. This is an explicit import ownership
correction, not a claim that previously appended open-surface geometry is part
of the engineering solid. The old CTC-02 planar-cap roundtrip assertion also required these grafted
faces. It now proves that #3148/#3149 belong to separate Fill.1/Split.1 OPEN_SHELL
surface models and that those caps stay out of the 635-face material solid, both
on import and roundtrip. Its source circular geometry is independently checked.
The tessellation-only FTC-08-tg import-failure policy
and its baseline remain unchanged.

## Reproduction

Tracked runnable sources:

- [Move](../../../../fixtures/Regression/InlineStep/production/ftc09-round-move.firmament).
- [Remove](../../../../fixtures/Regression/InlineStep/production/ftc09-round-remove.firmament).

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll imported-features inspect testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll imported-features plan testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp --candidate "through-hole:step-1255+1282" --as MountHole --offset 15.09 0.05 --geometry-only --source-out artifacts/local/ftc09-direct-edit/move/revision.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll imported-features plan testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp --candidate "through-hole:step-1255+1282" --as MountHole --remove --geometry-only --source-out artifacts/local/ftc09-direct-edit/remove/revision.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate artifacts/local/ftc09-direct-edit/move/revision.firmament --locked --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build artifacts/local/ftc09-direct-edit/move/revision.firmament --locked --output artifacts/local/ftc09-direct-edit/move/revision.step --json
```

Use the analogous validate/build commands for removal. The source-only fixtures
use portable LF text pins; CLI plans use byte-pinned dependency receipts.

Local evidence: `artifacts/local/ftc09-direct-edit/`, including inspection and
plan/build JSON, original/moved/removed front SVG/PNG previews, revised STEP files,
Windows/Linux canonical hashes, and regression logs. SVGs are diagnostic wireframe
projections, not manufacturing drawings.

## Qualification

**Success for the bounded round through-hole family.**

- Release solution build: passed (existing nullable/SQLite WASM warnings remain).
- Fast kernel lane: **1,088 passed**, no failures or skips.
- Full solution gate: **5,882 passed across 20 test assemblies**, no failures or
  skips, including Core 1,243, Firmament 3,116, CLI 635, and editable V8 7.
- Linux focused kernel/ownership/corpus/CTC-02 roundtrip lane: **31 passed**.
  Windows SDK VSTest was run under the local Linux runtime with ReadyToRun
  disabled so Windows-native precompiled SDK code was not loaded on Linux.
- Both tracked FTC-09 fixtures validate at Built; all four independent locked
  revision sources build to qualified STEP artifacts.
- Initial full gate found the historical CTC-02 cap assertion described above;
  it was corrected with source ownership evidence, passed individually, and the
  complete solution gate was rerun to green. The original failing log remains
  local as `full-tests-initial.log`.
- `git diff --check` passed. The final code-quality pass retained one parser and
  geometric admission owner, checked diagnostic spans and actual removal report
  counts, and introduced no regex, global rewrite or fallback machinery.

The remaining kernel prerequisite is an explicit complete void-family certificate
for slots and countersinks. This release does not claim their editing support,
multi-operation batching, arbitrary frames, resizing or vendor PMI migration.

Updated public documentation:
[syntax](../../../public/firmament/syntax.md) and
[imported editing guide](../../../public/firmament/imported-feature-recognition.md).
