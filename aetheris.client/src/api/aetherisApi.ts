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
    diagnostics: DiagnosticDto[];
}

export interface StepImportResponseDto {
    documentId: string;
    definitionId: string;
    occurrenceId: string;
    name: string | null;
    diagnostics: DiagnosticDto[];
}

export interface PlacementDto {
    tx: number;
    ty: number;
    tz: number;
}

export interface DocumentSnapshotDefinitionDto {
    definitionId: string | null;
    stepText: string | null;
}

export interface DocumentSnapshotOccurrenceDto {
    occurrenceId: string | null;
    definitionId: string | null;
    name: string | null;
    placement: PlacementDto | null;
}

export interface DocumentSnapshotDto {
    documentId: string;
    definitions: DocumentSnapshotDefinitionDto[];
    occurrences: DocumentSnapshotOccurrenceDto[];
}

export interface DocumentSnapshotImportResultDto {
    documentId: string;
    definitionCount: number;
    occurrenceCount: number;
}

export type BooleanOperation = 'union' | 'subtract' | 'intersect';

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
    entityKind: 'Face' | 'Edge';
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
        this.name = 'ApiError';
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
            'Content-Type': 'application/json',
            ...(init.headers ?? {}),
        },
    });

    return parseEnvelope<T>(response);
}

export async function createDocument(name?: string): Promise<DocumentCreateResponseDto> {
    return request<DocumentCreateResponseDto>('/api/v1/documents', {
        method: 'POST',
        body: JSON.stringify({ name: name ?? null }),
    });
}

export async function getDocumentSummary(documentId: string): Promise<DocumentSummaryResponseDto> {
    return request<DocumentSummaryResponseDto>(`/api/v1/documents/${documentId}`, {
        method: 'GET',
    });
}

export async function createBox(documentId: string, width: number, height: number, depth: number): Promise<BodyCreatedResponseDto> {
    return request<BodyCreatedResponseDto>(`/api/v1/documents/${documentId}/bodies/primitives/box`, {
        method: 'POST',
        body: JSON.stringify({ width, height, depth }),
    });
}

export async function tessellateBody(documentId: string, bodyId: string): Promise<TessellationResponseDto> {
    return request<TessellationResponseDto>(`/api/v1/documents/${documentId}/bodies/${bodyId}/tessellate`, {
        method: 'POST',
        body: JSON.stringify({ options: null }),
    });
}

export async function pickBody(documentId: string, bodyId: string, pickRequest: PickRequestDto): Promise<PickResponseDto> {
    return request<PickResponseDto>(`/api/v1/documents/${documentId}/bodies/${bodyId}/pick`, {
        method: 'POST',
        body: JSON.stringify(pickRequest),
    });
}

export async function translateBody(documentId: string, bodyId: string, translation: Vector3Dto): Promise<BodyTransformedResponseDto> {
    return request<BodyTransformedResponseDto>(`/api/v1/documents/${documentId}/bodies/${bodyId}/transform`, {
        method: 'POST',
        body: JSON.stringify({ translation }),
    });
}

export async function executeBoolean(documentId: string, requestDto: BooleanRequestDto): Promise<BodyCreatedResponseDto> {
    return request<BodyCreatedResponseDto>(`/api/v1/documents/${documentId}/operations/boolean`, {
        method: 'POST',
        body: JSON.stringify(requestDto),
    });
}

export async function exportDefinitionStep(documentId: string, definitionId: string): Promise<string> {
    const response = await request<StepExportResponseDto>(`/api/v1/documents/${documentId}/definitions/${definitionId}/export/step`, {
        method: 'GET',
    });

    return response.stepText;
}

export async function importStep(documentId: string, stepText: string, name?: string): Promise<StepImportResponseDto> {
    return request<StepImportResponseDto>(`/api/v1/documents/${documentId}/import/step`, {
        method: 'POST',
        body: JSON.stringify({ stepText, name: name ?? null }),
    });
}

export async function exportDocumentSnapshot(documentId: string): Promise<DocumentSnapshotDto> {
    return request<DocumentSnapshotDto>(`/api/v1/documents/${documentId}/snapshot`, {
        method: 'GET',
    });
}

export async function importDocumentSnapshot(documentId: string, snapshot: DocumentSnapshotDto): Promise<DocumentSnapshotImportResultDto> {
    return request<DocumentSnapshotImportResultDto>(`/api/v1/documents/${documentId}/snapshot`, {
        method: 'POST',
        body: JSON.stringify(snapshot),
    });
}

export { parseEnvelope };
