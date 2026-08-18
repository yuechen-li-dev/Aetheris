# Curve representation audit

Audit date: 2026-08-13. Public means accessible across assemblies; bounded refers to the representation itself rather than a B-rep binding that trims an unbounded support.

| Family / area | Visibility and bounds before M1 | Derivatives / domain before M1 | Provenance and consumers | M1 seam |
|---|---|---|---|---|
| `Line3Curve` / Kernel.Core | public, unbounded support | `Evaluate`, unit `Tangent`; no owned domain | B-rep edges, STEP, tessellation, Panels, Piping | bounded distance-parameterized adapter with raw derivative |
| `Circle3Curve` / Kernel.Core | public, periodic support | `Evaluate`, raw `Tangent`; no owned domain | B-rep edges, STEP, Panels, Drawing, Piping | arc/full-circle adapter; explicit periodic seam |
| `Ellipse3Curve` / Kernel.Core | public, periodic support | `Evaluate`; derivative duplicated by consumers or absent | STEP/imported and constructive edges, Drawing/tessellation | bounded adapter supplies analytic first derivative |
| `Hyperbola3Curve` / Kernel.Core | public, unbounded explicit branch | first and second derivatives; trim owned by B-rep | transverse cone-plane construction and tessellation | bounded branch adapter; construction authority unchanged |
| `BSpline3Curve` / Kernel.Core | public, knot-bounded non-rational spline | `Evaluate`, exact polynomial `EvaluateTangent`, knot domain | STEP, Panel boundary extraction, tessellation | knot-domain bounded adapter; degree remains on native support |
| Arc | represented by bounded `Circle3Curve` trim | trim stored separately | Panel ruled boundaries and many B-rep features | same circle adapter with authored trim orientation |
| Helix | no named Kernel.Core curve found | expression support can represent it | no existing CAD consumer | calibration through generic expression-backed curve |
| Profile/path curves | public/internal 2D line/arc/profile records, bounded by endpoints | family-specific evaluation; no shared 3D jet | Firmament Profiles and path authoring | audited only; no broad refactor |
| Panel semantic edges | public exact binding plus separate support/start/end/direction | local family switch in `PanelEdgeIr.Evaluate`; assembly had another switch | Panel mates and assembly G0 validation | `AuthoredCurve` is the directed public seam |
| Pipe centerline | public line/arc/line route elements | geometry recomputed in lowering; no common curve contract | Piping exact materializer and semantics | stable ordered `CenterlineCurves` while route retains ownership |
| STEP / SurfaceMeshIR / Drawing | imported/bound B-rep curve supports | several family switches and sampling paths | serialization, display, projection | consume adapters opportunistically later; no M1-wide rewrite |

Duplicated evaluation logic was confirmed in `PanelEdgeIr.Evaluate`, Firmament assembly Panel-mate evaluation, B-rep tessellation/sampling, SurfaceMeshIR, Drawing projection, and Piping centerline construction. M1 removes the Panel-local switch and defines the shared seam. The other systems remain unchanged to avoid an unrelated refactor; they can adopt the public adapter when their topology/materialization boundary is deliberately revised.
