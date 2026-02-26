import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, parseEnvelope, pickBody } from '../api/aetherisApi';

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
