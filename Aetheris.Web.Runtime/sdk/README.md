# @aetheris/cad

Browser-embeddable Aetheris CAD. The package includes the .NET WebAssembly runtime and uses the same Firmament, B-rep, tessellation, assembly, and AP242 authorities as native Aetheris.

```js
import { Aetheris } from '@aetheris/cad';
const cad = await Aetheris.create();
const { model, diagnostics } = await cad.compile(source, { sourceName: 'bracket.firmament' });
```

Vite projects add `aetherisCad()` from `@aetheris/cad/vite` to `vite.config.js`. The plugin serves the runtime during development and copies it into production builds.

X1 runs on the page thread. `worker: true` fails with the typed `worker-unavailable` error; a qualified Worker transport is deferred rather than silently changing behavior.

The preview package is AGPL-3.0-only. Contact the Aetheris project for commercial licensing; this statement is package metadata, not legal advice.
