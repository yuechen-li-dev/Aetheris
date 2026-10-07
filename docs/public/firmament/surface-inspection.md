# Surface and trim inspection

Choose a practical [viewport theme](viewport-themes.md) for inspection, or a presentation theme for sharing the same model.

Cadmata and Helios expose **Surface / trim inspection** in the WebGPU viewport. It compares the displayed geometry with source BRep curves using the same camera, occurrence transforms and renderer. Orbit, pan and zoom work throughout inspection.

Choose **Surfaces only** to see the filled mesh or field, **BRep wire only** to see source curves, **Translucent + wire** to expose curves through the surface, or **Face colours + wire** to distinguish adjacent source faces. Red lines are BRep edges, including trim boundaries where transported; they are not triangle edges. These views help distinguish filled holes, excess surface coverage, long facets and mismatched boundaries.

Choose a face in **Isolated display face**, or click a surface while an inspection view is active. Isolation uses occurrence plus source face identity, so repeated parts remain distinguishable. Choose **All faces** to restore the complete model. Cadmata additionally shows the source STEP entity, support family and face boundary edges when available. Changing the view or isolated face preserves the camera; loading a new model resets isolation.

Helios SDK packets currently carry source face ranges and occurrence edges without face-edge adjacency. Its isolated wire therefore shows all edges of that occurrence, and the control states this limitation. Cadmata's richer imported-face packet can restrict wire to the isolated face's boundary edges.

Whole-model surface and translucent views retain the actual CIR/SDF field when active. A field shader has no per-face trim mask. Face colours and face isolation explicitly show its retained BRep mesh proxy, with a notice in Helios. Proxy pixels must not be mistaken for proof of the field's silhouette. Compare the whole field with the proxy to investigate a discrepancy.

The reusable API is exported by `@aetheris/three-telos`:

```ts
import { inspectSurfaces, surfaceInspectionFaces } from "@aetheris/three-telos";

const faces = surfaceInspectionFaces(sourceScene);
host.setScene(inspectSurfaces(sourceScene, "overlay", faces[0]));
// Use the same host/camera for the next comparison; do not call fit here.
host.setScene(inspectSurfaces(sourceScene, "wire", faces[0]));
host.setScene(inspectSurfaces(sourceScene, "normal"));
```

Projection keeps engineering authority in the source scene. Packed faces share the complete immutable geometry and GPU buffers; triangle draw ranges preserve original triangle indices for source selection. It never reconstructs BRep edges from tessellation or changes export geometry. Products own controls, source metadata and screenshots; Three Telos owns drawing, camera and picking.

For reproducible corpus review, `scripts/inspect-step-legacy-cadmata.mts` captures five matched-camera views, face inventories and optional orbit/zoom/source-face click witnesses through real STEP upload. Outputs belong under ignored `artifacts/local/`. The canvas `data-surface-inspection` and `data-surface-inspection-pick` attributes expose the active projection and actual hit for automation. Contact sheets are review evidence, not engineering qualification or human signoff. See [X4 qualification](../../release/STEP-TRIM-TRIANGULATION-X4.md).
