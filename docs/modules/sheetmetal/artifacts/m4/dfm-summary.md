# M4 DFM summary

New/strengthened rules:

- exact analytic blank must exist and pass contour validation;
- exact rectangular/round relief topology must exist;
- relief width `>= thickness`;
- relief depth `>= inside radius + thickness`;
- minimum tangent-to-edge flange length;
- existing inside-radius ratio, cut-to-bend, cut-to-edge, overlap, duplicate-cut, bend-line containment, and zero-width checks remain active.

Suggestions are structured and numeric (`increase relief depth to 2.7 mm`, `increase flange Height/Length by X`) and are never auto-applied. Findings use semantic subjects such as `corner-Front-Right`, `FrontWall`, and `relief-Front-Right`.

Template output uses normal `SheetMetalDfm.Evaluate`; no template duplicates DFM logic. The deliberate bad/fixed tray pair demonstrates semantic-only repair. The PSU enclosure passes. The four-relief M3 tray and CTC-03 fail the strict exact-blank rule, correctly preventing M4 from presenting compatibility boundaries as fabrication authority.
