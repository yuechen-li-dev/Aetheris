# WEB-SDK-X1 release report

## Executive verdict

**Accepted.** An external developer can install the local `@aetheris/cad` package and, through public TypeScript APIs and documentation alone, build a basic semantic browser CAD editor. The qualified path compiles in-memory Firmament parts and assemblies in Chromium, exposes model trees and editable properties, renders typed-array mesh data, maps selection in both directions, rebuilds transactionally, and exports STEP bytes. Worker execution remains unavailable and Firefox remains unqualified; both are reported limitations rather than hidden parity claims.

## Runtime and parity audit

The browser host invokes `FirmamentBuildAndExport.CompileSource`, `Step242Importer`, `BrepDisplayTessellator`, `AssemblyM1Pipeline`, `AssemblyDisplayMeshExporter`, and `AssemblyIrAp242Exporter`. It does not implement Firmament, B-rep, tessellation, or assembly semantics in JavaScript. Source, STEP, and mesh stay in memory. Browser tessellation uses the existing synchronous tessellator because its optional native timeout wrapper blocks on monitors, which browser WASM forbids.

Core compilation, solid modeling, tessellation, and AP242 work in browser WASM. Filesystem-driven external parts, Forge subprocesses, SQLite material lookup, sheet metal, FEA, and mid-operation cancellation are excluded. The linked SQLite dependency reports unsupported native vararg entry points, so the public capability reports SQLite as unavailable.

## Public editor contract

The public surface is `Aetheris.create/info/capabilities/compile/dispose` and `ModelSession` snapshot, inspection, source/property editing, rebuild, bidirectional selection, STEP export, and disposal. Diagnostics and errors are normalized. Revisions are monotonic after successful builds; failed rebuilds retain the prior snapshot. Mesh definitions preserve sharing; occurrences carry hierarchy and transforms; face ranges map triangles to face and semantic identity.

## Package and consumer evidence

The reproducible command is `npm run build` in `Aetheris.Web.Runtime/sdk`. It builds browser WASM and copies runtime, ESM facade, Worker module, declarations, and Vite integration into `dist`; `npm pack` produces `@aetheris/cad` 2.0.0-preview.3. The final measured tarball is 33,072,478 bytes and unpacked package is 61,940,213 bytes across 446 entries. The runtime contains 202 WASM modules totaling 40,965,315 bytes; the largest are the 5,589,785-byte Firmament kernel, 4,869,913-byte core library, and 3,964,206-byte native runtime. Optimization is outstanding.

A fresh project outside the checkout installed the tarball and Vite, built successfully with no repository-relative paths, and ran headless Chromium. Its direct-runtime witness initialized in 392.9 ms (warm local server/browser conditions), compiled the canonical bracket, exposed 2 tree nodes, 4 properties, 1 mesh definition, 1 occurrence, and 1,808 triangles, resolved triangle 0 to `face:1` / `Plate.CenterMount`, changed X bounds from ±25 mm to ±40 mm after Width 50→80 mm, and exported 7,294-byte STEP before and after editing. A zero-thickness source edit produced structured diagnostics and retained revision 2; recovery succeeded.

Two independent fresh agents were given only the public web-SDK docs, packed tarball, and canonical Firmament fixture; neither inspected Aetheris internals or reference-editor source. One was asked for a distinct source-textarea frontend with viewport, parameter table, rebuild, and selection; it built a Vite/TypeScript editor with canvas projection, property table, bidirectional selection demonstration, and STEP export. Real Chromium WASM reached revision 4, selected `Plate.CenterMount` / `face:1`, and exported 7,338 bytes. The other received the mandatory model-tree, Three.js viewport, selection, property-inspector, width-edit, and STEP-download prompt. It built that editor independently; headless Edge passed with no console or page errors, selected `Plate.CenterMount`, rebuilt Width 50→72 at revision 2, and downloaded a valid 7,294-byte STEP file. Their friction findings tightened `EditableProperty.unit` to the public `Unit` union, documented property-override/source synchronization, and corrected the Three.js sample to convert kernel `Float64Array` coordinates to WebGL-compatible `Float32Array` at the rendering boundary.

The browser assembly witness compiled `shared-block-assembly.firmament` through the assembly pipeline with no diagnostics. Its revision-1 snapshot used schema `aetheris/assembly-display-mesh/1`, exposed 3 tree nodes, 2 shared definitions, and 3 hierarchy occurrences, resolved a triangle to a semantic occurrence and B-rep face, and exported 7,532 STEP bytes.

Native CLI build and browser WASM produced the identical baseline STEP SHA-256 `1AF5CD0E2A44F6FF509390DDD01E7C38943168522B9138AEA91C91B1AE7B3928`. Native reinspection found one enclosed-manifold body, 7 faces, 15 edges, 12 vertices, bounds `[-25,-20,0]` to `[25,20,8]` mm, six planes, and one cylinder. The edited browser STEP changed deterministically to SHA-256 `14C9BF8BF2FAA6CC3F2469D5E7D93F8F4EAED2ABFD766F13BD22DE040DA16FEA`.

## Rebuild, disposal, and concurrency

Session mutations are queued deterministically. The Chromium witness completed 100 repeated rebuilds, reached revision 103 after edit/failure/recovery, and then disposed the model and runtime. Observed `usedJSHeapSize` rose from 16,128,076 to 16,850,845 bytes (722,769 bytes, about 4.5%); this is bounded smoke evidence, not a cross-browser leak guarantee. Abort signals are honored before dispatch only. Worker initialization did not complete in Chromium, so `worker: true` now fails with `worker-unavailable` and capability discovery reports false.

The full serial Release solution build passed. The full solution test command passed 3,606 tests across 18 discovered suites; `Aetheris.FrictionLab.Tests` currently contains no discoverable tests.

## Licensing

Package and WASM metadata use `AGPL-3.0-only`, consistent with repository defaults. Commercial-use implications require the Aetheris license owner and, where appropriate, legal counsel; this report makes no additional legal conclusion.
