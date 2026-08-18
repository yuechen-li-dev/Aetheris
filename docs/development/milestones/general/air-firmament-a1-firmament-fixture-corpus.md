# AIR-FIRMAMENT-A1 — Firmament fixture corpus and lowering-stage taxonomy

AIR-FIRMAMENT-A2.3 extends the V2 metadata-only design fixture taxonomy with `fixtures/Templates/` for `template<Process>`/`concept` doctrine and `fixtures/PMI/` for product/manufacturing information examples. These fixtures remain parser-not-ready or metadata-rejected contracts and must not be treated as V1 parse failures.

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

The corpus lives under `fixtures/` with broad feature categories:

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

The side-hole golden path remains part of the language-level corpus at `fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture`. Its generated-on-demand artifact path remains controlled fixture evidence, not general side-hole support.

## Tests run

A1 adds `FirmamentFixtureCorpusTests` in `Aetheris.CLI.Tests`, covering metadata recognition, implemented stage reachability, not-implemented reporting, invalid diagnostics, side-hole golden-path corpus status, parser-backed box behavior, implementation independence, and deterministic corpus discovery/reporting.

## Next milestone recommendation

Next milestones should pick one narrow future fixture and lower it part-by-part: source parse, semantic intent, Feature AIR, Constructive/Compositional AIR, BRepPlan/BRep evidence, STEP/artifacts, and stable trace diagnostics. Do not admit broad shell, fillet, surfacing, Boolean, arbitrary side-hole, arbitrary face/axis, or CIR topology authority behavior through the corpus metadata path.

## AIR-FIRMAMENT-A2 — Firmament V2 design-fixture policy

Firmament V2 is the canonical future human-facing source language. Firmament V1 remains valid where already supported, but it is legacy TOON/YAML structured syntax, mostly frozen, and primarily useful for the existing corpus, interchange, and historical regression evidence.

A2 introduces metadata-only V2 design fixtures under `fixtures/`. These fixtures use record/block-style construction-intent snippets and declare `syntax-version: FirmamentV2`. AIR-FIRMAMENT-X1 promotes only `fixtures/Primitive/valid/box-v2.valid.firmfixture` and its small invalid primitive pilots to parser-backed V2 fixtures. The rest remain deliberately classified by metadata as `not-implemented` or `rejected`; they must not be surfaced as random V1 parser failures.

The intended V2 fixture tree is:

```text
fixtures/
  Primitive/
  Profile/
  Prism/
  Region/
  Chamfer/
  Fillet/
  Shell/
  Surfacing/
  Surface/
  SemanticRefs/
  Admissibility/
  Pattern/
  Material/
  RecordDerivation/
  Invalid/
```

Future features should be written in V2 syntax first, marked not implemented/future-design, and then advanced through parser, semantic intent, Feature AIR, Constructive/Compositional AIR, BRepPlan, BRep, STEP/artifact, and trace stages in narrow milestones. X1 establishes the first such narrow promotion at Feature AIR for typed-record Box only. Existing V1 fixtures remain valid in `fixtures/` and do not require migration.

## A2.1 semantic-reference and admissibility fixture expansion

A2.1 expands the `fixtures/` metadata-only taxonomy with semantic-reference, admissibility, surface-doctrine, and pattern-as-record pilot fixtures. These fixtures remain `syntax-version: FirmamentV2` and are deliberately classified by metadata rather than by the V1 parser. Valid design examples use `implementation: not-implemented`, `expected-stage: not-implemented`, and `expected-diagnostic: firmament-v2-parser-not-ready`; invalid doctrine examples use `implementation: rejected` with stable diagnostics such as `firmament-raw-backend-id-reference-forbidden`, `firmament-degenerate-dimension`, and `firmament-shell-thickness-collapses-body`.

## A2.2 record-derivation fixture expansion

A2.2 expands the `fixtures/` metadata-only taxonomy with `RecordDerivation/` fixtures for valid `with` variants and invalid derivation diagnostics. Valid design examples remain `implementation: not-implemented`, `expected-stage: not-implemented`, and `expected-diagnostic: firmament-v2-parser-not-ready`; invalid doctrine examples use `implementation: rejected` with diagnostics such as `firmament-degenerate-dimension`, `firmament-with-requires-record`, and `firmament-with-field-not-found`. These fixtures are classified by metadata and must not be routed through the V1 parser as incidental parse failures.


### AIR-FIRMAMENT-X2 fixture stage update

`fixtures/RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture` is no longer metadata-only. It is parser-backed, reaches `feature-air`, lowers the derived solid `tall` to `CreateBox`, and retains `base` as source record evidence. The surrounding V2 design fixtures remain metadata-only, parser-not-ready, not-implemented, deferred, or rejected according to their existing fixture metadata.

## X3 fixture stage note

`fixtures/SemanticRefs/valid/named-box-faces-v2.valid.firmfixture` is parser-backed as of AIR-FIRMAMENT-X3 and is expected to reach `feature-air` while reporting four semantic exposure aliases.


## AIR-FIRMAMENT-X4 fixture promotion

`fixtures/Region/valid/side-hole-v2.valid.firmfixture` is parser-backed for the controlled +X to -X side-hole region and reaches `region-parent-integrated` through the existing AIR Region golden trace path.

## AIR-FIRMAMENT-X5 V2 side-hole artifact status

`fixtures/Region/valid/side-hole-v2.valid.firmfixture` now has a generated-on-demand artifact workflow. Running `aetheris trace --fixture fixtures/Region/valid/side-hole-v2.valid.firmfixture --out-dir artifacts/air-firmament-x5/side-hole-v2` writes `side-hole-v2.step`, `side-hole-v2.trace.json`, `side-hole-v2.trace.txt`, and `manifest.json`. This records parser-backed V2 parity with the controlled AIR-REGION-X13 side-hole path without broad corpus migration or general side-hole support.

## AIR-FIRMAMENT-X6 V2 side-hole radius variation status

The Firmament V2 region corpus now includes controlled valid side-hole radius variation fixtures for radius `0.5` and `1.5`, plus invalid radius fixtures for zero, negative, and clearance-exceeding radii. These fixtures remain parser-backed and bounded to the same `base` Box, `face(+X)` to `face(-X)`, single `cut Cylinder` side-hole path; they do not migrate the broad corpus or add general side-hole support.


## AIR-FIRMAMENT-X7 fixture taxonomy update

X7 adds controlled Firmament V2 Region side-hole center-offset fixtures under `fixtures/Region/valid` and matching clearance/arity invalid fixtures under `fixtures/Region/invalid`. These remain parser-backed controlled Region fixtures, not broad corpus migration or general side-hole support.


## AIR-FIRMAMENT-X9 fixture taxonomy note

The Firmament V2 region fixture set now includes controlled reverse-X side-hole valid fixtures (`side-hole-reverse-x-v2`, `side-hole-aliases-reverse-x-v2`) and invalid route fixtures for same-face, mixed-axis, Y-axis-not-yet-supported, and alias wrong-through cases.


X10 fixture taxonomy note: Firmament V2 Region valid fixtures now include controlled Y-axis side-hole route fixtures, and invalid fixtures include Z-axis, mixed-axis, Y-clearance-boundary, and alias wrong-through rejections.

## X11 fixture taxonomy note

AIR-FIRMAMENT-X11 adds parser-backed Firmament V2 Region fixtures for controlled Z-axis opposite-face side holes, plus invalid mixed-axis and Z-clearance boundary fixtures. This is a narrow Region taxonomy addition and not a broad corpus migration.
