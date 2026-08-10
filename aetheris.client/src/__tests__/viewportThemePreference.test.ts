import { describe, expect, it } from "vitest";
import {
	loadViewportThemePreference,
	saveViewportThemePreference,
	VIEWPORT_THEME_STORAGE_KEY,
} from "../viewer/viewportThemePreference";

function memoryStorage(seed?: string): Storage {
	const values = new Map<string, string>();
	if (seed) values.set(VIEWPORT_THEME_STORAGE_KEY, seed);
	return {
		get length() {
			return values.size;
		},
		clear: () => values.clear(),
		getItem: (key) => values.get(key) ?? null,
		key: (index) => [...values.keys()][index] ?? null,
		removeItem: (key) => values.delete(key),
		setItem: (key, value) => values.set(key, value),
	};
}

describe("viewport theme preference", () => {
	it("uses a valid gallery query before persisted preference", () => {
		expect(loadViewportThemePreference("?theme=sirius", memoryStorage("mars"))).toBe("sirius");
	});

	it("persists valid selections and rejects corrupt storage", () => {
		const storage = memoryStorage("broken");
		expect(loadViewportThemePreference("", storage)).toBe("atelier");
		saveViewportThemePreference("aeons", storage);
		expect(loadViewportThemePreference("", storage)).toBe("aeons");
	});
});
