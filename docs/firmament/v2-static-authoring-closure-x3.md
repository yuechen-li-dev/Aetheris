# Firmament V2 static-authoring closure — X3 status

This document is an evidence log, not a claim that every X3 objective is
complete. Firmament V2 now has one canonical document root for ordinary parts,
Concept/Struct work, Profile/Compose, semantic Slots, and source-grounded
Selections:

```firmament
Model Name {
    Units: mm
    // all currently admitted declarations
}
```

The parser owns root recognition, units, parse disposition, and a normalized
document. Profile and section-stack readers are bounded semantic adapters
behind that root; they do not decide the author-visible document dialect.

## Current normalized and binding evidence

- `FirmamentV2Document` carries Profile, Compose, Selection, static-authoring,
  and canonical-symbol evidence.
- Semantic Slot lowering remains the existing exact Profile lowering, with
  source-to-topology correspondence and stable Slot descendant roles.
- Canonical selection binding now diagnoses malformed declarations, duplicate
  selection names, invalid result roles, and unknown Profile/Hole/Slot sources.
- `Match`, bounded Concept `Pattern`, and the existing typed-template expansion
  remain compile-time only and preserve static selection/template provenance.
- Canonical `Record`, `Static T[]`, a typed one-parameter `Template`,
  `Pattern ... Over`, and scalar `Require` normalize to a static AST then erase
  before material lowering. Pattern output admits Shaft Holes and Capsule or
  RoundedRectangle Slots; a Profile template is a direct indexed invocation.
- The canonical symbol table has one namespace for Record, Static array,
  Template, Pattern, Require, Profile, Compose, Hole, Slot, and Selection.
  It rejects collisions across families and records the checked target of each
  Profile/Hole/Slot Selection source.
- Canonical Profile/Compose/Slot fixtures are parser-admitted then built through
  production materializers; no build route relies on the old top-level root.

## Deliberate bounds

This closure is intentionally static: templates take one typed record
parameter, direct invocation is an indexed static-array element, and Profile
templates are direct-only because a generated Profile requires a declared
identity. Runtime loops, dynamic collections, and generic cross-document
linking remain outside the canonical grammar.

## Verification

The canonical corpus currently includes Profile extrusion, Compose, Capsule
Slot, RoundedRectangle Slot, Selection-driven chamfer, record-array generated
holes and slots, and direct Profile-template fixtures. Focused parser tests
prove canonical-root admission, deterministic typed Selection diagnostics, and
cross-family binding. The full Firmament suite remains the release gate.
