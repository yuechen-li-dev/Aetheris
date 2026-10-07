import { define, Packages, Security, Workspace } from "tspack/manifest";

// Cadmata imports the sibling Telos package. The workspace owns both paths;
// package-local manifests retain the client contract and RunTargets.
export default define(
  <Workspace name="aetheris-cadmata" runtime="nodejs">
    <Packages rows={[
      { name: "aetheris.client", root: "aetheris.client", manifest: "aetheris.client/package.manifest.tsx" },
    ]} />
    <Security acknowledgedLifecycleCategories={[
      { category: "consumer-install", reason: "Vite, Biome and renderer dependencies select platform binaries; lifecycle execution remains blocked." },
      { category: "maintainer-publish", reason: "Cadmata is an application, not an npm publication." },
    ]} />
  </Workspace>,
);
