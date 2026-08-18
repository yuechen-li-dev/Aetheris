# Assembly M2: `.firmasm`, AP242 product structure, and Cadmata

`.firmasm` is a supported Firmament V2 Assembly document profile. It is not a second language and it is not deprecated. `.firmament` is a general compilation unit that may contain assemblies; `.firmasm` uses the same parser, binder, Template system, `SemanticValue` model, and `AssemblyIR`, then requires exactly one exported/root Assembly product. Supporting declarations and external resources are allowed. No root and multiple roots are typed profile errors.

The old JSON-shaped syntax is `LegacyFirmasmManifest` behavior even where historical CLR names remain for compatibility. A `.firmasm` beginning with JSON is parsed only by the legacy migration input, converted to current Firmament V2 source, and compiled through the ordinary assembly pipeline. The extension remains current.

## Multipart STEP policy

STEP can be ambiguous between a multi-body part and an assembly. When incoming STEP does not provide trustworthy semantic evidence separating them, Aetheris normalizes multiplicity to an Assembly rather than preserving an ambiguous foreign ontology. One independent body may remain on the ordinary part path. More than one independent rigid product becomes a flat Assembly if AP242 supplies no trustworthy hierarchy. Explicit `NEXT_ASSEMBLY_USAGE_OCCURRENCE` hierarchy is preserved; geometry, proximity, names, and touching faces are never used to invent subassemblies.

The Aetheris multipart interchange round trip is:

```text
source.step -> assembly.firmasm + components/*.step -> equivalent AssemblyIR
```

The `.firmasm` is authoritative for Aetheris product structure and semantic authoring. Component STEP files are exact geometry resources emitted once per proven shared definition. Generated source is deliberately editable: users can rename occurrences, add semantics and Interfaces, or replace imported placement evidence with Mates.

The distinct native AP242 round trip is:

```text
AssemblyIR -> AP242 products/occurrences/transforms -> imported product structure
```

The bounded subset admits `PRODUCT`, `PRODUCT_DEFINITION_FORMATION`, `PRODUCT_DEFINITION`, `PRODUCT_DEFINITION_SHAPE`, `SHAPE_REPRESENTATION`, `SHAPE_DEFINITION_REPRESENTATION`, `NEXT_ASSEMBLY_USAGE_OCCURRENCE`, `ITEM_DEFINED_TRANSFORMATION`, `REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION`, and `CONTEXT_DEPENDENT_SHAPE_REPRESENTATION`. It preserves nested hierarchy, rigid occurrence transforms, names, and shared shape definitions. It does not encode Aetheris-only Interface/Mate/tolerance semantics.

## Placement authorities

- `MateDerived`: transform resolved from admitted Interface/Mate constraints. This is preferred native authoring.
- `ImportedOccurrence`: transform preserved from foreign AP242 product-structure evidence. It is legitimate interchange data, not inferred engineering intent.
- `LegacyExplicit`: transform migrated from historical JSON-shaped `.firmasm`. It is compatibility-only.

All three lower to physical AP242 occurrence transforms. Cadmata labels them distinctly.

## Physical interference gate

Assembly compilation performs a post-materialization physical-validity gate over resolved world BReps. The bounded exact lane proves intersection of closed convex planar solids from their oriented face half-spaces. A full-dimensional overlap is a fatal `assembly-solid-volume-interference` diagnostic, so Cadmata never becomes the first place that invalid native geometry is discovered. Zero-volume seating contact is admissible. AABB overlap alone never causes rejection, and unsupported curved/non-convex pairs are not classified as collisions without stronger evidence.

## Cadmata

Cadmata compiles `.firmasm` and assembly-bearing `.firmament` startup files into one assembly display packet. Mesh conversion is performed once per definition; occurrences reference a definition and carry a world transform. The viewport renders transformed instances, frames aggregate world bounds, and keeps occurrence-specific highlight state. The inspector shows the product tree separately from the Mate/Interface relationship table, placement authority, Mate participants/status, and tolerance stackup results.

Launch an assembly with the ordinary Cadmata host path or inspect backend evidence with:

```text
aetheris asm inspect assembly.firmasm --json --profile
aetheris asm import-step source.step --out package --json
aetheris asm export-ap242 package/source.firmasm --out assembly.step --json
```

The historical proof is `testdata/step242/OCCT/as1.step`: 27 AP242 occurrences, five shared exact geometry definitions, 18 geometric leaf occurrences, and hierarchy depth three. The persisted M2 artifacts are under `docs/development/milestones/assembly/artifacts/m2/`.
