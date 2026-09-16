# Aetheris Web SDK getting started

Install the preview package and configure Vite to copy and serve its self-contained runtime:

```sh
npm install @aetheris/cad
```

```js
// vite.config.js
import { defineConfig } from "vite";
import { aetherisCad } from "@aetheris/cad/vite";
export default defineConfig({ plugins: [aetherisCad()] });
```

```ts
import { Aetheris } from "@aetheris/cad";

const cad = await Aetheris.create();
const source = `Model Plate {
  Units: mm
  Box Body { Size: [50mm, 40mm, 8mm] }
  Modify Body { Hole<Shaft> Mount { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll } }
}`;
const { model, diagnostics } = await cad.compile(source, { sourceName: "plate.firmament" });
if (!model) throw new Error(diagnostics.map(item => item.message).join("\n"));

console.log(model.tree, model.properties, model.mesh);
const width = model.properties.find(item => item.name === "Width")!;
await model.setProperty(width.id, { value: 80, unit: "mm" });
const step = await model.exportSTEP();
const download = URL.createObjectURL(new Blob([step], { type: "model/step" }));
```

Call `model.dispose()` and `cad.dispose()` when finished. X1 runs on the page thread. `worker: true` is rejected with `worker-unavailable` until that transport is qualified.

The package is AGPL-3.0-only in X1. Contact the Aetheris project about commercial licensing; obtain legal advice for your distribution model.
