# X2 semantic migration witnesses

| Source | Intent and current expected outcome |
| --- | --- |
| `scoped-drill.firmament` | Accepted: Model-local Drill shadows document Drill; dependent defaults produce a 6 mm hole in 30 × 20 × 8 mm stock. |
| `pattern-symbol-collision.firmament` | Accepted: a binder and Hole named Diameter do not rewrite field labels. Two stable keyed holes have 6 mm diameter. |
| `sibling-feature.invalid.firmament` | Rejected: Other.Local is not visible to Plate. Validate/build fail; no STEP is emitted. |
| `template-shadowing.firmament` | Accepted: Model-local Family shadows document Family; the 30 × 20 × 6 mm product matches its concrete STEP equivalent. Promoted from the original pending specimen. |
| `sibling-template.invalid.firmament` | Rejected: Other.Family is invisible to Plate; both validate/build reject with the authored application location. |
| `record-shadowing.firmament` | Accepted: the outer and Model-local Spec schemas retain distinct nominal identities; the local product is 30 × 20 × 6 mm and matches concrete STEP. |
| `scoped-record-pmi.firmament` | Accepted: a local tolerance survives same-named outer schema/value declarations, Template and Feature binding into explicit AP242 PMI. |
| `scoped-table.firmament` | Accepted: local Table/schema/value names shadow outer declarations; a selected row produces 30 × 20 × 6 mm stock. |
| `sibling-data.invalid.firmament` | Rejected: the sibling Static secret is invisible; validate/build reject at its authored reference. |
| `panel-radians.firmament` | Existing radian-valued Panel arc admission retains exact sampled surface coordinates. Normal solid STEP export still reports its existing panel-only capability limitation. |
| `traceloop-rect2.firmament` | Accepted: original source formerly mislabeled as an invalid non-loop. A Rect2 is a closed boundary and TraceLoop is now supported. The Invalid/Pipeline witness now uses an actual open Line2. |
| `piping-field-casing.pending.firmament` | Known defect: validate/build accept, but inspection reports Generic for requested lowercase `connectionType: Weld`. Not qualified as correct behavior. |
| `piping-field-unknown.pending.firmament` | Known defect: an unknown optional Port field is ignored. It must reject after Piping's owning field reader migrates to AST consumption. |

The component tests compare accepted geometry to concrete/renamed equivalents,
check generated identities, and retain source-located private-name rejection explicitly.
The release report records real CLI outcomes and the complete corpus disposition.
These fixtures do not extend permitted Pattern nesting or geometry capabilities.

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/FrontendX2/scoped-drill.firmament --json --explain
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Regression/FrontendX2/pattern-symbol-collision.firmament --output artifacts/local/firmament-x2/mounting-plate.step --json
```
