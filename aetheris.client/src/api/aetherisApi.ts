export interface DiagnosticDto {
	code: string;
	severity: string;
	message: string;
	source: string | null;
}

export interface ApiEnvelope<T> {
	success: boolean;
	data: T | null;
	diagnostics: DiagnosticDto[];
}

export interface DocumentCreateResponseDto {
	documentId: string;
	name: string | null;
	volatile: boolean;
}

export interface BodyOccurrenceSummaryDto {
	occurrenceId: string;
	definitionId: string;
	name: string | null;
	translation: Vector3Dto;
}

export interface DocumentSummaryResponseDto {
	documentId: string;
	name: string | null;
	bodyCount: number;
	bodyIds: string[];
	definitionCount: number;
	occurrences: BodyOccurrenceSummaryDto[];
}

export interface BodyCreatedResponseDto {
	documentId: string;
	bodyId: string;
	definitionId: string;
	faceCount: number;
	edgeCount: number;
	vertexCount: number;
}

export interface BodyTransformedResponseDto {
	documentId: string;
	bodyId: string;
	definitionId: string;
	appliedTranslation: Vector3Dto;
}

export interface StepExportResponseDto {
	documentId: string;
	definitionId: string;
	stepText: string;
	canonicalHash: string;
	diagnostics: DiagnosticDto[];
}

export interface StepImportResponseDto {
	documentId: string;
	definitionId: string;
	occurrenceId: string;
	name: string | null;
	diagnostics: DiagnosticDto[];
	semanticPresentation?: unknown | null;
}

export type BooleanOperation = "union" | "subtract" | "intersect";

export interface BooleanRequestDto {
	leftBodyId: string;
	rightBodyId: string;
	operation: BooleanOperation;
}

export interface Point3Dto {
	x: number;
	y: number;
	z: number;
}

export interface Vector3Dto {
	x: number;
	y: number;
	z: number;
}

export interface FacePatchDto {
	faceId: number;
	positions: Point3Dto[];
	normals: Vector3Dto[];
	triangleIndices: number[];
}

export interface EdgePolylineDto {
	edgeId: number;
	points: Point3Dto[];
	isClosed: boolean;
}

export interface TessellationResponseDto {
	facePatches: FacePatchDto[];
	edgePolylines: EdgePolylineDto[];
}

export interface AnalyticDisplayFaceDomainHintDto {
	minU?: number | null;
	maxU?: number | null;
	minV: number | null;
	maxV: number | null;
}

export interface StartupStepDto {
	path: string;
	fileName: string;
	stepText: string;
	kind?: "step" | "assembly";
}

export interface AssemblyDisplayDefinitionDto {
	stableId: string;
	definitionIdentity: string;
	facePatches: FacePatchDto[];
}
export interface AssemblyDisplayOccurrenceDto {
	stableId: string;
	name: string;
	instancePath: string;
	parentStableId: string | null;
	definitionStableId: string | null;
	kind: "Assembly" | "Part";
	worldTransform: number[];
	placementAuthority: "MateDerived" | "ImportedOccurrence" | "LegacyExplicit";
	selectionMembers?: string[];
}
export interface AssemblyDisplayPublicSemanticDto {
	name: string;
	type: string;
	capabilities: string[];
	bindingKinds: string[];
	internalImplementationPath: string | null;
}
export interface AssemblyDisplayModuleDefinitionDto {
	stableId: string;
	definitionIdentity: string;
	templateName: string;
	specializationIdentity: string;
	provenance: string[];
	publicSemantics: AssemblyDisplayPublicSemanticDto[];
	localSolveMilliseconds: number;
}
export interface AssemblyDisplayMateDto {
	stableId: string;
	name: string;
	interfaceStableId: string;
	participants: string[];
	constraintIds: string[];
	validationStatus: string;
}
export interface AssemblyDisplayToleranceDto {
	name: string;
	passed: boolean;
	nominal: number;
	minimum: number;
	maximum: number;
	unit: string;
	contributors: string[];
	expandedContributors?: string[];
}
export interface AssemblyDisplayPacketDto {
	schema: string;
	name: string;
	rootOccurrenceStableId: string;
	definitions: AssemblyDisplayDefinitionDto[];
	occurrences: AssemblyDisplayOccurrenceDto[];
	mates: AssemblyDisplayMateDto[];
	toleranceStackups: AssemblyDisplayToleranceDto[];
	bounds: { minimum: number[]; maximum: number[] };
	diagnostics: DisplayDiagnosticDto[];
	performance: Record<string, number>;
	moduleDefinitions?: AssemblyDisplayModuleDefinitionDto[];
}

export interface AnalyticDisplayPlaneGeometryDto {
	origin: Point3Dto;
	normal: Vector3Dto;
	uAxis: Vector3Dto;
	vAxis: Vector3Dto;
	outerBoundary: Point3Dto[] | null;
}

export interface AnalyticDisplayCylinderGeometryDto {
	origin: Point3Dto;
	axis: Vector3Dto;
	xAxis: Vector3Dto;
	yAxis: Vector3Dto;
	radius: number;
}

export interface AnalyticDisplayConeGeometryDto {
	apex: Point3Dto;
	axis: Vector3Dto;
	xAxis: Vector3Dto;
	yAxis: Vector3Dto;
	semiAngleRadians: number;
}

export interface AnalyticDisplaySphereGeometryDto {
	center: Point3Dto;
	axis: Vector3Dto;
	xAxis: Vector3Dto;
	yAxis: Vector3Dto;
	radius: number;
}

export interface AnalyticDisplayTorusGeometryDto {
	center: Point3Dto;
	axis: Vector3Dto;
	xAxis: Vector3Dto;
	yAxis: Vector3Dto;
	majorRadius: number;
	minorRadius: number;
}

export interface AnalyticDisplayFaceDto {
	faceId: number;
	shellId: number;
	shellRole: "Outer" | "InnerVoid" | "Unknown";
	surfaceGeometryId: number;
	surfaceKind: "Plane" | "Sphere" | "Cylinder" | "Cone" | "Torus" | string;
	loopCount: number;
	domainHint: AnalyticDisplayFaceDomainHintDto | null;
	planeGeometry: AnalyticDisplayPlaneGeometryDto | null;
	cylinderGeometry: AnalyticDisplayCylinderGeometryDto | null;
	coneGeometry: AnalyticDisplayConeGeometryDto | null;
	sphereGeometry: AnalyticDisplaySphereGeometryDto | null;
	torusGeometry: AnalyticDisplayTorusGeometryDto | null;
}

export interface AnalyticDisplayFallbackFaceDto {
	faceId: number;
	shellId: number;
	shellRole: "Outer" | "InnerVoid" | "Unknown";
	reason:
		| "MissingFaceBinding"
		| "MissingSurfaceGeometry"
		| "UnsupportedSurfaceKind"
		| "UnsupportedTrim"
		| string;
	surfaceKind: string | null;
	detail: string | null;
}

export interface AnalyticDisplayPacketDto {
	bodyId: number;
	analyticFaces: AnalyticDisplayFaceDto[];
	fallbackFaces: AnalyticDisplayFallbackFaceDto[];
}

export interface DisplayDiagnosticDto {
	code: string;
	message: string;
	faceId: number | null;
	surfaceKind: string | null;
	phase: string | null;
	suggestedNextAction: string | null;
}

export interface DisplayWirePatchDto {
	kind: "WirePatch" | string;
	source: "BRepEdges" | string;
	quality: "PreviewPolyline" | string;
	loops: DisplayLoopDto[];
}

export interface DisplayLoopDto {
	loopId: number;
	role: "Outer" | "Inner" | "Unknown" | string;
	edges: DisplayEdgeDto[];
}

export interface DisplayEdgeDto {
	edgeId: number;
	points: Point3Dto[];
	sourceCurveKind: string;
	sampleCount: number;
	diagnostics: DisplayDiagnosticDto[];
}

export interface DisplayFaceDto {
	faceId: number;
	shellId: number | null;
	surfaceKind: string | null;
	status:
		| "Analytic"
		| "Mesh"
		| "WireframeOnly"
		| "BoundingProxy"
		| "DiagnosticOnly"
		| "Omitted"
		| string;
	patchKind:
		| "AnalyticPatch"
		| "MeshPatch"
		| "WirePatch"
		| "ProxyPatch"
		| "DiagnosticPatch"
		| string;
	meshPatch: FacePatchDto | null;
	analyticPatch: AnalyticDisplayFaceDto | null;
	wirePatch: DisplayWirePatchDto | null;
	materializationLane?:
		| "AnalyticPatch"
		| "BoundedMesh"
		| "DiagnosticOnly"
		| "WirePatch"
		| "ProxyPatch"
		| string
		| null;
	diagnostics: DisplayDiagnosticDto[];
}

export interface DisplayLaneDto {
	kind: "AnalyticPatch" | "BoundedMesh" | "DiagnosticOnly" | "WirePatch" | "ProxyPatch" | string;
	status: "Complete" | "Partial" | "DiagnosticOnly" | "Failed" | string;
	source: string;
	displayAuthority: string;
	implementation: string | null;
	quality: string | null;
	timeoutMs: number | null;
	faceCount: number;
	diagnosticCount: number;
}

export interface DisplayPreparationResponseDto {
	lane: "analytic-only" | "mixed-fallback" | "fallback-only" | string;
	analyticPacket: AnalyticDisplayPacketDto;
	tessellationFallback: TessellationResponseDto | null;
	status?: "Complete" | "Partial" | "DiagnosticOnly" | "Failed" | string;
	sourceAuthority?: string;
	displayAuthority?: string;
	lanes?: string[] | null;
	faces?: DisplayFaceDto[] | null;
	diagnostics?: DisplayDiagnosticDto[] | null;
	displayLanes?: DisplayLaneDto[] | null;
}

export interface CadmataVisualizationArtifactDto {
	schemaVersion: "cadmata-concept-viz-x1";
	fixtureId: string;
	sourcePath: string;
	entities: unknown[];
	selections: unknown[];
	diagnostics: unknown[];
	metrics: Record<string, number>;
}
export interface CadmataFixtureLoadResponseDto {
	documentId: string;
	bodyId: string;
	definitionId: string;
	fixtureId: string;
	visualization: CadmataVisualizationArtifactDto;
}

export interface PickOptionsDto {
	nearestOnly?: boolean;
	includeBackfaces?: boolean;
	edgeTolerance?: number;
	sortTieTolerance?: number;
	maxDistance?: number;
}

export interface PickRequestDto {
	origin: Point3Dto;
	direction: Vector3Dto;
	tessellationOptions: null;
	pickOptions: PickOptionsDto;
}

export interface PickHitDto {
	occurrenceId: string;
	t: number;
	point: Point3Dto;
	normal: Vector3Dto | null;
	entityKind: "Face" | "Edge";
	faceId: number | null;
	edgeId: number | null;
	bodyId: number | null;
	sourcePatchIndex: number | null;
	sourcePrimitiveIndex: number | null;
}

export interface PickResponseDto {
	hits: PickHitDto[];
}

export class ApiError extends Error {
	public readonly diagnostics: DiagnosticDto[];

	public constructor(message: string, diagnostics: DiagnosticDto[]) {
		super(message);
		this.name = "ApiError";
		this.diagnostics = diagnostics;
	}
}

async function parseEnvelope<T>(response: Response): Promise<T> {
	let envelope: ApiEnvelope<T>;

	try {
		envelope = (await response.json()) as ApiEnvelope<T>;
	} catch {
		throw new ApiError(`Invalid JSON response (HTTP ${response.status}).`, []);
	}

	if (!envelope.success || envelope.data === null) {
		const diagnostics = envelope.diagnostics ?? [];
		const firstMessage = diagnostics[0]?.message ?? `Request failed with HTTP ${response.status}.`;
		throw new ApiError(firstMessage, diagnostics);
	}

	return envelope.data;
}

async function request<T>(path: string, init: RequestInit): Promise<T> {
	const response = await fetch(path, {
		...init,
		headers: {
			"Content-Type": "application/json",
			...(init.headers ?? {}),
		},
	});

	return parseEnvelope<T>(response);
}

export async function createDocument(name?: string): Promise<DocumentCreateResponseDto> {
	return request<DocumentCreateResponseDto>("/api/v1/documents", {
		method: "POST",
		body: JSON.stringify({ name: name ?? null }),
	});
}

export async function getDocumentSummary(documentId: string): Promise<DocumentSummaryResponseDto> {
	return request<DocumentSummaryResponseDto>(`/api/v1/documents/${documentId}`, {
		method: "GET",
	});
}

export async function createBox(
	documentId: string,
	width: number,
	height: number,
	depth: number,
): Promise<BodyCreatedResponseDto> {
	return request<BodyCreatedResponseDto>(`/api/v1/documents/${documentId}/bodies/primitives/box`, {
		method: "POST",
		body: JSON.stringify({ width, height, depth }),
	});
}

export async function tessellateBody(
	documentId: string,
	bodyId: string,
): Promise<TessellationResponseDto> {
	return request<TessellationResponseDto>(
		`/api/v1/documents/${documentId}/bodies/${bodyId}/tessellate`,
		{
			method: "POST",
			body: JSON.stringify({ options: null }),
		},
	);
}

export async function prepareBodyDisplay(
	documentId: string,
	bodyId: string,
): Promise<DisplayPreparationResponseDto> {
	return request<DisplayPreparationResponseDto>(
		`/api/v1/documents/${documentId}/bodies/${bodyId}/display/prepare`,
		{
			method: "POST",
			body: JSON.stringify({ tessellationOptions: null }),
		},
	);
}

export async function loadCadmataFixture(
	documentId: string,
	fixtureId: string,
): Promise<CadmataFixtureLoadResponseDto> {
	return request<CadmataFixtureLoadResponseDto>(
		`/api/v1/documents/${documentId}/cadmata/fixtures/${fixtureId}`,
		{ method: "POST" },
	);
}

export async function pickBody(
	documentId: string,
	bodyId: string,
	pickRequest: PickRequestDto,
): Promise<PickResponseDto> {
	return request<PickResponseDto>(`/api/v1/documents/${documentId}/bodies/${bodyId}/pick`, {
		method: "POST",
		body: JSON.stringify(pickRequest),
	});
}

export async function translateBody(
	documentId: string,
	bodyId: string,
	translation: Vector3Dto,
): Promise<BodyTransformedResponseDto> {
	return request<BodyTransformedResponseDto>(
		`/api/v1/documents/${documentId}/bodies/${bodyId}/transform`,
		{
			method: "POST",
			body: JSON.stringify({ translation }),
		},
	);
}

export async function executeBoolean(
	documentId: string,
	requestDto: BooleanRequestDto,
): Promise<BodyCreatedResponseDto> {
	return request<BodyCreatedResponseDto>(`/api/v1/documents/${documentId}/operations/boolean`, {
		method: "POST",
		body: JSON.stringify(requestDto),
	});
}

export async function exportDefinitionStep(
	documentId: string,
	definitionId: string,
): Promise<StepExportResponseDto> {
	return request<StepExportResponseDto>(
		`/api/v1/documents/${documentId}/definitions/${definitionId}/export/step`,
		{
			method: "GET",
		},
	);
}

export async function importStep(
	documentId: string,
	stepText: string,
	name?: string,
): Promise<StepImportResponseDto> {
	return request<StepImportResponseDto>(`/api/v1/documents/${documentId}/import/step`, {
		method: "POST",
		body: JSON.stringify({ stepText, name: name ?? null }),
	});
}

export async function claimStartupStep(): Promise<StartupStepDto | null> {
	const response = await fetch("/api/v1/startup/step", { method: "POST" });
	if (response.status === 204) {
		return null;
	}

	return parseEnvelope<StartupStepDto>(response);
}

export async function prepareAssemblyDisplay(path: string): Promise<AssemblyDisplayPacketDto> {
	return request<AssemblyDisplayPacketDto>("/api/v1/assemblies/display", {
		method: "POST",
		body: JSON.stringify({ path }),
	});
}

export { parseEnvelope };
