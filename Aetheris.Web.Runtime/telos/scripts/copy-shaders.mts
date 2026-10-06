import { cp, mkdir, rm } from "node:fs/promises";

const destination = process.argv[2];
if (!destination) throw new Error("Pass the TypeScript output directory.");
await mkdir(`${destination}/shaders`, { recursive: true });
await cp(new URL("../src/shaders/", import.meta.url), `${destination}/shaders`, {
  recursive: true,
});
// Remove output from the former embedded-source module after incremental builds.
await rm(`${destination}/shaders.js`, { force: true });
await rm(`${destination}/shaders.d.ts`, { force: true });
