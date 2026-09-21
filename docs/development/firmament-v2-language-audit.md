# Firmament V2 — corpus rewrite and language-surface audit

Date: 2026-09-21. Parser treated as source of truth throughout
(`FirmamentV2/ProfileAuthoringParser.cs`, `FirmamentV2/FirmamentV2Parser.cs`).

## 1. What was done

The Canonical and Regression corpora were rewritten from the low-level `Segment`
form to the `|>` pipeline form. 39 fixtures changed.

Every rewrite was gated on two oracles, not on eyeballing:

1. **Profile equivalence.** `ProfileAuthoringParser.ResolveNamedProfile` was run
   over the before and after source, and the resolved frame, loop set, segment
   names and segment geometry dumped to canonical text. A rewrite was applied
   only when the two dumps were byte-identical.
2. **Document parse identity.** The full 281-file document-level parse
   (disposition plus diagnostic list, `Canonical` + `Regression/CanonicalGeometry`
   + `Regression/ProfileComposition` + `Regression/Profile`) was compared before
   and after. The diff is empty — no file changed disposition, and no file gained
   or lost a diagnostic.

Deliberately excluded from the rewrite, because the low-level form is the thing
under test in those files: `Compatibility/LegacyV1`, `Regression/LanguageBurnIn`
(its README calls it "regression and diagnostic evidence, not canonical
examples"), `Speculative`, every `Invalid/*` negative fixture,
`Canonical/Pipeline/manual-profile-parity.firmament`, and the four
`*low-level*` / `*low-level-segments*` parity witnesses in
`Regression/CanonicalGeometry`.

Test state after the rewrite: Firmament 1550/1550, Kernel.Core 1080/1080,
Server 59/59.

## 2. A real bug the rewrite smoked out

`Aetheris.Kernel.Firmament.Tests.Assembly.DifferenceEngineStorageGateTests.`
`GearMeshIncludesEveryFlankWithOutwardNormalsAndClosedVolume` was failing at HEAD
with enclosed volume 6.25% above cap-area-times-height. Bisected to `9b9d1ca7`
("Fix same sense"), which wired `DisplayMeshOrientation.Orient` into
`BrepDisplayTessellator`'s face dispatch.

`AssemblyDisplayMeshExporter` was already calling `DisplayMeshOrientation.Orient`
itself on the legacy tessellation path. Once the tessellator did it too, every
face whose binding says `same_sense = .F.` got flipped twice. The gear has
exactly one such face — the central bore cylinder — and its divergence-theorem
contribution came back at `+293.666` through the exporter versus `-292.196`
straight from the tessellator. Two times 293 over a 9369 part is precisely the
6.25% seen.

This is the same shape as the plane double-count fixed on the STEP import side in
the same commit: two layers each honouring the face sense once. Fixed by deleting
the exporter's own `Orient` call, since the tessellator now projects the face
sense for every surface kind at the single place face patches are produced.

Unrelated observation: `Step242RoundTripCorpusTests.ExportedBody_IsReadableByAetheris_AndKeepsItsShape`
failed once under full-suite parallel load and passed both in isolation and on a
rerun of the full suite. It is time-budgeted against the 5s display timeout, so
it is load-sensitive rather than wrong. Worth a wider budget under xunit
parallelism if it recurs.

## 3. Audit: where the language is not yet coherent

### 3.1 The pipeline traces whole guides; `Segment` traces sub-spans — MAJOR

This is the only structural gap, and it is the reason every surviving `Segment`
in the non-quarantined corpus survives.

`Segment N { Trace: G; From: A; To: B }` selects the piece of `G` between two
named points. A pipeline stage always traces `G` end to end. So the L-bracket
pattern — two overlapping rectangles where the outer boundary walks part of
`Horizontal.Top`, turns at a named interior point, and picks up `Vertical.Left` —
has no pipeline spelling at all. Converting it yields
`firmament-profile-pipeline-disconnected`.

Files that are stuck on this: `Regression/CanonicalGeometry/profile-compose-l-bracket.firmament`,
`profile-compose-l-bracket-external-modify-counterbore.firmament`,
`profile-compose-reflex-chamfer-with-counterbore.firmament`,
`profile-compose-reflex-chamfer-with-shaft.firmament`,
`Canonical/PMI/counterbore-shaft-diameter.firmament`, and
`Canonical/Revolve/sphere.firmament` (a half-circle meridian, which is the same
gap wearing an arc).

`docs/public/firmament/syntax.md:247` presents `Segment` as the low-level escape
hatch the pipeline supersedes. That is not true today and the doc should be
corrected regardless of whether the gap is closed.

**Proposal (for review).** Give the pipeline a sub-span stage. Two spellings
worth arguing about:

```
Horizontal.Top To Notch |> Vertical.Left |> Close      # implicit From = current endpoint
Horizontal.Top[TopRight..Notch] |> Vertical.Left |> Close
```

The first reads better and carries less syntax, and the pipeline already tracks
`current` for connectivity, so `From:` is redundant in a chain — it is only
needed on the opening stage, where `Reverse` already plays that role. The second
is more explicit and survives the case where a stage is not the natural
continuation. I lean to the first.

This also subsumes `Sweep: Clockwise | CounterClockwise`, which likewise has no
pipeline equivalent. For a full circle `TraceLoop` already resolves the
direction; for a partial arc the sweep falls out of the two endpoints plus
`Reverse`.

### 3.2 Implicit-loop pipelines break on any inline guide declaration — MAJOR

`Profile P Using Layout { A |> B |> C |> D |> Close }` works: the outer loop is
implicit. But if the `Profile` body also declares a guide inline —

```
Profile SideProfile Using PositiveXWorkplane {
    Rect2 Outline { Center: [0mm, 0mm]; Size: [20mm, 10mm] }
    Outline.Bottom |> Outline.Right |> Outline.Top |> Outline.Left |> Close
}
```

— it fails with `firmament-profile-pipeline-expression-required`, because the
pipeline binder requires the loop body to consist of nothing but the pipeline,
while the `Segment` binder scans for matches and tolerates neighbours. So the
presence of an inline `Rect2` silently forces the legacy form. This is
`Regression/Profile/valid/construction-plane-positive-x.firmament`, the one file
in the whole sweep that failed *only* because of body purity.

**Proposal.** Let the pipeline binder consume the pipeline expression and ignore
recognized declarations in the remainder, rather than rejecting any non-pipeline
content. The declaration set is already parsed by `AddOrdinaryGuides`; the purity
check is the only thing standing in the way. Low risk, removes a real footgun.

### 3.3 Inner-loop segment naming is asymmetric between the two forms — MAJOR

Manual segments keep their declared name verbatim in every loop. Pipeline stages
are named `{LoopName}.{leaf}` in any loop other than `Outer`, "to preserve
Profile-wide uniqueness". Two consequences:

- The manual form permits cross-loop identity collisions that the pipeline form
  makes impossible.
- A faithful rewrite of an inner loop must alias every single stage with `As`,
  which is exactly the busywork the pipeline was introduced to remove.

**Proposal.** Pick one rule and apply it to both binders. Qualifying in every
loop including `Outer` is the coherent choice, but it churns stable ids for every
existing selection. Qualifying in neither, and diagnosing a collision instead, is
cheaper and matches what an author would predict. Either way, the two forms
should not disagree.

### 3.4 `TraceLoop` admissibility is inconsistent — MODERATE

`TraceLoop` admits a full `Circle2` or `Ellipse2` guide, or a closed bound
`Concept Path`. `Polygon2<Rhombus> |> TraceLoop` works — but only because
`Polygon2` lowers to a `Concept Path` before pipeline validation runs.
`Rect2 Stock |> TraceLoop` does not work, and must be spelled as four stages,
even though `Rect2` declares exactly four named sub-guides that form a closed
loop. Whether `|> TraceLoop` is available to an author is currently a fact about
lowering order, not about the guide.

**Proposal.** Admit any guide that exposes a closed named sub-guide family, which
makes `Rect2 |> TraceLoop` work and collapses the most common four-stage pipeline
in the corpus to one stage.

Note that `TraceLoop` on a circle is not identity-preserving relative to a
quadrant decomposition: it yields one `Boundary` segment where four `Q1..Q4`
segments used to be. `Regression/ProfileComposition/mixed-line-arc-additive-overlap.firmament`
was left in the manual form for exactly this reason — its round-trip test pins
the four arc identities. `Canonical/Features/Boss/circular-boss-through-hole.firmament`
and `boss-pocket-block.firmament` were converted, because nothing selects their
quadrants.

### 3.5 `Reverse` is load-bearing only on the first stage, and a trap after it — MODERATE

`OrientPipelineCurve` auto-orients every stage after the first against the
current endpoint. But when `Reverse` is written explicitly, auto-orientation is
switched off and the stage must match the current endpoint at its *start* or the
loop is reported disconnected. So mid-chain `Reverse` can only ever break a
pipeline that would otherwise have worked. On the opening stage, where there is
no current endpoint, `Reverse` is the only way to set the loop's initial
direction.

**Proposal.** Either diagnose `Reverse` on a non-opening stage, or redefine it as
a preference — try the reversed orientation first, fall back to auto — so it is
never worse than omitting it. The second keeps the keyword honest in both
positions.

### 3.6 One diagnostic code covers two unrelated errors — MINOR

`firmament-pipeline-stage-type:<x>` is emitted both when `<x>` is a guide name
that does not resolve, and when `<x>` is a stage kind (`Close`, `TraceLoop`) in a
position that does not accept it. An author who misspells a guide gets a message
about stage types.

**Proposal.** Split out `firmament-pipeline-unknown-guide:<name>`.

## 4. Summary of proposals

| # | Change | Severity | Cost |
|---|---|---|---|
| 3.1 | Sub-span pipeline stage (`G To P`), subsuming `Sweep:` | Major | New grammar; unblocks 6 fixtures and retires `Segment` |
| 3.2 | Implicit-loop pipeline tolerates inline guide declarations | Major | Small; drop a purity check |
| 3.3 | One loop-qualification rule for both binders | Major | Medium; stable-id churn on one of the two options |
| 3.4 | `TraceLoop` admits any closed named sub-guide family | Moderate | Small; collapses the commonest 4-stage pipeline |
| 3.5 | `Reverse` is a preference, not a constraint | Moderate | Small |
| 3.6 | Distinct unknown-guide diagnostic | Minor | Trivial |

No new features are proposed. 3.1 is the only one that adds grammar, and it adds
it to close a hole where the "correct way" is currently not the easy way but the
impossible way.
