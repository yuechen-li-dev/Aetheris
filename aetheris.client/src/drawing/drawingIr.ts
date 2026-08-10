export type DrawingPoint2 = { x: number; y: number };
export type DrawingRect = { x: number; y: number; width: number; height: number };

export type DrawingIr = {
	schemaVersion: "aetheris-drawing-m0" | "aetheris-drawing-m0b";
	identity: string;
	metadata: {
		title: string;
		productName: string;
		partNumber?: string;
		revision?: { major: number; minor: number; patch: number };
		material?: string;
		company?: string;
		author?: string;
		date?: string;
		description?: string;
		drawingIdentity: string;
		templateIdentity: string;
	};
	pages: DrawingPageIr[];
};

export type DrawingPageIr = {
	pageNumber: number;
	orientation: "Portrait" | "Landscape";
	widthMillimetres: 210 | 297;
	heightMillimetres: 210 | 297;
	contentRect?: DrawingRect;
	views: DrawingViewIr[];
	annotations: DrawingAnnotationIr[];
	tables: DrawingTableIr[];
	notes: string[];
	zoneScheme?: DrawingZoneSchemeIr;
	informationBlock?: {
		bounds: DrawingRect;
		location: DrawingLocationIr;
		fields: Record<string, string>;
	};
	locatedNotes?: {
		identity: string;
		text: string;
		bounds: DrawingRect;
		location: DrawingLocationIr;
	}[];
	bom?: { identity: string; flatteningPolicy: string; items: unknown[]; table: DrawingTableIr };
};

export type DrawingLocationIr = { page: number; zone: string };
export type DrawingZoneSchemeIr = {
	rows: number;
	columns: number;
	rowLabels: string[];
	columnLabels: string[];
	zones: { address: string; bounds: DrawingRect }[];
};

export type DrawingViewIr = {
	identity: string;
	viewport: DrawingRect;
	primitives: {
		stableId: string;
		kind: "Visible" | "Silhouette" | "Hidden";
		points: DrawingPoint2[];
		occurrenceIdentity?: string;
		definitionIdentity?: string;
	}[];
	location?: DrawingLocationIr;
};

export type DrawingAnnotationIr = {
	identity: string;
	semanticReference: string;
	engineeringDisplay: string;
	selectedCandidate: { body: DrawingRect; leader: DrawingPoint2[] };
};

export type DrawingTableIr = {
	identity: string;
	columns: string[];
	rows: string[][];
	sourceIdentity: string;
	kind?: "Design" | "BillOfMaterials";
	bounds?: DrawingRect;
	location?: DrawingLocationIr;
};
