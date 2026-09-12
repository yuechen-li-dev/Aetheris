# LANG-PIPELINE-X1 — Profile / Path composition pipelines

## Executive verdict

Accepted. Firmament can derive Profiles and open Concept Paths from existing named semantic geometry without restating span endpoints or ordinary segment identity. Pipeline syntax is erased by the typed Profile/Path binder and lowers to the existing `ResolvedProfile2D` / `ResolvedConceptPath2D`, AIR, BRep, and STEP routes. It introduces no runtime pipeline or general control flow.

## Authority audit

1. A traceable span already carries analytic line/arc geometry, stable source identity, ordered endpoints, and source-container provenance. Named Point2 values and Concept Path endpoints provide semantic endpoint identities; the existing `1e-9` binder tolerance supplies the bounded geometric fallback.
2. `From`/`To` was required because the manual Segment grammar selected a guide plus an independently authored directed subspan. This remains useful for bespoke circle arcs, explicit endpoint choice, and debugging, but it redundantly restates a complete line/path span.
3. Manual orientation was resolved by `SelectGuide`; path orientation was fixed during `BindPaths`. X1 resolves pipeline orientation in `ProfileAuthoringParser` immediately before constructing the same `ResolvedProfileSegment2D` values.
4. Source span identity supplies the default output identity. Outer-loop `Stock.Bottom` becomes `Bottom`; inner-loop defaults are loop-qualified because the existing Profile validator requires Profile-wide uniqueness. `As` overrides output identity without changing provenance.
5. Manual `Segment { Trace/From/To/Sweep }`, `Profile ... From <ConceptPath>`, and `Loop ... From <ConceptPath>` remain available as low-level/whole-path authoring.

No parallel Profile representation, BRep backend, materializer, generic `Pipe<T>`, or runtime stage exists.

## Hero before / after

The flagship contains `BaseProfile`, `PadProfile`, and `CutoutProfile`. Each previous rectangle required four Segment blocks and repeated eight endpoint references:

```firmament
Profile BaseProfile Using Layout { Loop Outer {
    Segment Bottom { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
    Segment Right { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
    Segment Top { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
    Segment Left { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
} }
```

X1 states the engineering idea directly:

```firmament
Profile BaseProfile Using Layout { Loop Outer {
    Stock.Bottom
    |> Stock.Right
    |> Stock.Top
    |> Stock.Left
    |> Close
} }
```

Across the three rectangle Profiles, the canonical pipeline removes 12 Segment declarations and 24 repeated endpoint references. The safety result matters more than line count: `Trace: Stock.Bottom` can no longer contradict separately typed `From`/`To` values, and source names no longer need duplicate declarations.

## Semantics and safety

- Context typing: pipelines are admitted only as qualified geometry chains inside Profile loops or Concept Path declarations.
- Ordering: source order is semantic order; the binder never searches or reorders spans.
- Orientation: first-stage source direction is canonical. Each later stage attaches by endpoint identity/established tolerance; a unique end match reverses it.
- Explicit control: `Reverse <span>` requires that exact direction to connect. `<span> As <name>` provides bounded renaming.
- Identity: source leaf identity is inherited. Collisions fail as `firmament-profile-pipeline-identity-collision`; numeric suffixes are never synthesized.
- Closure: `Close` validates equality of final and first endpoint and adds no geometry.
- Whole-loop tracing: `<closed Concept Path> |> TraceLoop` copies ordered spans, preserves provenance, and normalizes outer/inner winding. Non-loop inputs fail typed.
- Provenance: ordinary Profile segments expose `tracedFrom`, `reversed`, `pipelineIndex`, and invocation source range through `inspect-profile --json`.
- Diagnostics: disconnected chains name the current and candidate endpoints; ambiguous degeneracy, invalid stages, identity collisions, invalid TraceLoop sources, and open Close are distinct diagnostics.

Manual Segment authoring remains the escape hatch for unusual subspans, explicit full-circle arc endpoints/sweep, mixed low-level work, and diagnosis.

## Control-flow boundary

`|>` is finite semantic composition. It does not introduce conditionals, loops, lambdas, filtering, mutation, arbitrary Feature calls, runtime data flow, nearest-neighbor path finding, or Pattern behavior. Feature remains reusable semantic transformation; Pattern remains bounded repetition; Template remains compile-time specialization.

## Qualification witnesses

- `fixtures/Canonical/Pipeline/profile-pipeline-flagship.firmament`: three Profile Compose/STEP product route.
- `rectangle-profile.firmament`: focused inherited-identity extrusion.
- `line-arc-profile.firmament`: mixed analytic Line/Arc chain.
- `profile-with-inner-loop.firmament`: opposite-winding hole.
- `open-path.firmament`: pipeline-authored open Concept Path through the real analytic Sweep/STEP route.
- `traceloop-profile.firmament`: real closed-loop copy.
- `manual-profile-parity.firmament`: low-level parity source.
- `fixtures/Invalid/Pipeline`: CLI-build witnesses for ambiguity, an open `Close`, disconnection, inherited-identity collision, invalid `TraceLoop`, a pipeline outside Profile/Path, and a wrong-type stage. Focused tests also cover precedence and control-flow rejection.

There is intentionally no fabricated “ambiguous loops” fixture: the current traceable whole-loop authority is one named `Concept Path`, which owns exactly one ordered chain. Profiles and Bodies are rejected as TraceLoop sources rather than treated as implicit loop containers. If a future semantic boundary value can expose multiple loops, selecting it without `.Outer`/a named loop must add the specified ambiguity diagnostic before admission.

## Schema and interaction boundary

Mechanical is the qualified schema. Template-specialized ordinary geometry continues through the same expansion-before-bind path. Feature, Pattern, and Template grammars remain unchanged, and pipeline stages cannot invoke them. Sweep can consume an open pipeline-authored Concept Path through its existing resolved-path contract; SectionChain can consume pipeline-derived Profiles through its existing Profile capability adapter. WireForm retains its own forming program. No new schema fallback is introduced.

## Validation

Validation evidence is generated under ignored `artifacts/local/lang-pipeline-x1/`.

- `dotnet build Aetheris.slnx --configuration Release --no-restore`: succeeded with 0 warnings and 0 errors.
- `dotnet test Aetheris.slnx --configuration Release --no-restore`: 3,308 passed, 0 failed, 0 skipped. The pre-existing empty `Aetheris.FrictionLab.Tests` assembly reported no discoverable tests.
- All seven canonical pipeline fixtures built through the CLI; all seven invalid fixtures failed with their typed pipeline diagnostics.
- Pipeline, repeat pipeline, manual-parity, and separately published packaged-CLI STEP exports all have SHA-256 `51A435DD2F558890FAA13A8E50CBA2A42B26A0A2AD12EC095AFAD7A9DA6B58DD`.
- The flagship STEP reimports as one enclosed-manifold body with 15 faces, 36 edges, 24 vertices, and bounds `[-20,-12,0]` to `[20,12,12]`.
- The packaged CLI was published to a temporary directory and executed with the repository outside its working directory.
- Public-document link tests are included in the 410 passing CLI tests. The repository information-architecture guard passed for 3,960 tracked files, and `git diff --check` passed.

The legacy mapping formatter does not format modern canonical V2 Profile bodies, so X1 adds no false formatter route. Public examples use one stage per line for long chains, and the shipped VS Code grammar recognizes `|>` as an operator.
