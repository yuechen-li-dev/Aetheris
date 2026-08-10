import { describe, expect, it } from "vitest";
import {
	ATELIER_VIEWPORT_THEME,
	MONUMENT_VIEWPORT_THEME,
	VIEWPORT_THEME_IDS,
	VIEWPORT_THEME_REGISTRY,
	VIEWPORT_THEMES,
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

	it("registers every curated theme exactly once", () => {
		expect(VIEWPORT_THEMES.map((theme) => theme.id)).toEqual(VIEWPORT_THEME_IDS);
		expect(new Set(VIEWPORT_THEMES.map((theme) => theme.id)).size).toBe(VIEWPORT_THEMES.length);
		expect(VIEWPORT_THEME_REGISTRY.size).toBe(VIEWPORT_THEME_IDS.length);
	});

	it("falls back to Atelier for missing or corrupt IDs", () => {
		expect(viewportThemeById("not-a-theme")).toBe(ATELIER_VIEWPORT_THEME);
		expect(viewportThemeById(null)).toBe(ATELIER_VIEWPORT_THEME);
	});

	it("keeps every theme static by default and selection-readable", () => {
		for (const theme of VIEWPORT_THEMES) {
			expect(theme.animated).toBe(false);
			expect(theme.edgeStyle.selectedWidth).toBeGreaterThan(theme.edgeStyle.width);
			expect(theme.annotation.selected).not.toBe(theme.annotation.background);
		}
	});
});
