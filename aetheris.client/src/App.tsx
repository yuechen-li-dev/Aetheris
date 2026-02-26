import { useCallback, useMemo, useState } from 'react';
import './App.css';
import {
    ApiError,
    createBox,
    createDocument,
    getDocumentSummary,
    tessellateBody,
    type DiagnosticDto,
    type TessellationResponseDto,
} from './api/aetherisApi';
import { ViewerViewport } from './viewer/ViewerViewport';
import { mapTessellationToRenderData } from './viewer/tessellationMapper';

type RequestStatus = 'idle' | 'loading' | 'success' | 'error';

function App() {
    const [documentId, setDocumentId] = useState<string | null>(null);
    const [bodyIds, setBodyIds] = useState<string[]>([]);
    const [activeBodyId, setActiveBodyId] = useState<string | null>(null);
    const [tessellation, setTessellation] = useState<TessellationResponseDto | null>(null);
    const [status, setStatus] = useState<RequestStatus>('idle');
    const [statusMessage, setStatusMessage] = useState<string>('Ready. Create a document to begin.');
    const [diagnostics, setDiagnostics] = useState<DiagnosticDto[]>([]);

    const runAction = useCallback(async (actionName: string, action: () => Promise<void>) => {
        setStatus('loading');
        setStatusMessage(`${actionName}...`);
        setDiagnostics([]);

        try {
            await action();
            setStatus('success');
            setStatusMessage(`${actionName} complete.`);
        } catch (error) {
            const apiError = error instanceof ApiError
                ? error
                : new ApiError((error as Error).message || 'Unexpected error.', []);
            setStatus('error');
            setStatusMessage(apiError.message);
            setDiagnostics(apiError.diagnostics);
        }
    }, []);

    const handleCreateDocument = useCallback(async () => {
        await runAction('Create document', async () => {
            const created = await createDocument('Viewer Shell M18');
            setDocumentId(created.documentId);
            setBodyIds([]);
            setActiveBodyId(null);
            setTessellation(null);
        });
    }, [runAction]);

    const handleCreateBox = useCallback(async () => {
        if (!documentId) {
            return;
        }

        await runAction('Create box + tessellate', async () => {
            const created = await createBox(documentId, 1.75, 1.25, 1.1);
            const summary = await getDocumentSummary(documentId);
            const tessellated = await tessellateBody(documentId, created.bodyId);

            setBodyIds(summary.bodyIds);
            setActiveBodyId(created.bodyId);
            setTessellation(tessellated);
        });
    }, [documentId, runAction]);

    const handleRefreshTessellation = useCallback(async () => {
        if (!documentId || !activeBodyId) {
            return;
        }

        await runAction('Refresh tessellation', async () => {
            const tessellated = await tessellateBody(documentId, activeBodyId);
            setTessellation(tessellated);
        });
    }, [activeBodyId, documentId, runAction]);
    const sceneData = useMemo(() => (tessellation ? mapTessellationToRenderData(tessellation) : null), [tessellation]);

    return (
        <div className="app-shell">
            <header className="toolbar">
                <h1>Aetheris Viewer Shell (M18)</h1>
                <div className="toolbar-actions">
                    <button type="button" onClick={() => void handleCreateDocument()}>
                        Create Document
                    </button>
                    <button type="button" onClick={() => void handleCreateBox()} disabled={!documentId || status === 'loading'}>
                        Create Box
                    </button>
                    <button type="button" onClick={() => void handleRefreshTessellation()} disabled={!activeBodyId || status === 'loading'}>
                        Refresh Tessellation
                    </button>
                </div>
            </header>

            <main className="main-grid">
                <ViewerViewport sceneData={sceneData} />
                <aside className="debug-panel">
                    <h2>Debug / Status</h2>
                    <p><strong>Request status:</strong> {status}</p>
                    <p><strong>Message:</strong> {statusMessage}</p>
                    <p><strong>Document ID:</strong> {documentId ?? 'None'}</p>
                    <p><strong>Active body ID:</strong> {activeBodyId ?? 'None'}</p>
                    <p><strong>Body count:</strong> {bodyIds.length}</p>
                    <ul>
                        {bodyIds.map((bodyId) => <li key={bodyId}>{bodyId}</li>)}
                    </ul>
                    <p><strong>Face patches:</strong> {tessellation?.facePatches.length ?? 0}</p>
                    <p><strong>Edge polylines:</strong> {tessellation?.edgePolylines.length ?? 0}</p>
                    <h3>Diagnostics</h3>
                    {diagnostics.length === 0 ? <p>None</p> : (
                        <ul>
                            {diagnostics.map((diagnostic, index) => (
                                <li key={`${diagnostic.code}-${index}`}>
                                    [{diagnostic.severity}] {diagnostic.code}: {diagnostic.message}
                                </li>
                            ))}
                        </ul>
                    )}
                </aside>
            </main>
        </div>
    );
}

export default App;
