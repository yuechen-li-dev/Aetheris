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

export interface DocumentSummaryResponseDto {
    documentId: string;
    name: string | null;
    bodyCount: number;
    bodyIds: string[];
}

export interface BodyCreatedResponseDto {
    documentId: string;
    bodyId: string;
    faceCount: number;
    edgeCount: number;
    vertexCount: number;
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

export { parseEnvelope };
