import { M } from "machinalayout/machina";

export type CadmataShellRegion =
	| "cadmata-shell"
	| "command-area"
	| "workspace"
	| "viewport"
	| "inspector";

// Machina is the structural authoring source for the long-lived shell. React
// retains semantic DOM ownership; these lowered rows make the same structure
// inspectable to tests, diagnostics, and future responsive layout adapters.
export const CADMATA_SHELL = M.root(
	"cadmata-shell",
	{ arrange: M.stackArrange("vertical", { gap: 0 }) },
	[
		M.fixed("command-area", 136, { view: "CommandArea" }),
		M.hstack("workspace", { gap: 0, view: "Workspace" }, [
			M.fill("viewport", 1, { view: "Viewport" }),
			M.fixed("inspector", 360, { view: "Inspector" }),
		]),
	],
);

export const CADMATA_SHELL_ROWS = M.rows(CADMATA_SHELL);

const regionIds = new Set(CADMATA_SHELL_ROWS.map((row) => row.id));

export function machinaRegion(region: CadmataShellRegion) {
	if (!regionIds.has(region)) throw new Error(`Unknown Cadmata shell region '${region}'.`);
	return {
		"data-machina-node-id": region,
		"data-machina-layout": "cadmata-application-shell",
	} as const;
}
