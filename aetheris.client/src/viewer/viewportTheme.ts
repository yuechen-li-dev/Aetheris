export interface ViewportTheme {
	id: "atelier" | "monument";
	label: string;
	sceneBackground: string;
	objectMaterial: { color: string; roughness: number; metalness: number };
	selectedMaterial: { color: string; emissive: string; emissiveIntensity: number };
	edgeStyle: { color: string; width: number; selectedColor: string; selectedWidth: number };
	gridStyle: {
		minorColor: string;
		majorColor: string;
		minorOpacity: number;
		majorOpacity: number;
		majorStep: number;
		targetCellCount: number;
		maxLinesPerAxis: number;
		extentScale: number;
		yOffset: number;
	};
	lights: {
		ambient: number;
		hemisphereSky: string;
		hemisphereGround: string;
		hemisphereIntensity: number;
		keyColor: string;
		keyIntensity: number;
		keyPosition: readonly [number, number, number];
		fillColor: string;
		fillIntensity: number;
		fillPosition: readonly [number, number, number];
	};
	shadowStyle: { enabled: boolean; opacity: number };
	environment: { toneMappingExposure: number };
	fog: { enabled: boolean; color: string; near: number; far: number };
	postProcess: { outline: boolean; colorGrade: "neutral" | "pastel" };
	cameraPresentation: { position: readonly [number, number, number]; zoom: number };
	axis: { x: string; y: string; z: string; label: string };
}

export const ATELIER_VIEWPORT_THEME: ViewportTheme = {
	id: "atelier",
	label: "Atelier",
	sceneBackground: "#252725",
	objectMaterial: { color: "#bbb7aa", roughness: 0.72, metalness: 0.03 },
	selectedMaterial: { color: "#d4bd76", emissive: "#574719", emissiveIntensity: 0.16 },
	edgeStyle: { color: "#24231f", width: 1.2, selectedColor: "#f0cf68", selectedWidth: 3 },
	gridStyle: {
		minorColor: "#76766f",
		majorColor: "#aaa69a",
		minorOpacity: 0.18,
		majorOpacity: 0.34,
		majorStep: 5,
		targetCellCount: 14,
		maxLinesPerAxis: 48,
		extentScale: 1.35,
		yOffset: 0.001,
	},
	lights: {
		ambient: 0.1,
		hemisphereSky: "#d9ded8",
		hemisphereGround: "#4b463c",
		hemisphereIntensity: 0.72,
		keyColor: "#fff4dc",
		keyIntensity: 1.8,
		keyPosition: [-6, 10, 7],
		fillColor: "#a9bac0",
		fillIntensity: 0.48,
		fillPosition: [7, 3, -5],
	},
	shadowStyle: { enabled: false, opacity: 0.18 },
	environment: { toneMappingExposure: 1.05 },
	fog: { enabled: false, color: "#252725", near: 30, far: 120 },
	postProcess: { outline: false, colorGrade: "neutral" },
	cameraPresentation: { position: [6, 6, 6], zoom: 90 },
	axis: { x: "#a46757", y: "#6f8b64", z: "#b39b62", label: "#cbc7ba" },
};

export const MONUMENT_VIEWPORT_THEME: ViewportTheme = {
	...ATELIER_VIEWPORT_THEME,
	id: "monument",
	label: "Monument",
	sceneBackground: "#d9cfbd",
	objectMaterial: { color: "#d98978", roughness: 0.9, metalness: 0 },
	selectedMaterial: { color: "#f4d86e", emissive: "#704b20", emissiveIntensity: 0.1 },
	edgeStyle: { color: "#725d57", width: 1, selectedColor: "#3d756f", selectedWidth: 3.2 },
	gridStyle: {
		...ATELIER_VIEWPORT_THEME.gridStyle,
		minorColor: "#a79c8b",
		majorColor: "#817665",
		minorOpacity: 0.12,
		majorOpacity: 0.24,
	},
	lights: {
		...ATELIER_VIEWPORT_THEME.lights,
		hemisphereSky: "#fff3d8",
		hemisphereGround: "#b9a6a0",
		hemisphereIntensity: 1.1,
		keyColor: "#fff1c3",
		keyIntensity: 1.55,
		fillColor: "#8fc3bb",
		fillIntensity: 0.38,
	},
	environment: { toneMappingExposure: 1.12 },
	fog: { enabled: true, color: "#d9cfbd", near: 34, far: 115 },
	postProcess: { outline: false, colorGrade: "pastel" },
	axis: { x: "#a2535c", y: "#528378", z: "#9e7d45", label: "#5f554c" },
};

export const VIEWPORT_THEMES = [ATELIER_VIEWPORT_THEME, MONUMENT_VIEWPORT_THEME] as const;

export function viewportThemeById(id: ViewportTheme["id"]): ViewportTheme {
	return VIEWPORT_THEMES.find((theme) => theme.id === id) ?? ATELIER_VIEWPORT_THEME;
}
