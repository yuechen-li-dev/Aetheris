# Semantic PMI and AP242

Firmament concepts and explicit PMI lower into the semantic engineering model, STEP AP242 product-definition entities, and Cadmata's semantic presentation. PMI records are measurable requirements; annotations are engineer-authored notes. Camera or label orientation is presentation state, not the requirement.

Preview 3's native Model export supports `Datum` plane records and toleranced `HoleDiameter` records. The diameter record targets a named shaft hole; on a counterbore it means the shaft `Diameter`, not `CounterboreDiameter`. Other parsed PMI kinds may be reported as deferred and cannot be silently omitted by a successful build.

For a simple `Hole<Shaft>` constructed in a Box, `face(H.Wall)` names the stable wall output of Hole `H`. It is a semantic role selector, independent of the current BRep face number. `HoleDiameter` accepts it as a target:

```firmament
Model WallReference {
    Units: mm
    Box Body { Size: [40mm, 30mm, 8mm] }
    Modify Body { Hole<Shaft> H { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll } }
    Pmi { HoleDiameter WallDiameter { Target: face(H.Wall) Value: 8mm } }
}
```

The complete file is [`hole-wall-selector.firmament`](../../../fixtures/Canonical/PMI/hole-wall-selector.firmament). The compiler binds `H` as a Hole symbol, then resolves `Wall` through the construction-owned Hole-wall correspondence. Renaming `H` changes the selector spelling; diameter and position edits preserve it. Deleting `H` gives a Hole-specific unresolved-symbol diagnostic. Box `face(+Z)`, imported `face(#id)`, and existing PMI targets retain their meanings. `face(H.Wall)` is currently qualified for shaft walls in direct Box/Hole construction; it does not name blind bottoms, entry/exit rims, counterbore sections, patterned instances, or imported Hole-like faces. A planar `Datum` cannot consume a cylindrical Hole wall.

The complete qualified example is [`multiple-hole-dimensions-with-chamfer.firmament`](../../../fixtures/Canonical/PMI/multiple-hole-dimensions-with-chamfer.firmament):

```powershell
aetheris validate fixtures/Canonical/PMI/multiple-hole-dimensions-with-chamfer.firmament --json
aetheris build fixtures/Canonical/PMI/multiple-hole-dimensions-with-chamfer.firmament --output artifacts/pmi.step --json
aetheris analyze artifacts/pmi.step --json
```

The build's `pmiExportEvidence` and the analyzer's `semanticPmi` are independent public evidence surfaces. Supported validate records and inspected AP242 records must agree at the semantic-record level; one record may lower to several STEP entities.

Pattern-generated geometry may coexist with PMI, but Preview 3 does not publish a stable selector for individual generated pattern instances or quantity/repeated-feature PMI authoring.
