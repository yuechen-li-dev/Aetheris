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
