# Variable outer section stack M3

Constant composed slabs reuse one two-dimensional region at both stations. M3 introduces `VariableOuterSectionInterval` as a separate plan-time type for the narrow case in which the outer line loop changes while every cavity loop is unchanged.

The interval owns lower/upper stations, lower/upper outer Profiles, explicit vertex and segment correspondence, unchanged inner-loop correspondences keyed by feature id, semantic owner, and provenance. Its validator rejects non-increasing stations, missing or duplicate correspondence, outer vertex-count mismatch, non-line outer segments, duplicate cavity identities, and any inner-loop center/radius/curve change.

This type is not yet consumed by `PrismaticSectionStackEmitter`; the emitter still assumes each slab has one constant `PrismaticSectionRegion`. The required next patch is deliberately localized: add a typed interval union to the authoritative section-stack construction and teach the emitter to create outer planar transition faces while reusing each unchanged inner loop's analytic wall and final cap loop. No materialized BRep may be changed afterward.
