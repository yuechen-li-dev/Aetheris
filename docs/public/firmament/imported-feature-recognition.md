# Imported through-hole revisions

Firmament can replace one isolated round through-hole in a pinned imported STEP
body, retaining the actual residual BRep. The first admitted operation moves the
hole without changing its diameter or depth. It does not reverse-engineer the
whole part or reconstruct stock from a guessed HostSize.

The executable golden source is
[`ctc01-hole-move.firmament`](../../../fixtures/Regression/InlineStep/production/ctc01-hole-move.firmament).
It moves a Ø25 mm hole on NIST CTC-01 from (-160,45) to (-130,45) mm, through the
same Z=-50..0 mm material column.

```firmament
Model Revision {
    units: mm
    InlineStep Source {
        path: "ctc01.stp"
        textSha256: "341cea888fb316673ed683e59391c11b4f96cff2542d9387e135f06682dbd745"
        importTolerance: 0.01mm
        geometryOnly: true
    }
    Recognize Source {
        Region MountHole {
            kind: HoleShaft
            stepFaceEntities: ["#764", "#765"]
            evidence: { radius: 12.5mm; through: true }
        }
    }
    Replace Source.MountHole with Hole<Shaft> MountHole {
        on: Source.Face(98)
        center: Point3(-130mm, 45mm, 0mm)
        diameter: 25mm
        end: ThroughAll
        within: {
            min: Point3(-175mm, 30mm, -50mm)
            max: Point3(-115mm, 60mm, 0mm)
        }
    }
}
```

Put the unmodified CTC-01 STEP beside this example as `ctc01.stp`, or use the
repository fixture's relative path. Prefer `aetheris lock revision.firmament` to
generate a dependency receipt, then omit the source hash field. For a source-only
specimen, choose exactly one explicit pin:

- `sha256` checks exact file bytes. Obtain it with PowerShell
  `Get-FileHash file.stp -Algorithm SHA256`.
- `textSha256` checks decoded source text encoded as UTF-8 with CRLF/CR converted
  to LF. No other whitespace or characters are changed. The fixture uses this
  explicit mode because Git stores the corpus as LF while older Windows worktrees
  can contain CRLF. Import still consumes the original text; no file is rewritten.

There is no fallback between these modes. Build JSON records the chosen hash kind
and actual input byte hash (UTF-8 resource hash for browser snapshots). Re-inspect
entity selectors before intentionally updating a pin after an upstream revision.

## Discover, name and plan a revision

Agents do not need to author BRep ordinals or guess edit bounds. Start with the
actual imported geometry:

```shell
aetheris imported-features inspect ctc01.stp --import-tolerance .01 --json
```

The report is explicitly `InspectionOnly`. It returns measured cylindrical groups,
original STEP wall entities, axis, diameter, and, when proved, through-depth,
entry/exit faces and center. `Recognized` means the bounded through-hole proof
passed. `CanPlan` additionally requires import qualification; it does **not**
certify a new destination. Unsupported groups carry rejection reasons. Strict
.001 mm inspection of CTC-01 still reports measurements but blocks planning.
No original design history or manufacturing process is inferred.

Select one candidate using these facts, then pass its exact ID. For the pinned
CTC-01 source, the upper-left diameter-25 hole has this ID:

```shell
aetheris imported-features plan ctc01.stp --candidate "through-hole:step-764+765" --as MountHole --offset 290 0 --geometry-only --import-tolerance .01 --source-out artifacts/local/imported-features/revision.firmament --json
aetheris validate artifacts/local/imported-features/revision.firmament --json
aetheris build artifacts/local/imported-features/revision.firmament --output artifacts/local/imported-features/revised.step --json
```

`--offset dx dy` uses imported-body XY coordinates from the proved upper opening;
`--to x y z` supplies an absolute Point3 instead. All numbers are millimetres.
Exactly one placement mode is required. `--clearance` defaults to .1 mm and must
exceed import uncertainty. The plan qualifies its generated source through normal
build admission before committing outputs; a refused plan leaves no new source file.

`--as MountHole` names the existing `Region` and replacement feature. There is no
additional Firmament `as` grammar. The emitted declaration is:

```firmament
Recognize Source {
    Region MountHole {
        kind: HoleShaft
        candidate: "through-hole:step-764+765"
    }
}
```

`candidate` is mutually exclusive with `faces` and `stepFaceEntities`. Candidate
IDs are stable within the pinned source, not across arbitrary STEP re-exports or
entity renumbering. A missing or unsupported candidate rejects; the compiler never
chooses a nearby hole. CLI planning writes `firmament.lock.toml` alongside the
hash-free revision source, and qualifies that saved source through the normal
compiler before reporting success. Previous source/lock files are restored on a
qualification failure. An existing receipt for another root rejects. Re-inspect
and review a changed STEP in a new revision/project; a lock update cannot rebind
surgery selections. The SDK source-only `PlanMove` result retains its explicit
`textSha256` pin for compatibility and immutable resource planning.

Generated geometry dimensions and coordinates use a **0.1 mm authoring grid**:
nearest tenth for nominal lengths and positions, midpoint ties away from zero.
Safety bounds and radial padding round outward. For example, imported arithmetic
noise `24.9999999999998mm` becomes authored `25mm`, and `45.0000000000001mm`
becomes `45mm`. This changes the authored nominal geometry and replacement PMI,
not merely their display formatting. The JSON plan records
`authoringIncrementMillimetres: 0.1`.

The numerical `importTolerance` budget keeps its own precision (`0.01mm` in this
fixture); rounding it to zero or widening it would change import admission.
The emitted, rounded source must still build. Genuine resizing or moving off the
proved support plane remains subject to the current capability checks. The grid
does not invent a manufacturing tolerance annotation or modify retained STEP
geometry. Explicit `tol` requirements remain the source of manufacturing limits.

The destination is a named `Concept Struct` Point3; destination bounds reference
its `.X` and `.Y` components. Editing that one point moves the requested hole and
reruns clearance checks without duplicating coordinates. The executable
[recovered fixture](../../../fixtures/Regression/InlineStep/production/ctc01-recovered-hole-move.firmament)
moves the hole across the central boss to (130,45) mm without editing the boss.

The .NET SDK exposes the same path through `FirmamentImportedFeatures.Inspect`
and `PlanMove`. Snapshot planning uses the immutable inspected resource text;
file-backed planning rechecks the pin through the ordinary loader. Explicit
geometry-only approval remains required in both paths.

## What the fields mean

`stepFaceEntities` selects original STEP entity IDs. `faces` is the historical
sequential face-number alternative; the two number spaces are not interchangeable.
Select the complete cylindrical wall group, including legitimate split half-walls.
Recognition proves that the group has complete circular openings on two parallel
planar supports and is an inward through-void. An authored kind, confidence or
evidence label cannot override failed geometry checks.

`on` selects one of those proved supports. In the pinned CTC-01 source, sequential
face 98 is the plane at Z=0. `center` is a Point3 in imported-body coordinates,
which coincide with Model world coordinates in this single-body workflow. It
must lie on the selected plane. Imported UV origins are not inferred from a Point2.

`within` authorizes the local edit volume. One bounds object must contain both
holes with positive clearance and span the support planes. Alternatively,
`within: [sourceBounds, destinationBounds]` takes exactly two bounds objects,
each `{ min: Point3(...); max: Point3(...) }`. They authorize suppression and
reinsertion separately; no swept movement corridor is edited. Separate regions
currently require provable line/circle support trim boundaries, and the
destination must be inside both material domains, excluding other openings.
An exterior destination cannot manufacture stock. The compiler proves
that no other boundary can interfere. A conservative refusal means the current
planner cannot prove that envelope safe; it is not necessarily a physically
impossible edit. Support faces can extend outside the box: their support surfaces,
outer loops and all unrelated inner loops remain intact.

`importTolerance` is a numeric import/trim qualification budget in mm, **not a
manufacturing tolerance or a `tol` requirement**. It defaults to 0.001 mm. CTC-01's
original polynomial intersection curves deviate about 0.0034 mm from their analytic
supports and the file declares 0.01 mm source accuracy. The example explicitly
admits that accuracy; strict 0.001 mm admission rejects. Source geometry is not
fitted or moved to hide that discrepancy. Export carries declared distance
accuracy rather than claiming native precision for retained imported geometry.

## Imported annotations

`geometryOnly: true` explicitly excludes the source annotation layer. It does not
modify the original STEP. The compiler emits a warning and reports the choice.
This first bounded revision workflow requires explicit geometry-only import for
every pinned source. Generic source PMI migration is not admitted, including
annotation graphs that the bounded inspector does not recognize. Without explicit
exclusion the edit rejects; it never silently discards imported requirements.

The new named Hole produces semantic diameter/depth PMI associated with its new
wall faces. Additional supported Firmament PMI can be authored against the revised
bindings. Importing geometry only does not certify, translate or preserve the
vendor's original dimensions, datums or GD&T.

## Admission and reports

The current family is one isolated round Z-axis through-hole, unchanged diameter
and depth, between two parallel planar supports in one qualified closed oriented
body. Blind, intersecting, breakout, threaded, blended-entry, counterbore,
countersink and non-Z-axis revisions reject. Unknown fields and placements reject.
No arbitrary-body Boolean fallback or general healing is performed. An unedited
pinned body can be re-exported through the qualified import owner; a pin does not
imply a replacement operation. Additional Modify operations still reject rather
than bypassing revision admission.

The operation suppresses the old wall/openings, materializes fresh analytic hole
geometry and reinserts it with fresh topology IDs. Unchanged topology keeps its
identity; a face correspondence map records the revision. Cached construction
geometry is invalidated. Required trim, boundary and STEP roundtrip checks must
pass before success. `validate` and `build` use the same admission path.

CLI build JSON exposes `importedHoleRevision`, including strategy, import accuracy,
annotation exclusion, retired/new walls, preserved entity counts and correspondence.
`inlineStepMigration.residualSurgery` is true for this operation. Syntax diagnostics
and kernel admission failures retain source context.

## Historical compatibility

The older HostSize-based Box/shaft replacement prototype remains a compatibility
route, reporting `holeShaft-bounded-rebuild` and `residualSurgery: false`. It does
not provide this imported-body preservation contract. Its historical admission
limitations are recorded in the [audit](../../release/HOLE-RECOGNIZE-REPLACE-AUDIT-2026-10-20.md).
Use the pinned `within` form for the qualified production operation.

See the [production closeout](../../release/IMPORTED-HOLE-REVISION-PRODUCTION-2026-10-20.md)
for test evidence and the [design contract](../../development/architecture/imported-hole-revisions.md)
for ownership and limits.

See the [recovery-assistance qualification](../../release/IMPORTED-FEATURE-ASSISTANCE-2026-10-21.md)
for the cross-boss revision and admission evidence.
