import { readFile, readdir, mkdir, copyFile, writeFile, realpath } from "node:fs/promises";
import { join, resolve } from "node:path";
import { createRequire } from "node:module";
import { createHash } from "node:crypto";
const [root, stage, assetsPath] = process.argv.slice(2).map(path => resolve(path));
const output = join(stage, "licenses");
await mkdir(output, { recursive: true });
const notices: string[] = [];
const unresolved: string[] = [];
const fetched = new Map<string, string>();
const bundle = JSON.parse(await readFile(join(stage, "wwwroot/bundled-packages.json"), "utf8")) as
  { packages: { name: string; version: string }[] };
const bundled = new Set(bundle.packages.map(pkg => `${pkg.name}@${pkg.version}`));
const excludedInstalledPackages: string[] = [];
const auditedFrontend = new Set<string>();
const supplements = JSON.parse(await readFile(join(root, "Aetheris.Cadmata.Desktop/licenses/supplemental-sources.json"), "utf8")) as
  { identity: string; file: string; url: string; sha256: string }[];
async function supplemental(identity: string, target: string): Promise<boolean> {
  const source = supplements.find(item => item.identity === identity);
  if (!source) return false;
  const file = join(root, "Aetheris.Cadmata.Desktop/licenses", source.file);
  const bytes = await readFile(file);
  if (createHash("sha256").update(bytes).digest("hex") !== source.sha256) throw new Error("Supplemental license hash mismatch: " + identity);
  await copyFile(file, join(target, "LICENSE.txt"));
  await writeFile(join(target, "UPSTREAM-LICENSE-SOURCE.txt"), source.url + "\nSHA256 " + source.sha256 + "\n");
  notices.push(identity + ": checked-in upstream license, source and hash supplied");
  return true;
}
async function collect(directory: string, identity: string) {
  auditedFrontend.add(identity);
  const files = await readdir(directory);
  const matches = files.filter((name) => /^(license|licence|copying|notice|third.?party.?notices)([.-]|$)/i.test(name));
  const target = join(output, identity.replace(/[^\w.-]/g, "_"));
  await mkdir(target, { recursive: true });
  for (const name of matches) await copyFile(join(directory, name), join(target, name));
  notices.push(`${identity}: ${matches.length ? matches.join(", ") : "license metadata below"}`);
  const packageJson = join(directory, "package.json");
  try {
    const metadata = JSON.parse(await readFile(packageJson, "utf8"));
    await writeFile(join(target, "package-license.json"), JSON.stringify({ name: metadata.name, version: metadata.version,
      license: metadata.license, repository: metadata.repository }, null, 2));
    if (!matches.length && !metadata.name?.startsWith("@aetheris/") && !await supplemental(identity, target))
      unresolved.push(identity + ": npm package omits license text");
  } catch (error) { if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error; }
}
// Preserve the installed runtime dependency/peer closure. Build tools and their
// platform executables are not distributed in Cadmata and need no ZIP payload.
const seen = new Set<string>();
async function visit(directory: string, includeOwnNotice = true) {
  const canonical = await realpath(directory);
  if (seen.has(canonical)) return;
  seen.add(canonical);
  const metadata = JSON.parse(await readFile(join(directory, "package.json"), "utf8"));
  const identity = `${metadata.name}@${metadata.version}`;
  if (includeOwnNotice) {
    if (bundled.has(identity) || metadata.name.startsWith("@aetheris/")) await collect(directory, identity);
    else excludedInstalledPackages.push(identity);
  }
  const resolver = createRequire(join(directory, "package.json"));
  for (const name of Object.keys({ ...metadata.dependencies, ...metadata.peerDependencies }).sort()) {
    for (const modules of resolver.resolve.paths(name) ?? []) {
      const candidate = join(modules, name);
      try { await readFile(join(candidate, "package.json")); }
      catch (error) { if ((error as NodeJS.ErrnoException).code === "ENOENT") continue; throw error; }
      await visit(candidate);
      break;
    }
  }
}
await visit(join(root, "aetheris.client"), false);
await visit(join(root, "Aetheris.Web.Runtime/telos"));
for (const identity of bundled) {
  if (!auditedFrontend.has(identity) && !identity.startsWith("@aetheris/"))
    unresolved.push(identity + ": emitted frontend package was not found in the notice dependency graph");
}
const assets = JSON.parse(await readFile(assetsPath, "utf8"));
for (const [identity, library] of Object.entries(assets.libraries) as [string, { type: string; path: string }][]) {
  if (library.type !== "package") continue;
  const directory = join(Object.keys(assets.packageFolders)[0], library.path);
  await collect(directory, `nuget-${identity}`);
  const nuspec = (await readdir(directory)).find(name => name.endsWith(".nuspec"));
  if (nuspec) {
    const target = join(output, `nuget-${identity}`.replace(/[^\w.-]/g,"_"));
    await copyFile(join(directory, nuspec), join(target, nuspec));
    const metadata = await readFile(join(directory, nuspec), "utf8");
    if (!(await readdir(target)).some(name => /^(license|licence|copying)([.-]|$)/i.test(name))) {
      const repository = metadata.match(/<repository[^>]*url="https:\/\/github.com\/([^"\s]+)"[^>]*commit="([0-9a-f]+)"/);
      let found = await supplemental(identity, target);
      if (!found && repository) {
        const key = repository[1].replace(/\.git$/, "") + "/" + repository[2];
        let license = fetched.get(key);
        if (!license) {
          for (const name of ["LICENSE", "LICENSE.txt", "LICENSE.TXT", "LICENSE.md", "License.txt"]) {
            const url = `https://raw.githubusercontent.com/${key}/${name}`;
            const response = await fetch(url);
            if (!response.ok) continue;
            license = await response.text();
            fetched.set(key, license);
            await writeFile(join(target, "UPSTREAM-LICENSE-SOURCE.txt"), url + "\n");
            break;
          }
        }
        if (license) { await writeFile(join(target, "LICENSE.txt"), license); found = true; }
      }
      if (!found) unresolved.push(identity + ": no supplied license text or resolvable pinned source license");
    }
  }
}
await writeFile(join(output, "INDEX.txt"), notices.sort().join("\n"));
await writeFile(join(output, "AUDIT.json"), JSON.stringify({ redistributionNoticesComplete: unresolved.length === 0,
  frontendInventory: "../wwwroot/bundled-packages.json", excludedInstalledPackages: [...new Set(excludedInstalledPackages)].sort(), unresolved }, null, 2));
if (unresolved.length) throw new Error("Release notice audit incomplete:\n" + unresolved.join("\n"));
