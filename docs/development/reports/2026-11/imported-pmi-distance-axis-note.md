# Imported-part PMI: distances between faces, datum axes, notes, nominal check

Helios's Label tab (HeliosCAD `docs/development/reports/2026-11/label-tab-design.md`) can only offer what the compiler
writes for an imported STEP part. This closes the gaps that mattered most for annotating a vendor part.

## Changes

| Record | Before | Now |
| --- | --- | --- |
| `Distance N { between: [a, b] value tolerance }` | `Distance` parsed only as the legacy `targetA`/`targetB`/`dimension` form, and export deferred it | Two parallel planar faces of an `InlineStep` part. Material between them (outward normals apart): `DIMENSIONAL_SIZE` named `thickness` on both faces. Otherwise `DIMENSIONAL_LOCATION` (`linear distance`) from the first face to the second. Non-parallel or non-planar faces: `firmament-v2-imported-pmi-distance-faces`. |
| `Datum` on a cylindrical imported face | rejected (`imported-pmi-target-kind`) | a datum axis (`semantic datum axis`) |
| `Note` on an imported part | rejected: every non-datum record required a cylinder | admitted; a part note has no face |
| Nominal vs geometry | not checked | `pmi-nominal-outside-geometry` warning when an imported `Distance` or `HoleDiameter` band does not contain the measured B-rep value |
| Viewport projection | an authored distance spanning two planes threw `pmi-display-target-multiple-unsupported`, which dropped every annotation from the preview | drawn between the two planes, in a plane containing their normal, beside the part (`Layout.Between`) |

The legacy `Distance { targetA targetB dimension }` form is unchanged (still parsed, still not exported). `between` is
the exported form; the two are not merged in this change because the legacy form has parser tests and no exporter.

The AP242 parity check counts `Distance` among dimensions.

## Tests

`ImportedPartPmiTests`: axis datum; thickness exported as `DIMENSIONAL_SIZE 'thickness'`, read back with both faces,
projected for the viewport; the nominal warning for an out-of-band distance and diameter; refusal of non-parallel
faces. The case that expected a cylinder datum to be rejected now asserts the axis instead.

## Not done

- Angles between faces (`ANGULAR_LOCATION`), sizes on imported edges, `Location` from a datum to an imported hole.
- Location distances are drawn with the same between-planes callout; they are not boxed or merged with basic
  dimensions of position-toleranced holes.
