import { describe, expect, it } from "vitest";
import { CADMATA_SHELL_ROWS, machinaRegion } from "./shellLayout";

describe("Cadmata shell Machina authoring", () => {
	it("lowers the stable command, viewport, and inspector geography", () => {
		expect(CADMATA_SHELL_ROWS.map((row) => [row.id, row.parent])).toEqual([
			["cadmata-shell", undefined],
			["command-area", "cadmata-shell"],
			["workspace", "cadmata-shell"],
			["viewport", "workspace"],
			["inspector", "workspace"],
		]);
	});

	it("provides inspectable attributes only for authored regions", () => {
		expect(machinaRegion("viewport")).toEqual({
			"data-machina-node-id": "viewport",
			"data-machina-layout": "cadmata-application-shell",
		});
	});
});
