# Aetheris 2.0.0-preview.3 release notes

Preview 3 is the feature-frozen Windows x64 release of Aetheris's integrated semantic CAD workflow.

## Firmament V2

- Semantic CAD authoring with typed Records, Templates, Static specialization, finite tables and arrays, `with`, `Require`, named concepts, and explicit engineering references.
- Stable CLI validation/build diagnostics: admitted intent is lowered and unsupported intent is rejected instead of silently omitted.

## Geometry

- Bounded analytic and prismatic modeling: named primitives and profiles, semantic holes, slots and patterns, admitted chamfer/fillet routes, hollow/lattice routes, and deterministic AP242 output.
- First-class connected finite Boss and finite-depth Pocket operations on the documented Compose host, with explicit through-depth and minimum-floor rejection.
- Parser-backed Sphere, Cone, and Torus single-solid AP242 routes. This is not general freeform or Boolean authoring.

## Sheet Metal

- Authored base, flange/bend, and planar-opening workflows with material identity, K-factor, semantic regions, DFM evidence, formed STEP, flat STEP, and SVG.
- Bounded imported reconstruction remains experimental and reports partial status rather than inventing authoritative intent.

## STEP AP242 and PMI

- Deterministic single-body import/export on the qualified classes.
- Semantic datums, toleranced diameters, manufacturing dimensions, position controls, engineering annotations, and qualified geometry associations in AP242 workflows.
- Unsupported PMI families remain explicit in diagnostics and the support matrix.

## Cadmata

- Interactive geometry inspection, model switching, selection, PMI filtering, datum/feature interaction, geometry-to-PMI discovery, and presentation-only callout movement.
- Production hosting is included in the Windows bundle; the standalone CLI tool does not include Cadmata.

## Materials

- Four deployed Standard Library catalog entries with typed property provenance.
- Material resolution is qualified for authored Sheet Metal and Firmament FEA. Ordinary prismatic CAD STEP does not persist a general solid-material designation.

## FEA

- Bounded linear-elastic isotropic analysis over native Firmament geometry and qualified `inlineSTEP` single bodies.
- Fixed component constraints, total-resultant Force, displacement/strain/stress/reaction results, cut-cell/vector-lattice lowering, and structured convergence/equilibrium evidence.
- The public A36 cantilever reports approximately `25.06 µm` maximum displacement versus approximately `24.7 µm` from simple beam theory. This is a narrow sanity witness, not general mechanics validation.

## Forge interoperability

- Forge Host Protocol v1 list/describe/invoke with structured diagnostics and deterministic file artifacts.
- The release ships a NativeAOT `win-x64` host and small qualified Python, Go, Rust, and TypeScript clients.
- Protocol version `1` is independent of Aetheris version `2.0.0-preview.3`.

## Documentation and packaging

- Publication-ready public guides and qualified examples are included in the Windows ZIP alongside licenses, third-party notices, material data, Cadmata, and Forge clients.
- Sixteen version-aligned public library packages, the CLI tool package, and the independently versioned `0.3.0-preview.3` VSIX are staged as release assets.
- Release archive and NuGet normalization provides a reproducible generation path and canonical SHA-256 inventory.

## Known limitations

- Only `win-x64` release binaries are qualified.
- Geometry is bounded: no general loft, helix, freeform feature family, or arbitrary public solid Boolean authoring.
- Imported STEP containment, source-unit preservation, Sheet Metal opening families, material persistence, and the generic tessellated mass estimate have the boundaries described in [known issues](known-issues.md).
- Sheet Metal and FEA cover the documented manufacturing and linear-elastic subsets, not general formed-feature or physics systems.

See the [supported-features matrix](supported-features.md) for the precise public contract.
