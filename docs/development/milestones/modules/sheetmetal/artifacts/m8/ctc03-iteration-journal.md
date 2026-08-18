# M8 iteration journal and LLM friction

| Interpretation | Feedback | Correction | Improvement |
|---|---|---|---|
| Historical recovery contained only two deck slots | CLI recognition exposed many cylindrical inner loops, but the recognizer required one closed circle edge | Admit multi-edge coaxial circular loops deterministically | Source inventory increased to 15 holes + 2 slots |
| Service feature looked like a full-width flange | Boundary evidence showed a 127 mm attachment and an extended central outer tab | Add generic `Span`/`SpanOffset` and bounded `Tab` semantics | Correct bend length and distinctive tab without a recovered polygon |
| Repeated holes were initially independent cuts | Semantic equality/pitch checks and comparison paths made duplication fragile | Use named `Pattern` declarations and generated stable members | 17/17 feature matches with fewer independent edits |
| Feature centers mixed global and local frames | Named region datums and `Region.Center + offset` exposed the intended frame | Resolve datums before profile lowering | 45-degree service features lower correctly from local coordinates |
| DFM suggested moving front mounting holes | Comparison showed source centers were already correct | Keep source positions; identify missing mounting-flange contour as cause | Avoided a locally “green” but source-wrong patch |

## LLM friction

**What did Codex repeatedly know semantically before she knew the final exact coordinates?** That the openings formed equal-size/equal-pitch groups; that front and rear mounting holes were paired about local flange frames; that the service cluster belonged to a partial 45-degree flange; and that a central connector tab modified its outer edge. Exact local origins and which side of each bend owned the coordinates came later from CLI evidence.

**Which new construct helped most?** `Pattern`. It converts count, feature size, center, and pitch into stable members plus executable equality evidence. `Datum` was a close second because it separated recovered placement from feature definition.

**What still feels awkward?** Irregular outer edges. The language can express a rectangle, a partial span, and one bounded tab, but related chamfers, end bands, notches, and step transitions still want either a giant coordinate profile or new semantic edge operations. A general sketch solver would be premature; a composable edge-profile MIR is the narrower next step.
