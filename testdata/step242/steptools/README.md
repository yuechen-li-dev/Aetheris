# STEP Tools samples

`boxy_with_surfacetex.stp`: STEP Tools, Inc. "Boxy" STEP-NC test part with a surface-finish specification and A, B, C datums, written by ST-Developer v14 (2010, schema AUTOMOTIVE_DESIGN). Unmodified; reported as failing to import on Hacker News.

It declares both circles of its 30 cylindrical bands as `FACE_OUTER_BOUND`, which the schema allows at most once per face. An independent reader measures the solid at 174839.178053 mm³ with 80 faces.
