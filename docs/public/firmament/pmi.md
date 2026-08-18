# Semantic PMI and AP242

Firmament concepts and explicit PMI lower into the semantic engineering model, STEP AP242 product-definition entities, and Cadmata's semantic presentation. PMI records are measurable requirements; annotations are engineer-authored notes. Camera or label orientation is presentation state, not the requirement.

Preview 3's native Model export supports `Datum` plane records and toleranced `HoleDiameter` records. The diameter record targets a named shaft hole; on a counterbore it means the shaft `Diameter`, not `CounterboreDiameter`. Other parsed PMI kinds may be reported as deferred and cannot be silently omitted by a successful build.

The complete qualified example is [`box-holes-pmi-chamfer.firmament`](../../../fixtures/Canonical/valid/box-holes-pmi-chamfer.firmament):

```powershell
aetheris validate fixtures/Canonical/valid/box-holes-pmi-chamfer.firmament --json
aetheris build fixtures/Canonical/valid/box-holes-pmi-chamfer.firmament --output artifacts/pmi.step --json
aetheris analyze artifacts/pmi.step --json
```

The build's `pmiExportEvidence` and the analyzer's `semanticPmi` are independent public evidence surfaces. Supported validate records and inspected AP242 records must agree at the semantic-record level; one record may lower to several STEP entities.

Pattern-generated geometry may coexist with PMI, but Preview 3 does not publish a stable selector for individual generated pattern instances or quantity/repeated-feature PMI authoring.
