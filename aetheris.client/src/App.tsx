import { useCallback, useMemo, useState } from 'react';
import './App.css';
import {
    ApiError,
    createBox,
    createDocument,
    getDocumentSummary,
    pickBody,
    tessellateBody,
    type DiagnosticDto,
    type PickHitDto,
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
    const [pickStatus, setPickStatus] = useState<RequestStatus>('idle');
    const [pickMessage, setPickMessage] = useState<string>('Click in the viewport to run nearest-hit pick.');
    const [pickDiagnostics, setPickDiagnostics] = useState<DiagnosticDto[]>([]);
    const [pickHits, setPickHits] = useState<PickHitDto[]>([]);

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
            setPickStatus('idle');
            setPickMessage('Click in the viewport to run nearest-hit pick.');
            setPickDiagnostics([]);
            setPickHits([]);
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
            setPickStatus('idle');
            setPickMessage('Click in the viewport to run nearest-hit pick.');
            setPickDiagnostics([]);
            setPickHits([]);
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

    const handlePickRay = useCallback(async (origin: { x: number; y: number; z: number }, direction: { x: number; y: number; z: number }) => {
        if (!documentId || !activeBodyId) {
            setPickStatus('error');
            setPickMessage('Cannot pick before a document and active body exist.');
            setPickDiagnostics([]);
            setPickHits([]);
            return;
        }

        setPickStatus('loading');
        setPickMessage('Picking (nearest-only)...');
        setPickDiagnostics([]);

        try {
            const pickResponse = await pickBody(documentId, activeBodyId, {
                origin,
                direction,
                tessellationOptions: null,
                pickOptions: {
                    nearestOnly: true,
                },
            });

            setPickStatus('success');
            setPickHits(pickResponse.hits);
            setPickMessage(pickResponse.hits.length === 0
                ? 'No hit for current click ray.'
                : `Picked ${pickResponse.hits[0].entityKind} (nearest hit).`);
        } catch (error) {
            const apiError = error instanceof ApiError
                ? error
                : new ApiError((error as Error).message || 'Unexpected pick error.', []);
            setPickStatus('error');
            setPickMessage(apiError.message);
            setPickDiagnostics(apiError.diagnostics);
            setPickHits([]);
        }
    }, [activeBodyId, documentId]);

    const sceneData = useMemo(() => (tessellation ? mapTessellationToRenderData(tessellation) : null), [tessellation]);
    const nearestHit = pickHits[0] ?? null;
    const highlightedFaceId = nearestHit?.entityKind === 'Face' ? nearestHit.faceId : null;
    const highlightedEdgeId = nearestHit?.entityKind === 'Edge' ? nearestHit.edgeId : null;

    return (
        <div className="app-shell">
            <header className="toolbar">
                <h1>Aetheris Viewer Shell (M18.5)</h1>
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
                <ViewerViewport
                    sceneData={sceneData}
                    highlightedFaceId={highlightedFaceId}
                    highlightedEdgeId={highlightedEdgeId}
                    onPickRay={(origin, direction) => void handlePickRay(origin, direction)}
                />
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
                    <h3>Pick Behavior (M18.5)</h3>
                    <p>Single left click in viewport sends world-space camera ray to backend.</p>
                    <p>Pick mode: nearest-only. Backface handling: backend default (not overridden).</p>
                    <p><strong>Pick status:</strong> {pickStatus}</p>
                    <p><strong>Pick message:</strong> {pickMessage}</p>
                    <p><strong>Pick hits:</strong> {pickHits.length}</p>
                    {nearestHit ? (
                        <ul>
                            <li><strong>Kind:</strong> {nearestHit.entityKind}</li>
                            <li><strong>Face ID:</strong> {nearestHit.faceId ?? 'n/a'}</li>
                            <li><strong>Edge ID:</strong> {nearestHit.edgeId ?? 'n/a'}</li>
                            <li><strong>t:</strong> {nearestHit.t.toFixed(5)}</li>
                            <li><strong>Point:</strong> ({nearestHit.point.x.toFixed(5)}, {nearestHit.point.y.toFixed(5)}, {nearestHit.point.z.toFixed(5)})</li>
                            <li>
                                <strong>Normal:</strong>{' '}
                                {nearestHit.normal
                                    ? `(${nearestHit.normal.x.toFixed(5)}, ${nearestHit.normal.y.toFixed(5)}, ${nearestHit.normal.z.toFixed(5)})`
                                    : 'n/a'}
                            </li>
                        </ul>
                    ) : <p>No nearest hit to display.</p>}
                    <h3>Pick Diagnostics</h3>
                    {pickDiagnostics.length === 0 ? <p>None</p> : (
                        <ul>
                            {pickDiagnostics.map((diagnostic, index) => (
                                <li key={`pick-${diagnostic.code}-${index}`}>
                                    [{diagnostic.severity}] {diagnostic.code}: {diagnostic.message}
                                </li>
                            ))}
                        </ul>
                    )}
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
