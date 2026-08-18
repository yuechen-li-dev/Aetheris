# Semantic Value M1

Milestone `P2-SEMANTIC-VALUE-M1` establishes the common typed layer and proves
the bounded routes already supported by Preview 2.

## Producers

- Firmament profiles and Concept Paths produce profile/selectable/exact/Compose
  values with validated `ResolvedProfile2D` bindings. Compose now obtains path
  operands through this contract.
- Concept IR scalars and Concept Struct members normalize without invented
  geometry capabilities.
- Template expansion runs first; specialization and Table/Record/with-derived
  input remain provenance while output identity remains its own identity.
- InlineStep/Recognize reimports the canonical artifact, checks the persisted
  STEP-entity-to-FaceId association, and exposes only recognized regions as exact
  boundary/selectable/analysis values. It does not infer profiles.
- Forge capabilities may return a compiler-owned semantic root. The sample
  exposes exact `TopFace` and `LoadRegion` members plus its exact body.

## Consumers and diagnostics

Profile and Compose share `ProfileSemanticConsumer`. Selection accepts a
`SemanticReference` and exact selection/BRep face/region evidence. Modify admits
only exact body, face/region, or profile bindings. FEA accepts
`BoundaryRegionCapability` and normalizes before AnalysisIR. The direct imported
proof uses `imported.MountFace`; native `body.face(+X)` syntax is also normalized
through a produced semantic value.

Standard codes are `semantic-value-missing-capability`,
`semantic-value-no-exact-binding`, `semantic-path-member-missing`,
`semantic-path-member-not-exposed`, `semantic-value-stable-identity-collision`,
and `forge-semantic-output-invalid`. Segment resolution retains the segment span.

## Stable identity and provenance

Native IDs derive from canonical authored declaration identities. Template
outputs retain their output ID and add specialization/input provenance.
Recognize IDs combine the canonical resource hash, body/member identity, and
sorted exact STEP entities. Forge IDs combine extension capability/version and
canonical arguments. Ordered member/capability/binding inspection is stable.
Repeated CLR allocation is irrelevant.

Provenance is an ordered list, never a cyclic object graph. Typical chains are
authored -> template specialization -> path segment -> consumer,
InlineStep hash -> Recognize declaration -> consumer, and template -> Forge
capability/version -> construction -> consumer.

## Bounded limitations

There is no general imported profile recognizer; recognized hole/plane regions
are boundary regions only. Modify syntax has not been broadened. Forge adds no
geometry family and the sample does not claim `ProfileCapability`. Raw faces,
edges, mesh elements, and arbitrary host objects remain unexposed. Cadmata keeps
its existing semantic/provenance tree; this M1 does not redesign its API.
