import { readFileSync, existsSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

const [host, requestPath, outputDirectory] = process.argv.slice(2);
const execution = spawnSync(host, [
  "invoke", "Standard.SheetMetal.ElectronicsEnclosure", "--request", "-", "--out", outputDirectory,
], { input: readFileSync(requestPath), encoding: "utf8" });
const result = JSON.parse(execution.stdout);
if (execution.status !== 0 || !result.success) throw new Error(execution.stdout || execution.stderr);
for (const artifact of result.artifacts) {
  if (!existsSync(join(outputDirectory, artifact.path))) throw new Error(`Missing ${artifact.path}`);
}
process.stdout.write(JSON.stringify(result));
