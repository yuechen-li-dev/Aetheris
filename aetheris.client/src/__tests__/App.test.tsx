import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import App from '../App';
import { ApiError } from '../api/aetherisApi';

const apiMocks = vi.hoisted(() => ({
    createDocument: vi.fn(),
    createBox: vi.fn(),
    executeBoolean: vi.fn(),
    exportDefinitionStep: vi.fn(),
    getDocumentSummary: vi.fn(),
    importStep: vi.fn(),
    pickBody: vi.fn(),
    tessellateBody: vi.fn(),
    translateBody: vi.fn(),
}));

vi.mock('../api/aetherisApi', async () => {
    const actual = await vi.importActual('../api/aetherisApi');

    return {
        ...actual,
        createDocument: apiMocks.createDocument,
        createBox: apiMocks.createBox,
        executeBoolean: apiMocks.executeBoolean,
        exportDefinitionStep: apiMocks.exportDefinitionStep,
        getDocumentSummary: apiMocks.getDocumentSummary,
        importStep: apiMocks.importStep,
        pickBody: apiMocks.pickBody,
        tessellateBody: apiMocks.tessellateBody,
        translateBody: apiMocks.translateBody,
    };
});

vi.mock('../viewer/ViewerViewport', () => ({
    ViewerViewport: () => <div data-testid="viewer-viewport" />,
}));

function setupDocumentApiMocks(): void {
    apiMocks.createDocument.mockResolvedValue({ documentId: 'doc-1', name: 'Test', volatile: true });
    apiMocks.createBox.mockResolvedValue({
        documentId: 'doc-1',
        bodyId: 'occ-2',
        definitionId: 'def-2',
        faceCount: 6,
        edgeCount: 12,
        vertexCount: 8,
    });
    apiMocks.getDocumentSummary.mockResolvedValue({
        documentId: 'doc-1',
        name: 'Test',
        bodyCount: 1,
        bodyIds: ['occ-2'],
        definitionCount: 1,
        occurrences: [{
            occurrenceId: 'occ-2',
            definitionId: 'def-2',
            name: 'Imported',
            translation: { x: 0, y: 0, z: 0 },
        }],
    });
    apiMocks.tessellateBody.mockResolvedValue({ facePatches: [], edgePolylines: [] });
}

describe('App STEP file upload flow', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        setupDocumentApiMocks();
    });

    afterEach(() => {
        cleanup();
    });

    it('updates local file selection state when a file is chosen', async () => {
        render(<App />);

        fireEvent.click(screen.getByRole('button', { name: 'Create Document' }));
        await screen.findByText(/Create document complete/i);

        const fileInput = screen.getByLabelText('STEP 242 File') as HTMLInputElement;
        const file = new File(['ISO-10303-21;'], 'sample.step', { type: 'text/plain' });
        fireEvent.change(fileInput, { target: { files: [file] } });

        expect(screen.getByText('Selected file: sample.step (13 bytes)')).toBeTruthy();
    });

    it('calls importStep with file contents and refreshes canonical hash', async () => {
        apiMocks.importStep.mockResolvedValue({
            documentId: 'doc-1',
            definitionId: 'def-2',
            occurrenceId: 'occ-2',
            name: 'Imported',
            diagnostics: [],
        });
        apiMocks.exportDefinitionStep.mockResolvedValue({
            documentId: 'doc-1',
            definitionId: 'def-2',
            stepText: 'ISO-10303-21;',
            canonicalHash: 'hash-123',
            diagnostics: [],
        });

        render(<App />);

        fireEvent.click(screen.getByRole('button', { name: 'Create Document' }));
        await screen.findByText(/Create document complete/i);

        const fileInput = screen.getByLabelText('STEP 242 File') as HTMLInputElement;
        const file = new File(['ISO-10303-21;DATA;'], 'part.stp', { type: 'text/plain' });
        fireEvent.change(fileInput, { target: { files: [file] } });

        fireEvent.click(screen.getByRole('button', { name: 'Import STEP 242' }));

        await waitFor(() => {
            expect(apiMocks.importStep).toHaveBeenCalledWith('doc-1', 'ISO-10303-21;DATA;');
        });
        expect(apiMocks.exportDefinitionStep).toHaveBeenCalledWith('doc-1', 'def-2');
        await screen.findByText('hash-123');
    });

    it('downloads backend STEP text with deterministic filename without mutating hash state', async () => {
        const createObjectUrlSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:download');
        const revokeObjectUrlSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
        const anchorClickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
        const appendSpy = vi.spyOn(document.body, 'appendChild');

        apiMocks.exportDefinitionStep
            .mockResolvedValueOnce({
                documentId: 'doc-1',
                definitionId: 'def-2',
                stepText: 'ISO-10303-21;HEADER;ENDSEC;',
                canonicalHash: 'hash-initial',
                diagnostics: [],
            })
            .mockResolvedValueOnce({
                documentId: 'doc-1',
                definitionId: 'def-2',
                stepText: 'ISO-10303-21;DATA;ENDSEC;',
                canonicalHash: 'hash-after-download',
                diagnostics: [],
            });

        render(<App />);

        fireEvent.click(screen.getByRole('button', { name: 'Create Document' }));
        await screen.findByText(/Create document complete/i);
        fireEvent.click(screen.getByRole('button', { name: 'Create Box' }));
        await screen.findByText(/Create box complete/i);

        fireEvent.click(screen.getByRole('button', { name: 'Export Active (STEP)' }));
        await screen.findByText('hash-initial');

        fireEvent.click(screen.getByRole('button', { name: 'Download Canonical 242' }));

        await waitFor(() => {
            expect(apiMocks.exportDefinitionStep).toHaveBeenNthCalledWith(2, 'doc-1', 'def-2');
        });

        const blob = createObjectUrlSpy.mock.calls[0][0] as Blob;
        expect(await blob.text()).toBe('ISO-10303-21;DATA;ENDSEC;');

        const downloadAnchor = appendSpy.mock.calls.at(-1)?.[0] as HTMLAnchorElement;
        expect(downloadAnchor.download).toBe('aetheris-def-2.step');
        expect(anchorClickSpy).toHaveBeenCalledTimes(1);
        expect(revokeObjectUrlSpy).toHaveBeenCalledWith('blob:download');

        expect(screen.getByText('hash-initial')).toBeTruthy();
        expect(screen.queryByText('hash-after-download')).toBeNull();
    });

    it('preserves ApiError diagnostics from import failure', async () => {
        apiMocks.importStep.mockRejectedValue(new ApiError('Malformed STEP payload.', [{
            code: 'ValidationFailed',
            severity: 'Error',
            message: 'Malformed STEP payload.',
            source: 'step242.import',
        }]));

        render(<App />);

        fireEvent.click(screen.getByRole('button', { name: 'Create Document' }));
        await screen.findByText(/Create document complete/i);

        const fileInput = screen.getByLabelText('STEP 242 File') as HTMLInputElement;
        const file = new File(['BAD'], 'bad.step', { type: 'text/plain' });
        fireEvent.change(fileInput, { target: { files: [file] } });

        fireEvent.click(screen.getByRole('button', { name: 'Import STEP 242' }));

        await screen.findByText('Malformed STEP payload.');
        await screen.findByText('[Error] ValidationFailed: Malformed STEP payload.');
    });

    it('preserves ApiError diagnostics from canonical download failure', async () => {
        apiMocks.exportDefinitionStep.mockRejectedValue(new ApiError('Export failed.', [{
            code: 'StepExportFailed',
            severity: 'Error',
            message: 'Export failed.',
            source: 'step242.export',
        }]));

        render(<App />);

        fireEvent.click(screen.getByRole('button', { name: 'Create Document' }));
        await screen.findByText(/Create document complete/i);
        fireEvent.click(screen.getByRole('button', { name: 'Create Box' }));
        await screen.findByText(/Create box complete/i);

        fireEvent.click(screen.getByRole('button', { name: 'Download Canonical 242' }));

        await screen.findByText('Export failed.');
        await screen.findByText('[Error] StepExportFailed: Export failed.');
    });
});
