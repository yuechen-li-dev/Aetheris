# Cadmata

Cadmata is Aetheris's interactive 3D engineering presentation and inspection surface. It displays geometry, supports selection and feature association, and presents semantic PMI—datums, dimensions, GD&T, and engineering notes—with filtering and inspection context.

Use `aetheris view model.firmament` or `aetheris view part.step` from the qualified Windows bundle. The standalone .NET tool package does not contain Cadmata. Cadmata is not primarily a traditional drawing system: static drawing and shop-floor projection are downstream presentations of the semantic model.

Cadmata opens AP242 STEP assemblies as a product tree with shared part definitions and placed occurrences. The STEP viewer's import control is the normal entry point; internal fixture endpoints are reserved for automated qualification and are not part of the viewer controls.

Preview 3 qualification is Windows x64. See [PMI presentation](pmi.md) and the [support matrix](../reference/supported-features.md).
