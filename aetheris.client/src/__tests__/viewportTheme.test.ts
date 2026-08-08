import { describe, expect, it } from "vitest";
import {
	ATELIER_VIEWPORT_THEME,
	MONUMENT_VIEWPORT_THEME,
	viewportThemeById,
} from "../viewer/viewportTheme";

describe("viewport themes", () => {
	it("switches complete renderer configurations without changing shell tokens", () => {
		expect(viewportThemeById("atelier")).toBe(ATELIER_VIEWPORT_THEME);
		expect(viewportThemeById("monument")).toBe(MONUMENT_VIEWPORT_THEME);
		expect(MONUMENT_VIEWPORT_THEME.sceneBackground).not.toBe(
			ATELIER_VIEWPORT_THEME.sceneBackground,
		);
		expect(MONUMENT_VIEWPORT_THEME.gridStyle.maxLinesPerAxis).toBeGreaterThan(0);
		expect(MONUMENT_VIEWPORT_THEME.cameraPresentation).toEqual(
			ATELIER_VIEWPORT_THEME.cameraPresentation,
		);
	});
});
