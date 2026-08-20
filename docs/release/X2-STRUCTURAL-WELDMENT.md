# X2 — Structural / weldment foundation

## Executive verdict

**Accepted.** Aetheris represents the bounded X2 structural/weldment foundation semantically from skeleton through fabrication output. The flagship workbench completes the real parser → AIR → joint → BRep → AP242 assembly → reimport/Cut List path. Forge Protocol template invocation and independent fresh-agent A/B/C authoring both pass through their packaged, public paths.

## Semantic architecture

```text
Structure nodes/paths
→ StructuralMemberAir (path + section + material + orientation + end interfaces)
→ deterministic Butt/Miter joint resolution
→ explicit assembly joint interfaces
→ per-member end planes and treatments
→ exact enclosed member BReps
→ AP242 product definitions and occurrences
→ Cut List IR and JSON
```

Profile geometry is materialized only after joint resolution. Assembly semantics were audited and reused for AP242 product identity; the general mate solver was not stretched into a fabrication cut solver.

## Sections and joints

X2 admits SquareTube, RectangularTube, RoundTube, Angle, FlatBar, and RoundBar. Material is resolved independently through the existing catalog. Tubes retain true inner walls; round square-cut tubes use analytic cylinders. Polygonal two-member miter joints route through a bounded JudgmentEngine policy. The separating and reflex bisectors have explicit admissibility, utility, tie-breaking, and rejection evidence; the same-side reflex candidate is rejected as overlap-producing. The compiler compares independently materialized end-loop point sets and proves the complete participant vertex sets remain in opposite retained half-spaces. Butt contacts receive the same post-materialization half-space proof. Butt joints identify a through/Primary member and trim the terminating member to its envelope. Round-section miters, collinear joints, multi-member miters, coping, and arbitrary profile intersections reject explicitly.

## Fabrication outputs

The canonical build emits an AP242 assembly of independent member definitions plus deterministic Cut List JSON. Weld requirements retain weld, joint, type, size, and member identity without pretending to supply bead geometry or AWS compliance. CLI `inspect`, `validate`, and `build --json` expose structural records directly. Member/Joint/Weld graphs can be authored as checked columnar `Static Table` data, and the Standard Structural workbench Template generates those tables from six policy fields before the same AIR lowering.

## Workbench evidence

Final source: `fixtures/Canonical/Structural/welded-workbench.firmament`.

- 10 members, 12 joints, 12 semantic fillet welds
- four cut-list groups: two 1000 mm miter rails, two 600 mm miter rails, two 560 mm crossmembers trimmed both ends, four 780 mm legs
- 40 × 40 × 3 mm square tube, `Standard.Materials.Steel.ASTM_A36`
- overall BRep bounds: `[-20,-20,0]` to `[1020,620,820]` mm
- 10 enclosed member bodies, 100 analytic planar faces
- AP242 assembly reimport: 10 geometric definitions and 10 occurrences
- nominal assembly mass: approximately 25.93 kg

The final manual artifact is `artifacts/local/x2/final/x2-welded-workbench.step`, SHA-256 `d401a4bf453835d79140bbc19116de5942313343ab886c4c7532b851d228ed88`. It contains 10 independent definitions and occurrences plus 12 reported joint interfaces; all four miter interfaces have coincident shared cut surfaces. Picture-frame rails retain mirror-opposed cuts and reimport as isosceles trapezoids rather than parallelograms. Its sibling Cut List is `artifacts/local/x2/final/x2-welded-workbench.cutlist.json`. Independent repeat builds produced byte-identical STEP output.

## Qualification and locality

Canonical member, orientation, butt, miter, frame, and workbench fixtures are present. Seven invalid fixtures isolate zero length, invalid dimensions, excessive wall, missing asymmetric orientation, disconnected joint, unsupported round miter, and unknown-member diagnostics. Determinism tests compare STEP text, hash, and cut-list JSON. A deliberately malformed cap binding is rejected before export with `structural-member-cap-support-plane-mismatch:<member>:<end>`; the message reports plane deviation, explains the STEP/parallelogram consequence, and supplies the correct start/end normal formula. Changing Butt/Miter treatment is localized to joint resolution and member end planes; graph, section, and material records remain unchanged.

Forge Protocol v1 lists, describes, and invokes `Standard.Structural.WeldedWorkbench`, returning AP242 and Cut List JSON without a structural-specific RPC. A published Forge Host invocation produced both artifacts successfully. A fresh agent, restricted to public docs and canonical fixtures, authored a rectangular butt-welded frame, four-legged Cut List-ready workbench, and miter variant; all three validated, built, and reimported on the literal first attempt with zero diagnostics. Its report is under `artifacts/local/x2/fresh-agent/qualification-report.md`. No piping, surfacing, weld analysis, connection design, or structural FEA scope was introduced.

## Friction and deferred work

- **MustFixX2:** none.
- **DocsFix:** resolved fresh-agent findings by documenting fixed table schemas, centerline dimensions, raw versus finished length, ignored miter `Primary`, and treatment-neutral identity naming.
- **DeferredX2a:** end caps, channel, weld PMI symbols, exploded view, generalized member-interior interface syntax.
- **FuturePiping:** routed/curved tube and fishmouth coping.
- **FutureSurfacing:** freeform multi-member intersection solving.
