import { isViewportThemeId, type ViewportThemeId } from "./viewportTheme";

export const VIEWPORT_THEME_STORAGE_KEY = "cadmata.viewport-theme";

export function loadViewportThemePreference(
	locationSearch = typeof window === "undefined" ? "" : window.location.search,
	storage = typeof window === "undefined" ? null : window.localStorage,
): ViewportThemeId {
	const queryTheme = new URLSearchParams(locationSearch).get("theme");
	if (isViewportThemeId(queryTheme)) return queryTheme;
	try {
		const stored = storage?.getItem(VIEWPORT_THEME_STORAGE_KEY);
		return isViewportThemeId(stored) ? stored : "atelier";
	} catch {
		return "atelier";
	}
}

export function saveViewportThemePreference(
	themeId: ViewportThemeId,
	storage = typeof window === "undefined" ? null : window.localStorage,
) {
	try {
		storage?.setItem(VIEWPORT_THEME_STORAGE_KEY, themeId);
	} catch {
		// Storage may be disabled; theme switching remains session-local.
	}
}
