# Preview 3 known issues and limitations

- Release binaries are qualified for Windows x64 only. Framework tests on other platforms are not a binary-support promise.
- `inlineSTEP` FEA supports the documented single-body imported class and stable imported face identities. Arbitrary imported containment and multi-root assembly-like STEP are rejected explicitly; some OCCT B-spline containment cases remain outside the qualified class.
- The generic tessellated mass verifier can report `Unavailable` for some valid combined analytic Boss/Pocket/hole models when a face cannot be triangulated. STEP reimport, manifold checks, analytic surface inventory, bounds, and route-specific analytic volume remain available; do not interpret an unavailable tessellation estimate as a topology failure.
- Compose-host Countersink is outside the current section-stack feature family. Model-domain Countersink remains supported. Sheet Metal Counterbore and Countersink are rejected because those formed-opening semantics are not implemented.
- Imported STEP analysis currently reports millimetres as the qualified public length unit but does not preserve the source unit entity in imported topology metadata. Use millimetre AP242 input for Preview 3 workflows.
- Standard Library material resolution is qualified for Firmament FEA and authored Sheet Metal. Ordinary prismatic CAD STEP export does not yet persist a general solid-material designation; do not infer one from the geometry artifact.
- Cadmata may emit one Three.js `Clock` deprecation warning in the browser developer console. It does not affect loading, navigation, selection, PMI interaction, or model switching.
