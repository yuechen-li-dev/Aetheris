import { useCallback, useMemo, useState } from 'react';
import './App.css';
import {
    ApiError,
    createBox,
    createDocument,
    createOccurrence,
    executeBoolean,
    getDocumentSummary,
    pickBody,
    tessellateBody,
    translateBody,
    type BodyOccurrenceSummaryDto,
    type BooleanOperation,
    type DiagnosticDto,
    type PickHitDto,
    type TessellationResponseDto,
} from './api/aetherisApi';
import { ViewerViewport } from './viewer/ViewerViewport';
import { mapTessellationToRenderData } from './viewer/tessellationMapper';

type RequestStatus = 'idle' | 'loading' | 'success' | 'error';
type BooleanOperationUi = 'Union' | 'Subtract' | 'Intersect';

const BOOLEAN_OP_TO_API: Record<BooleanOperationUi, BooleanOperation> = {
    Union: 'union',
    Subtract: 'subtract',
    Intersect: 'intersect',
};

function App() {
    const [documentId, setDocumentId] = useState<string | null>(null);
    const [occurrences, setOccurrences] = useState<BodyOccurrenceSummaryDto[]>([]);
    const [activeOccurrenceId, setActiveOccurrenceId] = useState<string | null>(null);
    const [tessellation, setTessellation] = useState<TessellationResponseDto | null>(null);
    const [status, setStatus] = useState<RequestStatus>('idle');
    const [statusMessage, setStatusMessage] = useState<string>('Ready. Create a document to begin.');
    const [diagnostics, setDiagnostics] = useState<DiagnosticDto[]>([]);
    const [pickStatus, setPickStatus] = useState<RequestStatus>('idle');
    const [pickMessage, setPickMessage] = useState<string>('Click in the viewport to run nearest-hit pick.');
    const [pickHits, setPickHits] = useState<PickHitDto[]>([]);
    const [boxWidth, setBoxWidth] = useState('1.75');
    const [boxHeight, setBoxHeight] = useState('1.25');
    const [boxDepth, setBoxDepth] = useState('1.1');
    const [tx, setTx] = useState('0');
    const [ty, setTy] = useState('0');
    const [tz, setTz] = useState('0');
    const [booleanTargetOccurrenceId, setBooleanTargetOccurrenceId] = useState<string>('');
    const [booleanToolOccurrenceId, setBooleanToolOccurrenceId] = useState<string>('');
    const [booleanOperation, setBooleanOperation] = useState<BooleanOperationUi>('Union');

    const runAction = useCallback(async (actionName: string, action: () => Promise<void>) => {
        setStatus('loading');
        setStatusMessage(`${actionName}...`);
        setDiagnostics([]);

        try {
            await action();
            setStatus('success');
            setStatusMessage(`${actionName} complete.`);
        } catch (error) {
            const apiError = error instanceof ApiError ? error : new ApiError((error as Error).message || 'Unexpected error.', []);
            setStatus('error');
            setStatusMessage(apiError.message);
            setDiagnostics(apiError.diagnostics);
        }
    }, []);

    const refreshSummaryAndActiveTessellation = useCallback(async (targetOccurrenceId?: string) => {
        if (!documentId) return;
        const summary = await getDocumentSummary(documentId);
        const fallback = summary.occurrences[0]?.occurrenceId ?? null;
        const selected = targetOccurrenceId ?? (summary.bodyIds.includes(activeOccurrenceId ?? '') ? activeOccurrenceId : fallback);

        setOccurrences(summary.occurrences);
        setActiveOccurrenceId(selected ?? null);

        if (selected) setTessellation(await tessellateBody(documentId, selected));
        else setTessellation(null);
    }, [activeOccurrenceId, documentId]);

    const handleCreateDocument = useCallback(async () => {
        await runAction('Create document', async () => {
            const created = await createDocument('Basic Modeling UI M25');
            setDocumentId(created.documentId);
            setOccurrences([]);
            setActiveOccurrenceId(null);
            setTessellation(null);
            setBooleanTargetOccurrenceId('');
            setBooleanToolOccurrenceId('');
            setPickHits([]);
            setPickStatus('idle');
            setPickMessage('Click in the viewport to run nearest-hit pick.');
        });
    }, [runAction]);

    const handleCreateBox = useCallback(async () => {
        if (!documentId) return;
        const width = Number(boxWidth); const height = Number(boxHeight); const depth = Number(boxDepth);
        if ([width, height, depth].some((v) => Number.isNaN(v) || v <= 0)) {
            setStatus('error');
            setStatusMessage('Box dimensions must be positive numbers.');
            return;
        }

        await runAction('Create box', async () => {
            const created = await createBox(documentId, width, height, depth);
            await refreshSummaryAndActiveTessellation(created.occurrenceId);
        });
    }, [boxDepth, boxHeight, boxWidth, documentId, refreshSummaryAndActiveTessellation, runAction]);

    const handleCreateOccurrence = useCallback(async () => {
        if (!documentId || !activeOccurrenceId) return;
        await runAction('Create body instance', async () => {
            const created = await createOccurrence(documentId, activeOccurrenceId);
            await refreshSummaryAndActiveTessellation(created.occurrenceId);
        });
    }, [activeOccurrenceId, documentId, refreshSummaryAndActiveTessellation, runAction]);

    const handleSelectOccurrence = useCallback(async (occurrenceId: string) => {
        if (!documentId) return;
        await runAction('Select active occurrence', async () => {
            setActiveOccurrenceId(occurrenceId);
            setTessellation(await tessellateBody(documentId, occurrenceId));
        });
    }, [documentId, runAction]);

    const handleApplyTranslation = useCallback(async () => {
        if (!documentId || !activeOccurrenceId) return;
        const x = Number(tx); const y = Number(ty); const z = Number(tz);
        if ([x, y, z].some((v) => Number.isNaN(v))) return;

        await runAction('Apply translation', async () => {
            await translateBody(documentId, activeOccurrenceId, { x, y, z });
            await refreshSummaryAndActiveTessellation(activeOccurrenceId);
        });
    }, [activeOccurrenceId, documentId, refreshSummaryAndActiveTessellation, runAction, tx, ty, tz]);

    const handleExecuteBoolean = useCallback(async () => {
        if (!documentId || !booleanTargetOccurrenceId || !booleanToolOccurrenceId || booleanTargetOccurrenceId === booleanToolOccurrenceId) return;

        await runAction(`Boolean ${booleanOperation}`, async () => {
            const result = await executeBoolean(documentId, {
                leftBodyId: booleanTargetOccurrenceId,
                rightBodyId: booleanToolOccurrenceId,
                operation: BOOLEAN_OP_TO_API[booleanOperation],
            });
            await refreshSummaryAndActiveTessellation(result.occurrenceId);
            setBooleanTargetOccurrenceId(result.occurrenceId);
        });
    }, [booleanOperation, booleanTargetOccurrenceId, booleanToolOccurrenceId, documentId, refreshSummaryAndActiveTessellation, runAction]);

    const handlePickRay = useCallback(async (origin: { x: number; y: number; z: number }, direction: { x: number; y: number; z: number }) => {
        if (!documentId || !activeOccurrenceId) return;
        setPickStatus('loading');
        try {
            const response = await pickBody(documentId, activeOccurrenceId, { origin, direction, tessellationOptions: null, pickOptions: { nearestOnly: true } });
            setPickHits(response.hits);
            setPickStatus('success');
            setPickMessage(response.hits.length ? `Picked ${response.hits[0].entityKind}.` : 'No hit for current click ray.');
        } catch (error) {
            const apiError = error instanceof ApiError ? error : new ApiError((error as Error).message || 'Unexpected pick error.', []);
            setPickStatus('error');
            setPickMessage(apiError.message);
        }
    }, [activeOccurrenceId, documentId]);

    const sceneData = useMemo(() => (tessellation ? mapTessellationToRenderData(tessellation) : null), [tessellation]);
    const nearestHit = pickHits[0] ?? null;

    return (
        <div className="app-shell">
            <header className="toolbar"><h1>Aetheris Modeling UI (M25)</h1><button type="button" onClick={() => void handleCreateDocument()}>Create Document</button></header>
            <main className="main-grid">
                <ViewerViewport sceneData={sceneData} highlightedFaceId={nearestHit?.faceId ?? null} highlightedEdgeId={nearestHit?.edgeId ?? null} onPickRay={(o, d) => void handlePickRay(o, d)} />
                <aside className="debug-panel">
                    <h3>Create Box</h3>
                    <div className="form-grid"><label>Width <input type="number" value={boxWidth} onChange={(e) => setBoxWidth(e.target.value)} /></label><label>Height <input type="number" value={boxHeight} onChange={(e) => setBoxHeight(e.target.value)} /></label><label>Depth <input type="number" value={boxDepth} onChange={(e) => setBoxDepth(e.target.value)} /></label></div>
                    <button type="button" onClick={() => void handleCreateBox()} disabled={!documentId || status === 'loading'}>Create Box</button>
                    <h3>Occurrences</h3>
                    {occurrences.map((occ) => <button key={occ.occurrenceId} className={occ.occurrenceId === activeOccurrenceId ? 'active-row' : ''} onClick={() => void handleSelectOccurrence(occ.occurrenceId)}>{occ.occurrenceId.slice(0, 8)} → {occ.definitionId.slice(0, 8)}</button>)}
                    <button type="button" onClick={() => void handleCreateOccurrence()} disabled={!activeOccurrenceId}>Create Instance from Active</button>
                    <h3>Translate Active Occurrence</h3>
                    <div className="form-grid"><label>X <input type="number" value={tx} onChange={(e) => setTx(e.target.value)} /></label><label>Y <input type="number" value={ty} onChange={(e) => setTy(e.target.value)} /></label><label>Z <input type="number" value={tz} onChange={(e) => setTz(e.target.value)} /></label></div>
                    <button type="button" onClick={() => void handleApplyTranslation()} disabled={!activeOccurrenceId}>Apply Translation</button>
                    <h3>Boolean</h3>
                    <select value={booleanTargetOccurrenceId} onChange={(e) => setBooleanTargetOccurrenceId(e.target.value)}><option value="">Target</option>{occurrences.map((o) => <option key={`t-${o.occurrenceId}`} value={o.occurrenceId}>{o.occurrenceId}</option>)}</select>
                    <select value={booleanToolOccurrenceId} onChange={(e) => setBooleanToolOccurrenceId(e.target.value)}><option value="">Tool</option>{occurrences.map((o) => <option key={`u-${o.occurrenceId}`} value={o.occurrenceId}>{o.occurrenceId}</option>)}</select>
                    <select value={booleanOperation} onChange={(e) => setBooleanOperation(e.target.value as BooleanOperationUi)}><option value="Union">Union</option><option value="Subtract">Subtract</option><option value="Intersect">Intersect</option></select>
                    <button type="button" onClick={() => void handleExecuteBoolean()}>Execute Boolean</button>
                    <p><strong>Status:</strong> {statusMessage}</p>
                    <p><strong>Document:</strong> {documentId ?? 'None'}</p>
                    <p><strong>Active occurrence:</strong> {activeOccurrenceId ?? 'None'}</p>
                    <p><strong>Occurrence count:</strong> {occurrences.length}</p>
                    <p><strong>Pick:</strong> {pickStatus} — {pickMessage}</p>
                    {nearestHit ? <p>Hit occurrence: {nearestHit.occurrenceId ?? 'n/a'} at ({nearestHit.point.x.toFixed(2)}, {nearestHit.point.y.toFixed(2)}, {nearestHit.point.z.toFixed(2)})</p> : null}
                    {diagnostics.map((d, i) => <p key={`${d.code}-${i}`}>[{d.severity}] {d.message}</p>)}
                </aside>
            </main>
        </div>
    );
}

export default App;
