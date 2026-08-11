import {
  define,
  defineDeps,
  npm,
  Package,
  Policies,
  RunTargets,
  Security,
  tool,
  Tools,
  Workspace,
  type BoundaryPolicy,
  type TypePolicy,
} from "tspack/manifest";

const types = {
  declarations: "optional",
  missingTypes: "error",
  publicTypeLeakage: "warn",
  typeOnlyRuntimeLeakage: "error",
} satisfies TypePolicy;
const boundaries = {
  undeclaredImports: "error",
  phantomDependencies: "error",
  crossTargetImports: "error",
} satisfies BoundaryPolicy;
const deps = defineDeps({
  vscodeTypes: tool(npm("@types/vscode", "^1.90.0"), { key: "@types/vscode" }),
  nodeTypes: tool(npm("@types/node", "^22.0.0"), { key: "@types/node" }),
  typescript: tool(npm("typescript", "^5.9.0")),
  esbuild: tool(npm("esbuild", "^0.25.0")),
  vsce: tool(npm("@vscode/vsce", "^3.6.0"), { key: "@vscode/vsce" }),
  textmate: tool(npm("vscode-textmate", "^9.2.0"), { key: "vscode-textmate" }),
  oniguruma: tool(npm("vscode-oniguruma", "^2.0.1"), { key: "vscode-oniguruma" }),
  biome: tool(npm("@biomejs/biome", "^1.9.4"), { key: "@biomejs/biome" }),
});

export default define(
  <Workspace name="aetheris-firmament" runtime="nodejs">
    <Package
      name="aetheris-firmament"
      version="0.2.0-preview.2"
      kind="app"
      license="MIT"
      dependencies={{
        values: [
          deps.vscodeTypes,
          deps.nodeTypes,
          deps.typescript,
          deps.esbuild,
          deps.vsce,
          deps.textmate,
          deps.oniguruma,
          deps.biome,
        ],
      }}
    >
      <Policies types={types} boundaries={boundaries} />
      <Tools
        values={[
          deps.vscodeTypes,
          deps.nodeTypes,
          deps.typescript,
          deps.esbuild,
          deps.vsce,
          deps.textmate,
          deps.oniguruma,
          deps.biome,
        ]}
      />
      <RunTargets
        rows={[
          { name: "typecheck", runtime: "node", command: ["tsc", "--noEmit"] },
          { name: "test", runtime: "node", command: ["node", "--test", "tests/*.test.ts"] },
          {
            name: "build",
            runtime: "node",
            command: [
              "esbuild",
              "src/extension.ts",
              "--bundle",
              "--platform=node",
              "--format=cjs",
              "--external:vscode",
              "--outfile=dist/extension.cjs",
            ],
          },
          {
            name: "package",
            runtime: "node",
            command: [
              "vsce",
              "package",
              "--no-dependencies",
              "--out",
              "../../artifacts/release/preview2/aetheris-firmament-0.2.0-preview.2.vsix",
            ],
          },
        ]}
      />
    </Package>
    <Security
      acknowledgedLifecycleCategories={[
        {
          category: "consumer-install",
          reason:
            "Tool dependencies may select platform binaries; TSPack records but does not execute lifecycle scripts.",
        },
        {
          category: "maintainer-publish",
          reason: "VSIX packaging is an explicit maintainer command and Marketplace publication is out of scope.",
        },
      ]}
    />
  </Workspace>,
);
