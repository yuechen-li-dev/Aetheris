# Single-edge finish: what Helios offers versus what builds

Question: when Helios offers a fillet or chamfer on a picked edge, does building it succeed? Audit before
adding an eligibility query (light up the edges a finish can take, with a size range, before the user
clicks).

## Method

A throwaway probe compiled every Canonical fixture, took the edges the web runtime offers through
`SingleEdgeFinishSelection.Index`, and built each offered finish (fillet where `FilletSupported`, and
chamfer) at 0.5 mm, inserted as Helios does: an `EdgeFinish` in the body's `Modify` block.

## Evidence

105 parts offered edges; 1880 finishes were offered; 793 built.

| Refusals after offer | Count | Meaning |
|---|---|---|
| Offered, but the history or route cannot take it | 810 | The offer rule is wrong |
| Probe artifact (Box fillets take `Distance:`, not `Radius:`; insertion point) | 144 | Not Aetheris's fault |
| The part's own `Assert` or PMI no longer holds | 96 | Correct refusal |
| Admitted, then construction failed | 33 | Kernel bugs |

Leading causes of the 810:

- `ProfileRimFinishHistoryUnsupported` (294) and `ProfileBoundaryChamferComposeOpenChainUnsupported`
  (150): end-face chamfers offered on composed histories the rim planner does not take.
- `ProfileBoundaryEdgeFinishSequenceUnsupported` (132): the profile route admits one `EdgeFinish`; once
  a part has one, every other offered edge fails.
- Equation-curve profiles (`ProfileBoundaryChamferSegmentUnknown`, "corner is missing or ambiguous",
  134), profiles with inner loops (64), curved segments (`ArcMaterializationNotImplemented`).

Construction failures (33): `brep-preflight-trim-off-surface` on `Set/named-point-access` bottom
chamfers (trim points 0.16–0.36 mm off their plane), `ProfileEdgeFinishMixedShellProfileDegenerate`.

## Cause

Two predicates. `SingleEdgeFinishSelection.Index` offers by topology role (Box edge, longitudinal
corner, end-face edge) and two provenance checks. Each materializer (`TryExportProfileCornerHost`, the
rim and compose chamfer planners, `BuildBoxSingleEdgeStock`) has its own qualification, found only by a
full compile. Helios then maps refusal text to advice with regular expressions (`edgePreviewAdvice.ts`).

Both admissions are cheap without building a solid: the longitudinal finish is a 2D corner rewrite
(`ProfileLongitudinalEdgeFinish.ApplyOne`), the Box finish a function of the Box frame and size, and
the history gates read the parsed composition.

## Verdict

Meaningful progression: the cause is isolated. Kernel construction is mostly sound (33 of 1736 valid
probes); 47% of offers are refused by rules the offer did not consult. Next:

1. One admission per route, called by both the offer and the materializer, returning admitted or a
   typed reason, and the size range.
2. The runtime's selection carries that admission; Helios lights admitted edges, explains dark ones from
   the typed reason, and clamps the size control to the range.
3. The probe becomes a test: every admitted edge builds at a size inside its range.
4. Then the 33 construction failures, and accepting `Radius:` for Box fillets.

## Follow-up: offer equals build

- Admission is a trial build (`SingleEdgeFinishTrial`): the finish is inserted through the editor's own palette
  template and `insertFeature` edit, then the document compiles. An edge is offered only when that succeeds; a
  refusal carries the compiler's code and a plain explanation keyed by it. The size range comes from bisection on the
  selected edge, rounded inward to 0.01 mm.
- Cost: median 160 ms per part natively for both kinds over the Canonical fixtures (p90 1 s, max 12 s); the web
  runtime caches per revision and stops after 20 s with an explicit not-checked reason.
- Web runtime: `edgeFinishAdmission` and `edgeFinishRange` session requests; SDK `edgeFinishAdmission(kind)` and
  `edgeFinishRange(kind, edgeId)`.
- Helios: Fillet an edge… / Chamfer an edge… light the admitted edges (gold, other edges faded, both renderers); a
  click on a lit edge starts sizing with its range; a click on a dark edge says why per finish; sizes outside the
  range are refused before compiling.
- Contract: every one of the 825 admitted Canonical finishes built at 25, 50 and 75% of its range (2475 builds, no
  failure). `SingleEdgeFinishTrialTests.EveryOfferedFinish_BuildsInsideItsRange` keeps it on four fixtures.
- Fixed while there:
  - Box fillets on concept-bounded stock accept `Radius:` (the editor template's field), not only `Distance:`.
  - An open chamfer run ending on a slanted neighbour placed its end square to the chamfered edge, off the
    neighbour's line: bottom chamfers failed export preflight (0.08 to 0.36 mm off plane) and top chamfers bent the
    neighbour's side face. The end now slides along the neighbour, past the vertex at a reflex corner. Checked
    exactly: the rhombus plate's chamfer volume matches the closed form to 1e-6 mm³, by Aetheris and by an
    independent kernel's reader.

## What still should be finishable

Judged by what a person picking the edge expects, not by what the routes cover:

| Refusal | Count | Verdict |
|---|---|---|
| Fillet on one end-face edge (`EdgeFinishFilletNotOffered`) | 906 | Should work. The existing cap fillet stops one radius short of each corner, which is not what picking an edge means; a full-length end needs new termination topology (the end quarter-disc lying in a square neighbour's side). Its STEP also fails an independent reader: two end faces' loops run the wrong way. Next task. |
| Second finish on a Profile body (`SequenceUnsupported`) | 132 | Should work; basic use. |
| End-face chamfer after other operations (`RimFinishHistoryUnsupported`, `ComposeOpenChainUnsupported`) | 396 | Mostly should work when the operations stay clear of the edge. |
| Profiles with openings, equation curves, curved edges | 216 | Should work eventually; outer-edge chamfers on a plate with a hole first. |
| The part's own Assert or PMI would break | 88 | Correct refusals. |

142 further refusals have no code prefix (`EdgeFinishRefused`) and are not yet classified.

Verdict: meaningful progression. The offer can no longer disagree with the build, and what it offers builds across
its range. The next blocker is coverage, isolated above, led by full-length fillets on end-face edges.

## Follow-up: full-length fillets on end-face edges

- A fillet on one Top or Bottom edge with no `EndClearance` runs the whole edge. At square outside corners the
  round's end section lies in the neighbour's side plane: the cap loses its two corners, each neighbour's side face
  takes the end arc, and there are no end walls. Inside, slanted and too-short neighbours reject naming the end.
  Explicit `EndClearance` keeps the stop-short round; chains keep stopping one radius short.
- The stop-short round's end walls faced into the material. Its STEP reread at a different volume (4019.857 against
  4019.570 mm³ on the bracket foot) and an independent reader rejected both faces. They now face the groove.
- Evidence: bracket foot end, r = 1 mm, top and bottom: 4019.356194 mm³ against the closed form 4019.3561945, by an
  independent kernel reading the STEP, no issues; stop-short 4019.570796 against 4019.5707963. Canonical admission
  rose from 825 to 958 finishes; all 958 build at 25, 50 and 75% of their range. 142 refusals on library-part bodies
  now carry `EdgeFinishBodyNotInDocument`.
- Remaining cap-fillet refusals: composed bodies (276), a body that already has a finish (264), and profiles holding
  any arc, even away from the edge (154).

Verdict: success for the motivating case: picking a square-cornered end-face edge and choosing Fillet rounds it
end to end, in Helios and in source.
