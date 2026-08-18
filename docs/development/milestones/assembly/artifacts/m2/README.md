# Assembly M2 validation artifacts

These files are persisted evidence for the bounded Assembly M2 milestone. They are generated through the public CLI and compiled through the same parser, binder, assembly compiler, exact geometry materializer, and AP242 paths used by applications.

## Historical OCCT proof

The source fixture is `testdata/step242/OCCT/as1.step`. Direct product-structure inspection finds 27 `NEXT_ASSEMBLY_USAGE_OCCURRENCE` relationships, five exact geometric definitions, 18 geometric leaf occurrences, and a maximum hierarchy depth of three. Aetheris preserves the supplied hierarchy; it does not infer hierarchy from geometry or names.

- `occt-package/as1.firmasm` is editable current Firmament V2 Assembly source.
- `occt-package/components/*.step` contains one exact STEP resource per shared definition.
- `occt-package/component-package.json` records deterministic component hashes and normalization policy.
- `occt-imported-assembly-ir.json` is the compiled package inspection report.
- `occt-as1-ap242.step` is the native AP242 product-structure export.
- `template-block-pair-ap242.step` proves native Firmament assembly export independently of imported placement evidence.
- `cadmata-display-packet-summary.json` records the current hierarchical OCCT display-packet contract without checking in the approximately 10.4 MB mesh payload.
- `cadmata-occt-assembly.png` and `cadmata-occt-occurrence-selected.png` are rendered historical import/autoframe and occurrence-specific selection evidence.
- `cadmata-template-mate-assembly.png` is rendered native Mate-derived placement, Mate residual, and tolerance-stackup evidence.

## Placement authority audit

Imported OCCT occurrences are labeled `ImportedOccurrence`. Historical JSON-shaped `.firmasm` transforms migrate as `LegacyExplicit`. Native Mate-constrained source remains `MateDerived`. All lower to physical rigid occurrence transforms, but none is relabeled as engineering intent it did not contain. The generated OCCT source therefore has no invented Mates.

## Geometry and multiplicity audit

| Definition | Exact faces | Local bounds min | Local bounds max |
| --- | ---: | --- | --- |
| bolt | 7 | `(-7.5, -1e-15, 0)` | `(7.5, 1e-15, 37)` |
| l-bracket | 16 | `(0, 0, 0)` | `(50, 60, 100)` |
| nut | 8 | `(0, 0, 0)` | `(20, 15, 3)` |
| plate | 18 | `(0, 0, 0)` | `(180, 150, 20)` |
| rod | 4 | `(-5, -1e-15, 0)` | `(5, 0, 200)` |

Geometry is mapped from each definition's specific rigid representation root. This prevents unrelated AP242 roots from contaminating a definition. Definitions are materialized and tessellated once; 18 leaf occurrences reference the five definitions with world transforms.

## Bounded AP242 audit

The export contains product definitions, exact shape representations, occurrence relationships, item-defined rigid transformations, representation relationships with transformations, and context-dependent shape representations. Nested hierarchy, definition reuse, names, rotations, and translations round-trip. Interface, Mate, tolerance, and placement-authority semantics remain authoritative in Firmament and are not claimed as native AP242 semantics.

When input contains multiple independent geometric roots without trustworthy AP242 occurrence hierarchy, the importer emits a diagnostic and normalizes them to a flat Assembly. A single independent body stays on the ordinary part path.

## Determinism

SHA-256 values for this checked-in run:

| Artifact | SHA-256 |
| --- | --- |
| `occt-as1-ap242.step` | `6CE373F1060203D277C20D9E1DDC8AE3916860B8A7EBDCA4C93690B6814A79D1` |
| `occt-imported-assembly-ir.json` | `D4B426712F01A542851186F11CEA8EBFF756D7403651190672F6F36053F66969` |
| `occt-package/as1.firmasm` | `54143773C45A03EF73F183DAD9751EBC50DC07FE6BD6447DC100BFFECFFEFB7E` |
| `occt-package/component-package.json` | `AC929996E8E7A1F6CFE179713962A26A3C48D78FD9044EF6DAD2D8341FE5F5CA` |
| `template-block-pair-ap242.step` | `8068F0EA553F2479FE839CDB94EF8E49D8F90795EECFCBFC7B55F25F025C1238` |

The package manifest uses a source filename rather than a machine-specific absolute path. Component hashes are recorded in the manifest. Tests compare stable structure, definition reuse, and representative transforms after export/import; CLI output also reports generated hashes.

## Performance observation

The persisted historical compile report measured 54.97 ms parse, 5.61 ms bind, 0.75 ms Mate validation, 4.64 ms placement, 1.65 ms dimensional graph, 1.20 ms tolerance analysis, 108.15 ms definition materialization, and 43.61 ms geometry execution on the recording machine. These are diagnostic observations, not cross-machine performance guarantees.

The timed CLI workflow measured 263.47 ms for historical multipart import plus component-package emission, 299.03 ms for native AP242 export, and 248.39 ms for AP242 reimport plus package emission. The current hierarchical OCCT Cadmata packet measured 208.95 ms internally (580.94 ms observed HTTP round-trip) and 10,379,199 response bytes. These are single-run diagnostic observations on the recording machine.

Cadmata's current OCCT server packet is verified to contain five reusable mesh definitions and 28 product-tree nodes: one root, nine nested assembly occurrences, and 18 geometric Part occurrences. The older flat legacy JSON compatibility path has one root plus 18 Parts. Aggregate world bounds are `(-10, 0, -4)` to `(190, 150, 80)`. The rendered browser session confirmed aggregate autoframing, nested product-tree structure, occurrence-specific tree/viewport selection, the `Imported occurrence` label, and no invented Mates. The native Template block pair session confirmed two transformed definitions, selection-specific highlight, `Derived from Mate(s)`, a valid `SeatedAxis` Mate residual, and a passing `TemplateFitTransition` stackup. Its repaired base-to-top seating has zero-volume contact and no solid overlap. Reverting it to the former top-to-top seating now fails before Cadmata with `assembly-solid-volume-interference`. No browser console errors occurred; the only warning was Three.js's upstream `Clock` deprecation notice.
