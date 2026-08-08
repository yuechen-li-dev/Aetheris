import {
	CompatFiles,
	JsonFile,
	Package,
	Policies,
	RunTargets,
	Security,
	Targets,
	Tools,
	TsConfig,
	VSCode,
	Workspace,
	define,
	defineDeps,
	dep,
	npm,
	tool,
	type BoundaryPolicy,
	type TypePolicy,
} from "tspack/manifest";

const deps = defineDeps({
	react: dep(npm("react", "^19.2.0")),
	reactDom: dep(npm("react-dom", "^19.2.0"), { key: "react-dom" }),
	three: dep(npm("three", "^0.183.1")),
	fiber: dep(npm("@react-three/fiber", "^9.5.0"), { key: "@react-three/fiber" }),
	drei: dep(npm("@react-three/drei", "^10.7.7"), { key: "@react-three/drei" }),
	machinalayout: dep(npm("machinalayout", "^0.7.0")),
	cva: dep(npm("class-variance-authority", "^0.7.1"), { key: "class-variance-authority" }),
	clsx: dep(npm("clsx", "^2.1.1")),
	tailwindMerge: dep(npm("tailwind-merge", "^3.5.0"), { key: "tailwind-merge" }),
	zod: dep(npm("zod", "^3.25.76")),
	typescript: tool(npm("typescript", "~5.9.3")),
	vite: tool(npm("vite", "^7.3.1")),
	viteReact: tool(npm("@vitejs/plugin-react", "^5.1.1"), { key: "@vitejs/plugin-react" }),
	vitest: tool(npm("vitest", "^4.0.18")),
	jsdom: tool(npm("jsdom", "^28.1.0")),
	testingLibraryReact: tool(npm("@testing-library/react", "^16.3.2"), {
		key: "@testing-library/react",
	}),
	testingLibraryDom: tool(npm("@testing-library/dom", "^10.4.1"), {
		key: "@testing-library/dom",
	}),
	testingLibraryJestDom: tool(npm("@testing-library/jest-dom", "^6.9.1"), {
		key: "@testing-library/jest-dom",
	}),
	reactTypes: tool(npm("@types/react", "^19.2.7"), { key: "@types/react" }),
	reactDomTypes: tool(npm("@types/react-dom", "^19.2.3"), { key: "@types/react-dom" }),
	threeTypes: tool(npm("@types/three", "^0.183.1"), { key: "@types/three" }),
	nodeTypes: tool(npm("@types/node", "^24.10.1"), { key: "@types/node" }),
	eslint: tool(npm("eslint", "^9.39.1")),
	eslintJs: tool(npm("@eslint/js", "^9.39.1"), { key: "@eslint/js" }),
	typescriptEslint: tool(npm("typescript-eslint", "^8.48.0"), { key: "typescriptEslint" }),
	reactHooks: tool(npm("eslint-plugin-react-hooks", "^7.0.1"), {
		key: "eslint-plugin-react-hooks",
	}),
	reactRefresh: tool(npm("eslint-plugin-react-refresh", "^0.4.24"), {
		key: "eslint-plugin-react-refresh",
	}),
	globals: tool(npm("globals", "^16.5.0")),
	tailwind: tool(npm("tailwindcss", "^3.4.17"), { key: "tailwind" }),
	tailwindAnimate: tool(npm("tailwindcss-animate", "^1.0.7"), { key: "tailwindcss-animate" }),
	postcss: tool(npm("postcss", "^8.5.8")),
	autoprefixer: tool(npm("autoprefixer", "^10.4.27")),
	biome: tool(npm("@biomejs/biome", "^2.4.15"), { key: "@biomejs/biome" }),
});

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

export default define(
	<Workspace name="aetheris-cadmata" runtime="nodejs">
		<Package
			name="aetheris.client"
			version="0.0.0-preview.2"
			kind="app"
			license="AGPL-3.0-or-later"
			dependencies={{
				values: [
					deps.react,
					deps.reactDom,
					deps.three,
					deps.fiber,
					deps.drei,
					deps.machinalayout,
					deps.cva,
					deps.clsx,
					deps.tailwindMerge,
					deps.zod,
					deps.typescript,
					deps.vite,
					deps.viteReact,
					deps.vitest,
					deps.jsdom,
					deps.testingLibraryReact,
					deps.testingLibraryDom,
					deps.testingLibraryJestDom,
					deps.reactTypes,
					deps.reactDomTypes,
					deps.threeTypes,
					deps.nodeTypes,
					deps.eslint,
					deps.eslintJs,
					deps.typescriptEslint,
					deps.reactHooks,
					deps.reactRefresh,
					deps.globals,
					deps.tailwind,
					deps.tailwindAnimate,
					deps.postcss,
					deps.autoprefixer,
					deps.biome,
				],
			}}
		>
			<Policies types={types} boundaries={boundaries} />
			<Targets
				rows={[
					{
						name: "browser",
						export: ".",
						entry: "src/main.tsx",
						runtime: "dist/assets/index.js",
						types: "",
						javascriptRuntime: "browser",
						deps: [
							deps.react,
							deps.reactDom,
							deps.three,
							deps.fiber,
							deps.drei,
							deps.machinalayout,
							deps.cva,
							deps.clsx,
							deps.tailwindMerge,
							deps.zod,
						],
					},
				]}
			/>
			<Tools
				values={[
					deps.typescript,
					deps.vite,
					deps.viteReact,
					deps.vitest,
					deps.jsdom,
					deps.testingLibraryReact,
					deps.testingLibraryDom,
					deps.testingLibraryJestDom,
					deps.reactTypes,
					deps.reactDomTypes,
					deps.threeTypes,
					deps.nodeTypes,
					deps.eslint,
					deps.eslintJs,
					deps.typescriptEslint,
					deps.reactHooks,
					deps.reactRefresh,
					deps.globals,
					deps.tailwind,
					deps.tailwindAnimate,
					deps.postcss,
					deps.autoprefixer,
					deps.biome,
				]}
			/>
			<RunTargets
				rows={[
					{ name: "dev", runtime: "node", command: ["vite"], url: "https://localhost:5173" },
					{ name: "typecheck", runtime: "node", command: ["tsc", "-b", "--pretty", "false"] },
					{ name: "test", runtime: "node", command: ["vitest", "run"] },
					{
						name: "profile-grid",
						runtime: "node",
						command: [
							"vitest",
							"run",
							"src/__tests__/logarithmicGrid.profile.test.ts",
							"--reporter=verbose",
							"--disableConsoleIntercept",
						],
					},
					{ name: "build", runtime: "node", command: ["vite", "build"] },
					{ name: "lint", runtime: "node", command: ["eslint", "."] },
				]}
			/>
		</Package>
		<CompatFiles>
			<JsonFile path="tsconfig.tspack.json" value={TsConfig.manifestEditor()} />
			<JsonFile path=".vscode/settings.json" value={VSCode.settings()} />
			<JsonFile path=".vscode/extensions.json" value={VSCode.extensions()} />
		</CompatFiles>
		<Security
			acknowledgedLifecycleCategories={[
				{
					category: "consumer-install",
					reason:
						"Vite, Biome, and renderer dependencies select platform binaries; TSPack records but does not execute lifecycle scripts.",
				},
				{
					category: "maintainer-publish",
					reason: "Cadmata is an application and is not published as an npm package.",
				},
			]}
		/>
	</Workspace>,
);
