import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, executeBoolean, parseEnvelope, pickBody, translateBody } from '../api/aetherisApi';

describe('parseEnvelope', () => {
    it('returns data when envelope success is true', async () => {
        const response = new Response(JSON.stringify({
            success: true,
            data: { documentId: 'abc' },
            diagnostics: [],
        }), { status: 200 });

        const data = await parseEnvelope<{ documentId: string }>(response);

        expect(data.documentId).toBe('abc');
    });

    it('throws ApiError with diagnostics when envelope indicates failure', async () => {
        const response = new Response(JSON.stringify({
            success: false,
            data: null,
            diagnostics: [{
                code: 'InvalidArgument',
                severity: 'Error',
                message: 'Bad request',
                source: 'documents.create',
            }],
        }), { status: 400 });

        await expect(parseEnvelope(response)).rejects.toEqual(new ApiError('Bad request', [{
            code: 'InvalidArgument',
            severity: 'Error',
            message: 'Bad request',
            source: 'documents.create',
        }]));
    });
});

describe('pickBody', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('posts pick payload and parses nearest-hit response envelope', async () => {
        const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
            success: true,
            data: {
                hits: [{
                    occurrenceId: 'occ-1',
                    t: 1.25,
                    point: { x: 0, y: 0, z: 1 },
                    normal: { x: 0, y: 0, z: 1 },
                    entityKind: 'Face',
                    faceId: 2,
                    edgeId: null,
                    bodyId: null,
                    sourcePatchIndex: 0,
                    sourcePrimitiveIndex: 1,
                }],
            },
            diagnostics: [],
        }), { status: 200 }));
        vi.stubGlobal('fetch', fetchMock);

        const response = await pickBody('doc-1', 'body-1', {
            origin: { x: 1, y: 2, z: 3 },
            direction: { x: 0, y: 0, z: -1 },
            tessellationOptions: null,
            pickOptions: { nearestOnly: true },
        });

        expect(fetchMock).toHaveBeenCalledWith('/api/v1/documents/doc-1/bodies/body-1/pick', expect.objectContaining({ method: 'POST' }));
        expect(response.hits[0].entityKind).toBe('Face');
        expect(response.hits[0].faceId).toBe(2);
    });
});

describe('translateBody', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('posts translation payload with v1 envelope handling', async () => {
        const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
            success: true,
            data: {
                documentId: 'doc-1',
                bodyId: 'body-1',
                definitionId: 'def-1',
                appliedTranslation: { x: 1, y: 2, z: 3 },
            },
            diagnostics: [],
        }), { status: 200 }));
        vi.stubGlobal('fetch', fetchMock);

        const response = await translateBody('doc-1', 'body-1', { x: 1, y: 2, z: 3 });

        expect(fetchMock).toHaveBeenCalledWith('/api/v1/documents/doc-1/bodies/body-1/transform', expect.objectContaining({ method: 'POST' }));
        expect(response.appliedTranslation.z).toBe(3);
    });
});

describe('executeBoolean', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('posts boolean payload with v1 envelope handling', async () => {
        const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
            success: true,
            data: {
                documentId: 'doc-1',
                bodyId: 'result-body-1',
                definitionId: 'result-def-1',
                faceCount: 6,
                edgeCount: 12,
                vertexCount: 8,
            },
            diagnostics: [],
        }), { status: 200 }));
        vi.stubGlobal('fetch', fetchMock);

        const response = await executeBoolean('doc-1', {
            leftBodyId: 'body-a',
            rightBodyId: 'body-b',
            operation: 'union',
        });

        expect(fetchMock).toHaveBeenCalledWith('/api/v1/documents/doc-1/operations/boolean', expect.objectContaining({ method: 'POST' }));
        expect(response.bodyId).toBe('result-body-1');
    });

    it('surfaces unsupported diagnostics from envelope failures', async () => {
        const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
            success: false,
            data: null,
            diagnostics: [{
                code: 'NotImplemented',
                severity: 'Error',
                message: 'Boolean operation not yet implemented for non-box input.',
                source: 'operations.boolean',
            }],
        }), { status: 501 }));
        vi.stubGlobal('fetch', fetchMock);

        await expect(executeBoolean('doc-1', {
            leftBodyId: 'body-a',
            rightBodyId: 'body-b',
            operation: 'intersect',
        })).rejects.toEqual(new ApiError('Boolean operation not yet implemented for non-box input.', [{
            code: 'NotImplemented',
            severity: 'Error',
            message: 'Boolean operation not yet implemented for non-box input.',
            source: 'operations.boolean',
        }]));
    });
});
