# AIR-FIRMAMENT-A1 — Firmament fixture corpus and lowering-stage taxonomy

## Purpose

AIR-FIRMAMENT-A1 formalizes Firmament as Aetheris' source-language corpus. `.firmfixture` files are compiler-style fixtures: they record authoring intent, expected validity, expected implementation state, expected lowering stage, and stable diagnostics before geometry support is broadened.

## Compiler-style pipeline

```text
Firmament source fixture
  -> parse
  -> semantic intent
  -> Feature AIR
  -> Constructive / Compositional AIR
  -> BRepPlan
  -> BRep
  -> STEP / artifacts
  -> trace / diagnostics
  -> regression corpus
```

Firmament is source/authoring intent. AIR is the compiler IR layer. BRepPlan/BRep are topology/materialization backends. STEP/artifacts are compiler outputs. CIR remains an analysis/evaluation mirror only and is not topology authority.

## Fixture directory taxonomy

The corpus lives under `fixtures/Firmament/` with broad feature categories:

- `Primitive/`
- `Profile/`
- `Prism/`
- `Boolean/`
- `Region/`
- `Chamfer/`
- `Fillet/`
- `Surfacing/`
- `Shell/`
- `Pattern/`
- `Material/`
- `Invalid/` is reserved for cross-category invalid language fixtures when a category-local home is not clearer.

A1 uses this subdirectory convention inside each category:

- `valid/` — valid Firmament that is currently implemented and should reach its expected real stage.
- `future/` — valid Firmament/design intent whose lowering or materialization is explicitly not implemented or deferred.
- `invalid/` — invalid Firmament that should reject with stable diagnostics.

## Metadata contract

Each `.firmfixture` in the A1 batch carries leading `// key: value` metadata. Required A1 metadata fields are:

```text
// fixture-id:
// case:
// category:
// validity: valid | invalid
// implementation: implemented | not-implemented | deferred | rejected
// expected:
// expected-stage:
```

Common optional metadata keys include:

```text
// expected-diagnostic:
// expected-feature-air:
// expected-constructive-air:
// expected-brep-plan:
// expected-brep:
// expected-step-smoke:
// expected-artifact:
// expected-blocker-category:
// notes:
```

`case` remains for compatibility with the trace fixture loader. `validity` is source-language validity, while `implementation` records whether Aetheris currently lowers/materializes that valid intent.

## Stage taxonomy

A1 documents this stable lowering-stage vocabulary:

- `source-only`
- `parsed`
- `semantic-intent`
- `feature-air`
- `constructive-air`
- `brep-plan`
- `emitted-brep`
- `step-smoke`
- `artifact-emitted`
- `region-parent-integrated`
- `region-rejected`
- `rejected`
- `not-implemented`
- `deferred`

Existing region fixtures retain their established region-specific stages (`region-parent-integrated`, `region-rejected`). Existing parser-backed box fixtures report `emitted-brep` through the parser/profile-emission trace path.

## Valid vs. invalid vs. not implemented

A valid but unimplemented Firmament fixture is not syntax-invalid. It should be represented as:

```text
// validity: valid
// implementation: not-implemented
// expected-stage: not-implemented
// expected-diagnostic: firmament-feature-not-implemented
```

A1 adds a metadata-only trace classification path for future fixtures. That path reports deliberate `not-implemented`, `deferred`, or `rejected` outcomes without pretending geometry exists and without invoking broad new production routes.

## Broad feature categories

A1 starts coverage across primitives, profiles/prisms, booleans, regions, chamfers, fillets, surfacing, shell, patterns, and materials. Future features should be designed by writing intended Firmament first, marking it `not-implemented` or `deferred`, then designing Feature AIR, Constructive/Compositional AIR, BRepPlan/BRep, and STEP/artifact support in later milestones.

## Current A1 fixture batch summary

The corpus now includes 27 `.firmfixture` files:

- implemented valid fixtures: parser-backed box, top-face-loop chamfer, controlled side-hole FaceAttachedRegion;
- future/not-implemented valid fixtures: cylinder, profile/prism sketches, boolean subtract intent, face-attached pocket/boss, single-edge chamfer, single-edge fillet, ruled surfacing, shell, linear pattern, and material assignment;
- invalid/rejected fixtures: primitive missing radius, negative dimension, ambiguous boolean target, implicit parent mutation, unsupported chamfer/fillet/surfacing/shell/pattern/material cases.

The side-hole golden path remains part of the language-level corpus at `fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture`. Its generated-on-demand artifact path remains controlled fixture evidence, not general side-hole support.

## Tests run

A1 adds `FirmamentFixtureCorpusTests` in `Aetheris.CLI.Tests`, covering metadata recognition, implemented stage reachability, not-implemented reporting, invalid diagnostics, side-hole golden-path corpus status, parser-backed box behavior, implementation independence, and deterministic corpus discovery/reporting.

## Next milestone recommendation

Next milestones should pick one narrow future fixture and lower it part-by-part: source parse, semantic intent, Feature AIR, Constructive/Compositional AIR, BRepPlan/BRep evidence, STEP/artifacts, and stable trace diagnostics. Do not admit broad shell, fillet, surfacing, Boolean, arbitrary side-hole, arbitrary face/axis, or CIR topology authority behavior through the corpus metadata path.
