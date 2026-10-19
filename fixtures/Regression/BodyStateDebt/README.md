# Body-state and semantic-debt witnesses

These sources are exercised by BodyStateDebtTests, BodyStateDebtCliTests and
ProfileTemplateProjectionTests. They are not all standalone valid products.

| Specimen | Expected result / evidence |
| --- | --- |
| box, cylinder, profile, counterbore-chamfer | Accepted engineering-source BodyState; exact STEP equality to the corresponding `.reference` product and serialized source replay. |
| explicit-pmi | Accepted Record/Pattern/Compose/counterbore/chamfer product with explicit PMI; original STEP and semantic product survive construction/replay unchanged. |
| plain-offset | Accepted hole-free housing and crown edit; no fabricated hole entity or hole PMI; replay agrees. |
| second-hole-after-add | Accepted attachment followed by two holes; compare exact STEP to the reference with all bores constructed before attachment. Both earlier geometry and feature identities must survive. |
| unsupported-cylinder-edit | Rejected operation capability; the source cylinder must never become a housing. |
| history-drop | Rejected history capability; a later housing rebuild must never discard the earlier attachment. |
| unknown-field | Rejected unknown `heigth` field at its authored field location, without an unrelated Model/output cascade. |
| record-member-string | Accepted typed Template product. A string containing `spec.ThisIsTextAndNotAField` is text, not a missing Record member. A test mutation to a real missing member must reject. |
| requirements.fragment | Prefix for existing Structural/Sculpting/PlasticShell products; the true predicate preserves exact STEP, and its false mutation rejects validate/build. |
| profile-hygiene.fragment | Syntax-transformation-only specimen. Checks local-guide/member/segment identity and preservation of strings/field labels/external members. Not counted as a materializing product. |

The tests also wrap unchanged canonical cone, sphere, torus, rounded-box, Gear,
Sweep and Revolve products using the correct state-owner selector. Typed-value
checks distinguish frame/knot changes and actual replacement control-net changes;
replacement construction is serialized, restored and replayed to the same STEP.

The full regression suite and canonical qualification remain separate gates.
Run output belongs under ignored `artifacts/local/`, never beside these files.
