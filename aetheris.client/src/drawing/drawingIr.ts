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
	definitionEvidence?: {
		nominalGeometryComplete: boolean;
		dimensionalTolerancesComplete: boolean;
		materialSpecified: boolean;
		processCapabilityVerified: boolean;
		family: string;
		coveredFaces: number[];
		missingRequirements: string[];
		stockDimensions: Record<string, number>;
		holes: { identity: string; x: number; y: number; diameter: number; extent: string; group: string; sourceReferences: string[] }[];
		linearToleranceMillimetres?: number | null;
		angularToleranceDegrees?: number | null;
		minimumHoleWebAtDeclaredLimitsMillimetres?: number | null;
		toleranceScreenScope: string;
	} | null;
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
        table?: {
            columnWidths: number[];
            rows: { height: number; cells: { key: string; label: string; value: string; columnSpan: number;
                bounds: DrawingRect; valueLines: string[]; valueSizeMillimetres: number; emphasized: boolean }[] }[];
        };
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
		kind: "Visible" | "Silhouette" | "Hidden" | "SurfaceGrid" | "CenterMark";
		points: DrawingPoint2[];
		occurrenceIdentity?: string;
		definitionIdentity?: string;
	}[];
	location?: DrawingLocationIr;
};

export type DrawingAnnotationIr = {
	identity: string;
	semanticReference: string;
	kind?: string;
	strokes?: { start: DrawingPoint2; end: DrawingPoint2 }[];
	engineeringDisplay: string;
	selectedCandidate: { body: DrawingRect; leader: DrawingPoint2[] };
};

export type DrawingTableIr = {
	identity: string;
	columns: string[];
	rows: string[][];
	sourceIdentity: string;
	kind?: "Design" | "BillOfMaterials" | "HoleCoordinates";
	bounds?: DrawingRect;
	location?: DrawingLocationIr;
};
