# Aetheris 2.0.0-preview.3 release notes

Preview 3 is a Windows x64 release candidate for integrated engineering workflows.

User-visible areas include:

- Firmament V2 authoring with typed Records, Templates, Static specialization, tables, semantic references, and explicit diagnostics.
- Bounded analytic and prismatic CAD, including first-class connected Boss and finite-depth Pocket semantics, semantic holes, admitted EdgeFinish routes, and deterministic STEP AP242 export.
- Bounded Sheet Metal base/flange/bend/opening workflows with formed STEP, flat STEP, SVG, material identity, bend identity, and DFM evidence.
- Semantic AP242 PMI for the qualified manufacturing families, including datums, dimensions, position controls, engineering annotations, and feature associations.
- Cadmata geometry inspection, selection, PMI filtering, datum/feature interaction, geometry-to-PMI discovery, and presentation-only callout dragging.
- A deployed Standard Library material catalog used by authored Sheet Metal and Firmament FEA.
- Bounded linear-elastic FEA over native Firmament geometry or qualified `inlineSTEP` bodies, with Fixed/Force conditions and typed displacement, strain, stress, and reaction-force results.
- Forge Host Protocol v1 discovery and deterministic invocation from foreign-language processes through the shipped NativeAOT host.
- A release ZIP containing the public documentation, executable examples, material catalog, CLI, Cadmata, Forge Host, and foreign-language clients.

Important boundaries:

- Only `win-x64` release binaries are qualified.
- Geometry support is bounded; there is no general loft, helix, freeform feature family, or arbitrary solid Boolean authoring.
- Ordinary prismatic CAD STEP does not persist a general solid-material designation; material identity is qualified on the Sheet Metal and FEA paths.
- Sheet Metal and FEA support the documented bounded manufacturing and linear-elastic classes, not general formed-feature or physics systems.
- Protocol version `1` is independent of Aetheris version `2.0.0-preview.3`.

See `known-issues.md` and `supported-features.md` for the precise release boundary.
