# Helios authoring feedback

- `hole-variant.firmament` is an admitted Shaft hole. Tests specialize its
  variant and relief fields to qualify Shaft, Counterbore and Countersink
  completion, bound-field inspection and real geometry construction.
- Adding counterbore fields to the Shaft specimen must reject at those field
  tokens, without an unrelated malformed-Model diagnostic or duplicate
  parse-status error. Both accepted capitalization aliases retain locations.
- `empty-modify.firmament` deliberately keeps the Modify after deleting its last
  feature. It must compile to exactly the unmodified stock STEP. Replacing its
  target with an unresolved name must reject.

The public-SDK browser qualification in
`scripts/qualify-assembly-motion-browser.mts` exercises these cases in real
WASM alongside the articulated-motion proof. It does not qualify Helios's
viewport renderer.
