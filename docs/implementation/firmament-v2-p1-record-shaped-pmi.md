# Firmament V2 P1 record-shaped PMI

P1 implements an authoring-oriented `pmi { ... }` block for Firmament V2 manufacturing intent. The syntax is record-shaped and data-only; symbolic GD&T/GPS frames remain lowering/export targets rather than the primary authoring UX.

## Supported records

- `datum <label>` with required `target`.
- `diameter <name>` with required `target` and `dimension`, or explicit `value` plus `tolerance`.
- `distance <name>` with required `targetA`, `targetB`, and `dimension`.
- `flatness <name>` with required `target` and length `tolerance`.
- `parallel`, `perpendicular`, and `coplanar` relation records with required `target`, `datum`, and length `tolerance`.

## Tolerance requirement

PMI dimensions must carry tolerance evidence. `diameter` and `distance` records accept a `dimension:` reference only when the referenced scalar or dotted record field is a `length` with tolerance metadata. A tolerance-free `length` produces `firmament-v2-pmi-dimension-missing-tolerance`.

## Datum and target behavior

Datum labels are unique within the P1 PMI block. Relation records resolve `datum:` values against previously declared datum labels. Targets preserve structured expressions such as `part.region("baseFace")`, `importedPart.region("mountHoleA")`, and `importedPart.face("#304")`; recognized-region validation is applied when recognition metadata is available, otherwise P1 preserves the target for later build/export context.

## AP242 lowering matrix

| PMI record | P1 parser/binder | Existing AP242 lowering |
| --- | --- | --- |
| `datum` | Supported | Supported through existing semantic datum PMI path when target resolves |
| `diameter` | Supported with required tolerance | Supported through existing semantic diameter PMI path when target resolves |
| `distance` | Supported | Export deferred |
| `flatness` | Supported | Export deferred |
| `parallel` | Supported | Export deferred |
| `perpendicular` | Supported | Export deferred |
| `coplanar` | Supported | Export deferred |

Deferred controls are preserved in the bound PMI block for reporting/export work; P1 does not silently reinterpret them as graphical PMI.

## Diagnostics

P1 adds deterministic `firmament-v2-pmi-*` diagnostics for duplicate blocks/records/datums, unknown record kinds, missing/unknown/duplicate fields, invalid targets, unknown datums, dimension type mismatch, missing dimension tolerance, tolerance type mismatch, and unsupported PMI syntax.

## Non-scope

P1 does not implement graphical PMI, drawing views, full GD&T/Y14.5, new modeling behavior, STEP geometry changes, or automatic feature reconstruction.
