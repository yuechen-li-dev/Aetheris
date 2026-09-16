# Editor integration

`compile` creates a transactional `ModelSession`. A successful snapshot contains a normalized tree, editable source-backed properties, display mesh, diagnostics, revision, and coarse changeset. A failed rebuild leaves the last successful snapshot and revision usable.

Property editing is deliberately bounded. X1 recognizes scalar length arguments in a template specialization and the width, height, thickness, and first hole diameter of the canonical box-with-hole form. `setProperty` records an effective source-span override while deliberately leaving `model.source` unchanged, so a source editor and property grid can temporarily differ. Treat the property grid as the effective value view, or patch the editor text separately when immediate textual synchronization is required. `setSource` replaces the source and clears all property overrides; use it for arbitrary authoring changes.

Each mesh definition contains `Float64Array` positions/normals, `Uint32Array` indices, and triangle ranges with B-rep face and semantic IDs. Occurrences preserve hierarchy and 4×4 transforms. Use `resolveSelection(definitionId, triangleIndex, occurrenceId?)` for viewport-to-model selection and `selectionForEntity(id)` for tree-to-viewport highlighting.

For Three.js, create one `BufferGeometry` per definition and one `Mesh` per occurrence:

```ts
const geometry = new THREE.BufferGeometry();
// The SDK preserves kernel precision with Float64Array. WebGL vertex attributes
// require a supported GPU format, so convert at the renderer boundary.
geometry.setAttribute("position", new THREE.BufferAttribute(Float32Array.from(definition.positions), 3));
geometry.setAttribute("normal", new THREE.BufferAttribute(Float32Array.from(definition.normals), 3));
geometry.setIndex(new THREE.BufferAttribute(definition.indices, 1));
const object = new THREE.Mesh(geometry, material);
object.matrix.fromArray(occurrence.transform);
object.matrixAutoUpdate = false;
```

`setProperty`, `setSource`, and `rebuild` are serialized per session. Abort signals reject before an operation begins; mid-kernel cancellation is not supported in X1. The Vite plugin copies runtime assets for offline deployment. CSP must allow same-origin WASM compilation and module scripts; no generated JavaScript or `eval` is used by the SDK.
