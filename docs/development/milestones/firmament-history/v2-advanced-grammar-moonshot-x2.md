# Firmament V2 advanced grammar — X2 progress

## Canonical root and adapter boundary

Firmament V2 admits canonical advanced documents through one root:

```firmament
Model Name {
    Units: mm
    // Concept, Struct, Construction Plane, Profile, Compose, Selection
}
```

`FirmamentV2Parser` owns root recognition, unit validation, parse disposition,
and the normalized V2 document. Existing Concept IR, profile, and section-stack
readers are semantic adapters behind that boundary. Build extracts a body only
after canonical admission; it does not use the first source keyword to choose a
user-visible dialect.

## Normalized declarations

`FirmamentV2Document` now carries parser-owned `Profiles`, `Composes`, and
`Selections` alongside existing primitive, Modify, Concept IR, template, and
static value fields. Advanced material records are deliberately distinct from
primitive records so primitive lowering cannot accidentally treat a profile or
section stack as a box.

## Current semantic policy

- The canonical root is PascalCase and requires `Units: mm`.
- Ordinary dimensions use `mm`; angles use `deg`; ordinary feature points use
  `Point2(xmm, ymm)`.
- `Concept Struct` supplies immutable compile-time geometry. `Struct` supplies
  material construction. `Construction Plane` traces Concept-plane provenance.
- Profile and Compose preserve their resolved local frame, profile identities,
  operation roles, intervals, and selection declarations through the adapter.
- Compatibility inputs remain accepted at the boundary while their migration is
  completed.

## Verified corpus

`profile-line-extrusion.firmament` and `profile-compose-base.firmament` build,
export STEP, and reimport as enclosed one-body solids from their canonical
roots. Construction-plane holes and static `Match` diagnostics are covered by
V2 parser tests.

## Remaining work before Preview 1

This is not yet the complete moonshot. Slot/Selection consumers, Template
syntax, typed profile guide literals, record/array syntax, and complete
duplicate-name resolution still retain compatibility-era implementation seams.
They need to be lowered into the same document model rather than represented
only as adapter metadata. No claim of full dialect retirement should be made
until those routes, their invalid fixtures, and all end-to-end exports are
covered.
