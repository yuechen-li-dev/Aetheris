import { describe, expect, it } from 'vitest';
import { ApiError, parseEnvelope } from '../api/aetherisApi';

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
