# AIR-FIRMAMENT-X3 — V2 Semantic Refs and `=>` Exposure for Box

## Purpose and scope

AIR-FIRMAMENT-X3 adds the first parser-backed Firmament V2 semantic subobject reference slice. The implemented scope is intentionally narrow: directly-authored `Box` records may contain an `expose { ... }` block whose entries bind supported semantic selectors to source-level aliases with `=>`.

No geometry, BRep topology, STEP, material, PMI, shell, chamfer, fillet, surfacing, pattern, region, side-hole, Boolean, CIR topology authority, Firmasm, or production-route behavior changes are introduced.

## Relationship to A2.1

A2.1 established the semantic-reference admissibility doctrine and metadata-only fixtures. X3 advances the named Box faces fixture from metadata-only design coverage to parser-backed frontend behavior for `face(axis)` and `face(axis).outerLoop` selectors.

## Relationship to X1/X2

X1 introduced the isolated V2 parser/frontend for typed Box records and Feature AIR `CreateBox` summaries. X2 added parser-backed `base with { size: [...] }` Box derivation. X3 preserves both paths and adds exposure metadata without changing Box lowering dimensions.

## V1/V2 parser separation

X3 remains in the Firmament V2 parser/frontend namespace and structures. V2 exposure fixtures are routed through `FirmamentV2Parser`; V1 syntax is not expanded and V2 fixtures are not sent through the legacy parser.

## Supported X3 syntax

```firmament
solid base: Box {
    size: [10, 8, 6]
    expose {
        face(+Z) => top
        face(-Z) => bottom
        face(+X) => right
        face(+Z).outerLoop => topRim
    }
}
```

Supported axes are `+X`, `-X`, `+Y`, `-Y`, `+Z`, and `-Z`.

## Semantic reference model

`face(axis)` creates a `FaceRef` summary. `face(axis).outerLoop` creates a `LoopRef` summary. Aliases such as `top` and `topRim` are source-level semantic names only: they are not BRep IDs, STEP entity IDs, coedge IDs, or backend topology identifiers, and they do not force BRep materialization.

## `=>` exposure semantics

Inside a Box `expose` block, `selector => alias` records a stable frontend exposure summary containing alias, selector kind, selector string, ref type, axis, and optional subselector.

## Unsupported selectors and constructs

Unsupported in X3: `edge(...)`, `vertex(...)`, profile selectors, feature-output selectors, selector predicates, arbitrary topology queries, raw backend/STEP/BRep/coedge IDs, aliases as feature targets, expose blocks outside Box records, regions, side-hole, chamfer, shell, material/FEA, templates/concepts/PMI/where, and selector-driven BRep emission.

## Alias-copying / `with` policy

`base with { size: [...] }` remains valid. X3 does not silently copy aliases through `with`; derived records receive no inherited exposure aliases. Expose blocks on derived records are rejected as unsupported until an explicit alias-copying rule is designed.

## Trace JSON/text shape

Text trace prints an `Expose:` section beneath each V2 solid with entries such as `face(+Z) => top : FaceRef`. JSON includes `firmamentV2.solids[].exposures[]` entries with `alias`, `selectorKind`, `selector`, `refType`, `axis`, and `subselector` fields.

## Fixture changes

`fixtures/SemanticRefs/valid/named-box-faces-v2.valid.firmfixture` is now parser-backed and expected to reach `feature-air` with four exposures. Focused invalid fixtures cover duplicate aliases, invalid axes, raw backend IDs, `=>` outside expose blocks, and unsupported `edge(...)` selectors.

## Diagnostics

X3 defines stable diagnostics for unsupported expose blocks, non-Box expose usage, duplicate/invalid aliases, unsupported selectors, invalid axes, unsupported subselectors, fat arrows outside expose blocks, and forbidden raw backend ID references.

## Tests run

The implementation was validated with targeted build, CLI trace/help, valid/invalid fixture trace, and filtered CLI/Kernel/FrictionLab test commands during the X3 change.

## Next milestone recommendation

The next milestone should keep alias semantics metadata-only unless an explicit design lands for alias copying through derivation and selector-backed BRep/topology materialization.

## AIR-FIRMAMENT-X8 consumption

AIR-FIRMAMENT-X8 uses X3 parser-backed `=>` aliases as controlled V2 side-hole attach and through targets. The aliases remain scoped to the modified solid's exposure table and must resolve to FaceRef selectors for the bounded `+X` to `-X` side-hole path.
