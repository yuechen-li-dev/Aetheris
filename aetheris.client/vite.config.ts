import { fileURLToPath, URL } from "node:url";

import { defineConfig, type Plugin } from "vite";
import plugin from "@vitejs/plugin-react";
import fs from "fs";
import path from "path";
import child_process from "child_process";
import { env } from "process";

// Audit emitted code, including the lazy WebGL fallback, rather than treating
// every installed optional peer as a redistributed dependency.
function bundledPackageInventory(): Plugin {
	return {
		name: "cadmata-bundled-package-inventory",
		generateBundle(_options, bundle) {
			const packages = new Map<string, { name: string; version: string; modules: string[] }>();
			for (const output of Object.values(bundle)) {
				if (output.type !== "chunk") continue;
				for (const [id, module] of Object.entries(output.modules)) {
					if (!module.renderedLength || !id.includes("node_modules")) continue;
					let directory = path.dirname(id.split("?")[0]);
					while (directory !== path.dirname(directory)) {
						const file = path.join(directory, "package.json");
						if (fs.existsSync(file)) {
							const metadata = JSON.parse(fs.readFileSync(file, "utf8"));
							if (metadata.name && metadata.version) {
								const identity = `${metadata.name}@${metadata.version}`;
								const entry = packages.get(identity) ?? { name: metadata.name, version: metadata.version, modules: [] as string[] };
								entry.modules.push(path.relative(directory, id.split("?")[0]).replaceAll("\\", "/"));
								packages.set(identity, entry);
								break;
							}
						}
						directory = path.dirname(directory);
					}
				}
			}
			this.emitFile({ type: "asset", fileName: "bundled-packages.json", source: JSON.stringify({
				basis: "Rollup emitted chunks; modules with nonzero renderedLength, including lazy chunks",
				packages: [...packages.values()].sort((a, b) => a.name.localeCompare(b.name)).map(entry => ({
					...entry, modules: [...new Set(entry.modules)].sort(),
				})),
			}, null, 2) });
		},
	};
}

function ensureCertificates() {
	const baseFolder =
		env.APPDATA !== undefined && env.APPDATA !== ""
			? `${env.APPDATA}/ASP.NET/https`
			: `${env.HOME}/.aspnet/https`;

	const certificateName = "aetheris.client";
	const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
	const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

	if (!fs.existsSync(baseFolder)) {
		fs.mkdirSync(baseFolder, { recursive: true });
	}

	if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
		const certificateResult = child_process.spawnSync(
			"dotnet",
			["dev-certs", "https", "--export-path", certFilePath, "--format", "Pem", "--no-password"],
			{ stdio: "inherit" },
		);

		if (certificateResult.status !== 0) {
			throw new Error("Could not create certificate.");
		}
	}

	return {
		key: fs.readFileSync(keyFilePath),
		cert: fs.readFileSync(certFilePath),
	};
}

const target = env.ASPNETCORE_HTTPS_PORT
	? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
	: env.ASPNETCORE_URLS
		? env.ASPNETCORE_URLS.split(";")[0]
		: "https://localhost:7145";

export default defineConfig(({ command }) => ({
	plugins: [plugin(), bundledPackageInventory()],
	resolve: {
		alias: {
			"@": fileURLToPath(new URL("./src", import.meta.url)),
		},
	},
	build: {
		assetsInlineLimit: (filePath) => filePath.endsWith(".wgsl") ? false : undefined,
		// The rendering core is intentionally a single cached vendor chunk. Its
		// measured production size is 724 KB; retain a small budget above that
		// rather than accepting Vite's generic 500 KB advisory.
		chunkSizeWarningLimit: 750,
		rollupOptions: {
			output: {
				manualChunks: {
					three: ["three"],
				},
			},
		},
	},
	server:
		command === "serve"
			? {
					fs: {
						allow: [
							fileURLToPath(new URL(".", import.meta.url)),
							fileURLToPath(new URL("../Aetheris.Web.Runtime/telos", import.meta.url)),
							...(env.AETHERIS_TELOS_WITNESS === "1" ? [
								"../fixtures/three-telos",
								"../docs/development/milestones/modules/sheetmetal/artifacts/ctc03-manufacturing-release",
								"../artifacts/local/three-telos/cir",
							].map(value => fileURLToPath(new URL(value, import.meta.url))) : []),
						],
					},
					proxy: {
						"^/api": {
							target,
							secure: false,
						},
					},
					port: 5173,
					strictPort: true,
					// The packaged app remains HTTPS by default. Browser-driven localhost
					// validation may opt into HTTP when the automation surface cannot trust
					// the developer certificate.
					https: env.AETHERIS_VITE_HTTP === "1" ? undefined : ensureCertificates(),
				}
			: undefined,
}));
