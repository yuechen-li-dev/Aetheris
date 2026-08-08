# P1-HARDEN-M2 status

This pass reached **meaningful progression**, not release closure.

The original hypothesis was only partly correct: the ~41k mm³ number is an overconservative whole-shell `area × 0.1 mm × 4` envelope, but the measured values are also materially biased. The generic mass route consumes display tessellation rather than integrating exact trimmed analytic patches. A close two-resolution delta concealed a 1.8–3.8% trim-domain bias.

The current geometry was frozen under `artifacts/release/p1-harden-m2-baseline/`. No Fillet construction, topology, policy, or STEP exporter code changed. M2 attempted a narrow Green-theorem cap lane; M3 full-corpus validation proved that its shared curve-orientation assumptions regressed other prismatic bodies, so the production attempt was removed. The forensic localization remains useful, but no partial cap integral is shipped as authority.

This isolates the remaining blocker to curved supports: cylinder/ellipse miter trims, sphere trims, and ring/horn-torus trims still need a parameter-domain boundary integral or deterministic adaptive quadrature with a meaningful error estimate. Until that exists, literal tolerances cannot be tightened and promotion would be unsupported.

FreeCAD 1.0.2 evidence already recorded by X8 matches the independent source-section values exactly. SolidWorks was not automated in this pass. Public documentation and the freeze manifest are intentionally unchanged because the support decision did not change.

Recommended next milestone: one narrowly defined continuation implementing and testing exact curve-to-UV trim-domain integration for Cylinder first, followed by Sphere/Torus, rather than `P1-CLAUDEFOOD-R4`.
