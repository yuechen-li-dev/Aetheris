# Combined-feature authoring feedback

- `countersink-fillet.firmament`: admitted conical countersink plus top boundary
  fillet. Tests vary the included angle and finish, check analytic geometry and
  independently estimated volume, round-trip STEP, and retain all hole sources.
- `two-bodies.firmament`: unsupported independent material bodies. It must reject
  at Extra, with an Assembly/Compose explanation, rather than silently exporting
  only the last body. Removing Modify must not make it valid.
- `two-bodies-compatibility.firmament`: the same rejected intent in the retained
  lowercase V2 spelling. The existing record-derivation fixture remains admitted:
  its referenced prototype is compile-time data, not another output request.

The original two-body and countersink failures were reproduced with the real CLI.
The old palette countersink refusal test is now a positive chamfer/fillet geometry
test. Body palette entries are qualified in empty Models; insertion into occupied
Models explicitly rejects. Existing source fixtures and frozen geometry references
are not rewritten to accommodate the new capability.
