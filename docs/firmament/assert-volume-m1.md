# Assert Volume M1

`Assert Volume` is a source-level verification contract, not geometry, PMI, or
a calculator language.

```firmament
Assert Volume Body {
    Expected: 1587.982297150257mm^3
    Tolerance: 0.01mm^3
    Note: "Finite-span quarter-cylinder straight Profile edge fillet"
}
```

`Expected` and `Tolerance` are required finite literal `mm^3` quantities;
`Tolerance` is absolute and non-negative. `Note` is optional. The target must
bind to one canonical material-body symbol. Lengths, areas, angles, scalars,
unknown targets, duplicate/unknown fields, and malformed notes are rejected at
parse/bind time.

Builds materialize normally, reimport the just-produced STEP, and evaluate its
body through `BrepMassProperties.Evaluate`. The shared
`FirmamentV2VolumeAssertionComparer` records expected, measured, signed and
absolute deltas, tolerance, note, source provenance, method, and error bound.
It passes only when `abs(measured - expected) <= tolerance`; unavailable mass
properties are a distinct failure. Failed assertions fail `build`; `validate`
only performs source checks. `build --json` exposes successful records under
`assertions`. Artifact-only `verify` cannot recover source contracts from a
STEP file and deliberately does not infer them.

Assertions never affect materialization, feature routing, selections, topology,
or STEP/PMI export. The test suite verifies identical STEP text for equivalent
sources with and without an assertion.

M1 intentionally has no formulas, expressions, relative tolerance, Area,
Centroid, topology assertions, embedded C#, or C# execution.
