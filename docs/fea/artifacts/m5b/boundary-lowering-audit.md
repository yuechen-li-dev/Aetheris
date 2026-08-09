# M5 boundary-lowering audit

| condition | M5 semantic resolution | M5 lattice lowering | orientation blocker | M5B replacement |
|---|---|---|---|---|
| fixed | semantic path / imported recognition | global coordinate equality | rotated plane has no constant global coordinate | exact fragment cells select nearest face-side supports |
| traction | semantic path | outer lattice-face area | global-axis plane parser | exact polygon area and Q1 trace integration |
| resultant | semantic path | separate uniform distribution | area and center can shift | exact area -> traction -> common quadrature |
| pressure | semantic path | hard-coded axis normal | global/stored sense is not material outward | CIR two-sided normal probe |

Semantic identity previously stopped before mechanics: mechanics reconstructed one of six box sides from source text and global axes. Exact BRep plane, trim, and face ID were absent from integration. M5B resolves `PlanarBoundaryDomain` only inside the backend; no generated identity leaks into Firmament.
