# Cadmata

Cadmata is Aetheris's interactive 3D engineering presentation and inspection surface. It displays geometry, supports selection and feature association, and presents semantic PMI—datums, dimensions, GD&T, and engineering notes—with filtering and inspection context.

Use `aetheris view model.firmament` or `aetheris view part.step` from the qualified Windows bundle to open the UI in your existing browser. `Cadmata.exe` starts a blank browser session; `Cadmata.Desktop.exe` is an optional Windows window using shared Evergreen WebView2. The normal bundle contains no browser engine. The standalone .NET tool package does not contain Cadmata. Cadmata is not primarily a traditional drawing system: static drawing and shop-floor projection are downstream presentations of the semantic model.

Cadmata opens AP242 STEP assemblies as a product tree with shared part definitions and placed occurrences. The STEP viewer's import control is the normal entry point; internal fixture endpoints are reserved for automated qualification and are not part of the viewer controls.

Use the viewport's **Measure** tool for picked-point distance, parallel support-plane
spacing, and analytic axis spacing. See [measurements in Cadmata and Helios](measurement.md)
for reference replacement, world-coordinate deltas, units, approximation labels and
the supported geometry boundary.

The packaged desktop convenience window is Windows-only. The .NET server and web
UI are separate from that shell: a server built for Linux or macOS can serve the
same UI to a browser. This is not a native macOS/Linux desktop-app release claim;
WebView2 is not used on those platforms. See [launch requirements](portable-windows.md),
[PMI presentation](pmi.md) and the [support matrix](../reference/supported-features.md).
