export const VIEWPORT_THEME_IDS = [
	"atelier",
	"monument",
	"mars",
	"sirius",
	"singularity",
	"aeons",
] as const;

export type ViewportThemeId = (typeof VIEWPORT_THEME_IDS)[number];

export interface ViewportTheme {
	id: ViewportThemeId;
	label: string;
	description: string;
	category: "workbench" | "architectural" | "planetary" | "stellar" | "cosmic";
	animated: boolean;
	performanceClass: "balanced" | "high";
	sceneBackground: string;
	background: {
		kind: "flat" | "mars" | "sirius" | "singularity" | "aeons";
		fallback: string;
		accent: string;
		intensity: number;
	};
	objectMaterial: { color: string; roughness: number; metalness: number };
	selectedMaterial: { color: string; emissive: string; emissiveIntensity: number };
	edgeStyle: { color: string; width: number; selectedColor: string; selectedWidth: number };
	gridStyle: {
		enabled: boolean;
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
		rimColor: string;
		rimIntensity: number;
		rimPosition: readonly [number, number, number];
	};
	shadowStyle: { enabled: boolean; opacity: number };
	environment: { toneMappingExposure: number };
	fog: { enabled: boolean; color: string; near: number; far: number };
	postProcess: {
		outline: boolean;
		colorGrade: "neutral" | "pastel" | "rust" | "cold" | "high-contrast" | "antique";
		vignette: number;
		bloom: number;
	};
	cameraPresentation: { position: readonly [number, number, number]; zoom: number };
	axis: { x: string; y: string; z: string; label: string };
	annotation: {
		text: string;
		background: string;
		leader: string;
		datum: string;
		dimension: string;
		selected: string;
	};
	overlay: {
		concept: string;
		profile: string;
		compose: string;
		selection: string;
		ancestor: string;
		diagnostic: string;
	};
}

export const ATELIER_VIEWPORT_THEME: ViewportTheme = {
	id: "atelier",
	label: "Atelier",
	description: "Dark graphite workbench with neutral task lighting and crisp technical edges.",
	category: "workbench",
	animated: false,
	performanceClass: "balanced",
	sceneBackground: "#252725",
	background: { kind: "flat", fallback: "#252725", accent: "#62645f", intensity: 0.2 },
	objectMaterial: { color: "#bbb7aa", roughness: 0.72, metalness: 0.03 },
	selectedMaterial: { color: "#d4bd76", emissive: "#574719", emissiveIntensity: 0.16 },
	edgeStyle: { color: "#24231f", width: 1.2, selectedColor: "#f0cf68", selectedWidth: 3 },
	gridStyle: {
		enabled: true,
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
		rimColor: "#a9bac0",
		rimIntensity: 0,
		rimPosition: [0, 2, -8],
	},
	shadowStyle: { enabled: false, opacity: 0.18 },
	environment: { toneMappingExposure: 1.05 },
	fog: { enabled: false, color: "#252725", near: 30, far: 120 },
	postProcess: { outline: false, colorGrade: "neutral", vignette: 0, bloom: 0 },
	cameraPresentation: { position: [6, 6, 6], zoom: 90 },
	axis: { x: "#a46757", y: "#6f8b64", z: "#b39b62", label: "#cbc7ba" },
	annotation: {
		text: "#fff9e8",
		background: "#1b1d1b",
		leader: "#e8d8a2",
		datum: "#8ec8d2",
		dimension: "#f0cf68",
		selected: "#ffffff",
	},
	overlay: {
		concept: "#6ea6b8",
		profile: "#4db6ac",
		compose: "#b48748",
		selection: "#ffbf47",
		ancestor: "#77a7d9",
		diagnostic: "#e05d5d",
	},
};

export const MONUMENT_VIEWPORT_THEME: ViewportTheme = {
	...ATELIER_VIEWPORT_THEME,
	id: "monument",
	label: "Monument",
	description: "Pastel architectural model on pale stone, softly lit like a quiet gallery plinth.",
	category: "architectural",
	sceneBackground: "#d9cfbd",
	background: { kind: "flat", fallback: "#d9cfbd", accent: "#f6ead2", intensity: 0.2 },
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
	postProcess: { outline: false, colorGrade: "pastel", vignette: 0.05, bloom: 0 },
	axis: { x: "#a2535c", y: "#528378", z: "#9e7d45", label: "#5f554c" },
	annotation: {
		text: "#302b26",
		background: "#fff6df",
		leader: "#5d6d6c",
		datum: "#246e79",
		dimension: "#915642",
		selected: "#000000",
	},
};

const MARS_VIEWPORT_THEME: ViewportTheme = {
	...ATELIER_VIEWPORT_THEME,
	id: "mars",
	label: "Mars",
	description: "Oxidized survey plate under a low sun, with dust strata and a burnt-iron horizon.",
	category: "planetary",
	performanceClass: "balanced",
	sceneBackground: "#180c0a",
	background: { kind: "mars", fallback: "#180c0a", accent: "#ff8b43", intensity: 1 },
	objectMaterial: { color: "#8f7564", roughness: 0.67, metalness: 0.24 },
	selectedMaterial: { color: "#ffd08a", emissive: "#8e2f0b", emissiveIntensity: 0.42 },
	edgeStyle: { color: "#21100c", width: 1.25, selectedColor: "#fff0b5", selectedWidth: 3.3 },
	gridStyle: {
		...ATELIER_VIEWPORT_THEME.gridStyle,
		minorColor: "#743322",
		majorColor: "#d36738",
		minorOpacity: 0.08,
		majorOpacity: 0.22,
	},
	lights: {
		...ATELIER_VIEWPORT_THEME.lights,
		ambient: 0.035,
		hemisphereSky: "#d56a38",
		hemisphereGround: "#160704",
		hemisphereIntensity: 0.45,
		keyColor: "#ffb15d",
		keyIntensity: 3.3,
		keyPosition: [-9, 2.4, 4],
		fillColor: "#5c1b13",
		fillIntensity: 0.36,
		fillPosition: [5, 4, -6],
		rimColor: "#ff5b24",
		rimIntensity: 0.75,
		rimPosition: [4, 1, -8],
	},
	environment: { toneMappingExposure: 0.96 },
	fog: { enabled: true, color: "#35150f", near: 35, far: 105 },
	postProcess: { outline: false, colorGrade: "rust", vignette: 0.44, bloom: 0.12 },
	axis: { x: "#ff8a54", y: "#b4a15d", z: "#d85c37", label: "#ffd2aa" },
	annotation: {
		...ATELIER_VIEWPORT_THEME.annotation,
		background: "#24100be8",
		leader: "#ef9c62",
		datum: "#76c6ce",
		dimension: "#ffc06c",
	},
	overlay: {
		...ATELIER_VIEWPORT_THEME.overlay,
		concept: "#85c9d2",
		profile: "#ffd36f",
		compose: "#ff8452",
		selection: "#ffffff",
	},
};

const SIRIUS_VIEWPORT_THEME: ViewportTheme = {
	...ATELIER_VIEWPORT_THEME,
	id: "sirius",
	label: "Sirius",
	description:
		"Precision hardware caught in the cold spectral halo of an impossible blue-white star.",
	category: "stellar",
	performanceClass: "balanced",
	sceneBackground: "#01040d",
	background: { kind: "sirius", fallback: "#01040d", accent: "#b9ddff", intensity: 1 },
	objectMaterial: { color: "#a8b5c3", roughness: 0.27, metalness: 0.67 },
	selectedMaterial: { color: "#fff4b5", emissive: "#3487d8", emissiveIntensity: 0.58 },
	edgeStyle: { color: "#071524", width: 1.05, selectedColor: "#ffffff", selectedWidth: 3.4 },
	gridStyle: {
		...ATELIER_VIEWPORT_THEME.gridStyle,
		minorColor: "#1d4c78",
		majorColor: "#85bce8",
		minorOpacity: 0.06,
		majorOpacity: 0.16,
	},
	lights: {
		...ATELIER_VIEWPORT_THEME.lights,
		ambient: 0.025,
		hemisphereSky: "#a8d7ff",
		hemisphereGround: "#020716",
		hemisphereIntensity: 0.58,
		keyColor: "#edf8ff",
		keyIntensity: 4.2,
		keyPosition: [-7, 8, 5],
		fillColor: "#164d9b",
		fillIntensity: 0.72,
		fillPosition: [7, 2, -5],
		rimColor: "#7ec8ff",
		rimIntensity: 1.65,
		rimPosition: [2, 4, -9],
	},
	environment: { toneMappingExposure: 1.16 },
	fog: { enabled: true, color: "#020716", near: 42, far: 145 },
	postProcess: { outline: false, colorGrade: "cold", vignette: 0.5, bloom: 0.34 },
	axis: { x: "#d86f8d", y: "#79d8c0", z: "#7ebeff", label: "#dceeff" },
	annotation: {
		...ATELIER_VIEWPORT_THEME.annotation,
		background: "#020817e8",
		leader: "#9bd3ff",
		datum: "#65e6d0",
		dimension: "#f3da81",
	},
	overlay: {
		...ATELIER_VIEWPORT_THEME.overlay,
		concept: "#72d8ff",
		profile: "#65e6d0",
		compose: "#d7b7ff",
		selection: "#fff3a0",
	},
};

const SINGULARITY_VIEWPORT_THEME: ViewportTheme = {
	...ATELIER_VIEWPORT_THEME,
	id: "singularity",
	label: "Singularity",
	description:
		"Severe gravitational composition: black center, lensed polar bands, and controlled accretion light.",
	category: "cosmic",
	performanceClass: "high",
	sceneBackground: "#000000",
	background: { kind: "singularity", fallback: "#000000", accent: "#ffb460", intensity: 1 },
	objectMaterial: { color: "#77777d", roughness: 0.31, metalness: 0.78 },
	selectedMaterial: { color: "#ffffff", emissive: "#ff7838", emissiveIntensity: 0.7 },
	edgeStyle: { color: "#050507", width: 1.15, selectedColor: "#ffd7a6", selectedWidth: 3.6 },
	gridStyle: {
		...ATELIER_VIEWPORT_THEME.gridStyle,
		enabled: false,
		minorColor: "#402345",
		majorColor: "#be6a55",
		minorOpacity: 0.03,
		majorOpacity: 0.08,
	},
	lights: {
		...ATELIER_VIEWPORT_THEME.lights,
		ambient: 0.012,
		hemisphereSky: "#8d7ca0",
		hemisphereGround: "#000000",
		hemisphereIntensity: 0.32,
		keyColor: "#fff0d0",
		keyIntensity: 3.7,
		keyPosition: [-8, 6, 3],
		fillColor: "#37104d",
		fillIntensity: 0.7,
		fillPosition: [7, 0, 4],
		rimColor: "#ff6b35",
		rimIntensity: 2.15,
		rimPosition: [3, 3, -9],
	},
	environment: { toneMappingExposure: 0.92 },
	fog: { enabled: true, color: "#000000", near: 38, far: 128 },
	postProcess: { outline: false, colorGrade: "high-contrast", vignette: 0.7, bloom: 0.38 },
	axis: { x: "#e8765a", y: "#9b76b7", z: "#efb771", label: "#e5ddd7" },
	annotation: {
		...ATELIER_VIEWPORT_THEME.annotation,
		background: "#050307ed",
		leader: "#e7b193",
		datum: "#bc8de5",
		dimension: "#ffb46b",
	},
	overlay: {
		...ATELIER_VIEWPORT_THEME.overlay,
		concept: "#b08eea",
		profile: "#ff9c68",
		compose: "#e2b06e",
		selection: "#ffffff",
	},
};

const AEONS_VIEWPORT_THEME: ViewportTheme = {
	...ATELIER_VIEWPORT_THEME,
	id: "aeons",
	label: "Aeons",
	description:
		"A quiet ancient-future observatory of immense arcs, muted gold, violet depth, and stellar haze.",
	category: "cosmic",
	performanceClass: "high",
	sceneBackground: "#05030b",
	background: { kind: "aeons", fallback: "#05030b", accent: "#c8a85d", intensity: 1 },
	objectMaterial: { color: "#77716a", roughness: 0.48, metalness: 0.58 },
	selectedMaterial: { color: "#e8d49a", emissive: "#80672b", emissiveIntensity: 0.52 },
	edgeStyle: { color: "#15101d", width: 1.1, selectedColor: "#f7e2a2", selectedWidth: 3.4 },
	gridStyle: {
		...ATELIER_VIEWPORT_THEME.gridStyle,
		minorColor: "#3b294f",
		majorColor: "#947642",
		minorOpacity: 0.035,
		majorOpacity: 0.13,
		majorStep: 10,
	},
	lights: {
		...ATELIER_VIEWPORT_THEME.lights,
		ambient: 0.025,
		hemisphereSky: "#4c3868",
		hemisphereGround: "#050208",
		hemisphereIntensity: 0.5,
		keyColor: "#ead69f",
		keyIntensity: 2.5,
		keyPosition: [-7, 9, 6],
		fillColor: "#382251",
		fillIntensity: 0.74,
		fillPosition: [8, 3, -4],
		rimColor: "#b8964f",
		rimIntensity: 1.2,
		rimPosition: [1, 5, -9],
	},
	environment: { toneMappingExposure: 1.02 },
	fog: { enabled: true, color: "#090512", near: 36, far: 132 },
	postProcess: { outline: false, colorGrade: "antique", vignette: 0.56, bloom: 0.2 },
	axis: { x: "#b36f73", y: "#759886", z: "#bd9c55", label: "#d9cba8" },
	annotation: {
		...ATELIER_VIEWPORT_THEME.annotation,
		background: "#090611ec",
		leader: "#baa56f",
		datum: "#8fbcb1",
		dimension: "#ddbf72",
	},
	overlay: {
		...ATELIER_VIEWPORT_THEME.overlay,
		concept: "#9d90c9",
		profile: "#a7c6ad",
		compose: "#c9a85d",
		selection: "#fff0aa",
	},
};

export const VIEWPORT_THEMES: readonly ViewportTheme[] = [
	ATELIER_VIEWPORT_THEME,
	MONUMENT_VIEWPORT_THEME,
	MARS_VIEWPORT_THEME,
	SIRIUS_VIEWPORT_THEME,
	SINGULARITY_VIEWPORT_THEME,
	AEONS_VIEWPORT_THEME,
];

export const VIEWPORT_THEME_REGISTRY: ReadonlyMap<ViewportThemeId, ViewportTheme> = new Map(
	VIEWPORT_THEMES.map((theme) => [theme.id, theme]),
);

export function isViewportThemeId(value: unknown): value is ViewportThemeId {
	return typeof value === "string" && VIEWPORT_THEME_REGISTRY.has(value as ViewportThemeId);
}

export function viewportThemeById(id: unknown): ViewportTheme {
	return isViewportThemeId(id)
		? (VIEWPORT_THEME_REGISTRY.get(id) ?? ATELIER_VIEWPORT_THEME)
		: ATELIER_VIEWPORT_THEME;
}
