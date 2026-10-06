# Three Telos

Three Telos is an engineering display layer that uses Three.js where Three.js is useful and replaces it where engineering visualization requires stronger guarantees.

Reuse upstream data structures. Own downstream GPU behavior.

This package owns the device, attachments, camera, ray query, raw mesh/field/line submission and lifetime. Three 0.183.2 supplies math and input adapters, never a GPU renderer. See `docs/release/THREE-TELOS-X0.md` in the repository for qualification and migration limits.
