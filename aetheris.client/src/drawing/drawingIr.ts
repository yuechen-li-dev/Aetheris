export type DrawingPoint2 = { x: number; y: number };
export type DrawingRect = { x: number; y: number; width: number; height: number };

export type DrawingIr = {
	schemaVersion: "aetheris-drawing-m0";
	identity: string;
	metadata: {
		title: string;
		productName: string;
		partNumber?: string;
		revision?: string;
		material?: string;
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
	views: DrawingViewIr[];
	annotations: DrawingAnnotationIr[];
	tables: DrawingTableIr[];
	notes: string[];
};

export type DrawingViewIr = {
	identity: string;
	viewport: DrawingRect;
	primitives: {
		stableId: string;
		kind: "Visible" | "Silhouette" | "Hidden";
		points: DrawingPoint2[];
	}[];
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
};
