import { useCallback, useMemo, useState } from 'react';
import './App.css';
import {
    ApiError,
    createBox,
    createDocument,
    getDocumentSummary,
    pickBody,
    tessellateBody,
    translateBody,
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
    const [boxWidth, setBoxWidth] = useState('1.75');
    const [boxHeight, setBoxHeight] = useState('1.25');
    const [boxDepth, setBoxDepth] = useState('1.1');
    const [tx, setTx] = useState('0');
    const [ty, setTy] = useState('0');
    const [tz, setTz] = useState('0');

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

    const refreshSummaryAndActiveTessellation = useCallback(async (targetBodyId?: string) => {
        if (!documentId) {
            return;
        }

        const summary = await getDocumentSummary(documentId);
        const selected = targetBodyId ?? (summary.bodyIds.includes(activeBodyId ?? '') ? activeBodyId : summary.bodyIds[0] ?? null);

        setBodyIds(summary.bodyIds);
        setActiveBodyId(selected ?? null);

        if (selected) {
            const tessellated = await tessellateBody(documentId, selected);
            setTessellation(tessellated);
        } else {
            setTessellation(null);
        }
    }, [activeBodyId, documentId]);

    const handleCreateDocument = useCallback(async () => {
        await runAction('Create document', async () => {
            const created = await createDocument('Basic Modeling UI M19');
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

        const width = Number(boxWidth);
        const height = Number(boxHeight);
        const depth = Number(boxDepth);

        if (width <= 0 || height <= 0 || depth <= 0 || Number.isNaN(width) || Number.isNaN(height) || Number.isNaN(depth)) {
            setStatus('error');
            setStatusMessage('Box dimensions must be positive numbers.');
            return;
        }

        await runAction('Create box', async () => {
            const created = await createBox(documentId, width, height, depth);
            await refreshSummaryAndActiveTessellation(created.bodyId);
            setPickStatus('idle');
            setPickMessage('Click in the viewport to run nearest-hit pick.');
            setPickDiagnostics([]);
            setPickHits([]);
        });
    }, [boxDepth, boxHeight, boxWidth, documentId, refreshSummaryAndActiveTessellation, runAction]);

    const handleSelectBody = useCallback(async (nextBodyId: string) => {
        if (!documentId) {
            return;
        }

        await runAction('Select active body', async () => {
            const tessellated = await tessellateBody(documentId, nextBodyId);
            setActiveBodyId(nextBodyId);
            setTessellation(tessellated);
            setPickStatus('idle');
            setPickMessage('Active body changed. Click in viewport to pick nearest hit.');
            setPickDiagnostics([]);
            setPickHits([]);
        });
    }, [documentId, runAction]);

    const handleApplyTranslation = useCallback(async () => {
        if (!documentId || !activeBodyId) {
            return;
        }

        const x = Number(tx);
        const y = Number(ty);
        const z = Number(tz);

        if ([x, y, z].some((value) => Number.isNaN(value))) {
            setStatus('error');
            setStatusMessage('Translation values must be valid numbers.');
            return;
        }

        await runAction('Apply translation', async () => {
            await translateBody(documentId, activeBodyId, { x, y, z });
            await refreshSummaryAndActiveTessellation(activeBodyId);
            setPickStatus('idle');
            setPickMessage('Body translated. Click in viewport to refresh nearest hit.');
            setPickDiagnostics([]);
            setPickHits([]);
        });
    }, [activeBodyId, documentId, refreshSummaryAndActiveTessellation, runAction, tx, ty, tz]);

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
                : `Picked ${pickResponse.hits[0].entityKind} (nearest hit on active body).`);
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
                <h1>Aetheris Basic Modeling UI (M19)</h1>
                <div className="toolbar-actions">
                    <button type="button" onClick={() => void handleCreateDocument()} disabled={status === 'loading'}>
                        Create Document
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
                    <h2>Modeling Controls</h2>
                    <section>
                        <h3>Create Box</h3>
                        <div className="form-grid">
                            <label>Width <input type="number" value={boxWidth} onChange={(event) => setBoxWidth(event.target.value)} /></label>
                            <label>Height <input type="number" value={boxHeight} onChange={(event) => setBoxHeight(event.target.value)} /></label>
                            <label>Depth <input type="number" value={boxDepth} onChange={(event) => setBoxDepth(event.target.value)} /></label>
                        </div>
                        <button type="button" onClick={() => void handleCreateBox()} disabled={!documentId || status === 'loading'}>Create Box</button>
                    </section>

                    <section>
                        <h3>Body List</h3>
                        {bodyIds.length === 0 ? <p>No bodies in document.</p> : (
                            <ul>
                                {bodyIds.map((bodyId) => (
                                    <li key={bodyId}>
                                        <button
                                            type="button"
                                            className={bodyId === activeBodyId ? 'active-row' : ''}
                                            onClick={() => void handleSelectBody(bodyId)}
                                            disabled={status === 'loading'}>
                                            {bodyId}
                                        </button>
                                    </li>
                                ))}
                            </ul>
                        )}
                    </section>

                    <section>
                        <h3>Translate Active Body</h3>
                        <div className="form-grid">
                            <label>X <input type="number" value={tx} onChange={(event) => setTx(event.target.value)} /></label>
                            <label>Y <input type="number" value={ty} onChange={(event) => setTy(event.target.value)} /></label>
                            <label>Z <input type="number" value={tz} onChange={(event) => setTz(event.target.value)} /></label>
                        </div>
                        <button type="button" onClick={() => void handleApplyTranslation()} disabled={!activeBodyId || status === 'loading'}>Apply Translation</button>
                    </section>

                    <h2>Debug / Status</h2>
                    <p><strong>Request status:</strong> {status}</p>
                    <p><strong>Message:</strong> {statusMessage}</p>
                    <p><strong>Document ID:</strong> {documentId ?? 'None'}</p>
                    <p><strong>Active body ID:</strong> {activeBodyId ?? 'None'}</p>
                    <p><strong>Body count:</strong> {bodyIds.length}</p>
                    <p><strong>Face patches:</strong> {tessellation?.facePatches.length ?? 0}</p>
                    <p><strong>Edge polylines:</strong> {tessellation?.edgePolylines.length ?? 0}</p>
                    <h3>Pick Diagnostics (active body only)</h3>
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
