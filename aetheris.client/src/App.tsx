import { useCallback, useMemo, useState } from 'react';
import './App.css';
import {
    ApiError,
    createBox,
    createDocument,
    executeBoolean,
    exportDefinitionStep,
    exportDocumentSnapshot,
    getDocumentSummary,
    importDocumentSnapshot,
    importStep,
    pickBody,
    tessellateBody,
    translateBody,
    type BooleanOperation,
    type DiagnosticDto,
    type BodyOccurrenceSummaryDto,
    type PickHitDto,
    type TessellationResponseDto,
    type DocumentSnapshotDto,
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
    const [bodyIds, setBodyIds] = useState<string[]>([]);
    const [occurrences, setOccurrences] = useState<BodyOccurrenceSummaryDto[]>([]);
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
    const [booleanTargetBodyId, setBooleanTargetBodyId] = useState<string>('');
    const [booleanToolBodyId, setBooleanToolBodyId] = useState<string>('');
    const [booleanOperation, setBooleanOperation] = useState<BooleanOperationUi>('Union');
    const [stepExportText, setStepExportText] = useState('');
    const [stepImportText, setStepImportText] = useState('');
    const [stepImportName, setStepImportName] = useState('Imported');
    const [snapshotExportText, setSnapshotExportText] = useState('');
    const [snapshotImportText, setSnapshotImportText] = useState('');

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
        setOccurrences(summary.occurrences ?? []);
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
            setOccurrences([]);
            setTessellation(null);
            setPickStatus('idle');
            setPickMessage('Click in the viewport to run nearest-hit pick.');
            setPickDiagnostics([]);
            setPickHits([]);
            setBooleanTargetBodyId('');
            setBooleanToolBodyId('');
            setBooleanOperation('Union');
            setStepExportText('');
            setStepImportText('');
            setStepImportName('Imported');
            setSnapshotExportText('');
            setSnapshotImportText('');
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
            setPickMessage('Occurrence translated. Click in viewport to refresh nearest hit.');
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


    const handleExportActiveStep = useCallback(async () => {
        if (!documentId || !activeBodyId) {
            return;
        }

        const activeOccurrence = occurrences.find((item) => item.occurrenceId === activeBodyId);
        if (!activeOccurrence) {
            setStatus('error');
            setStatusMessage('Active occurrence metadata is unavailable for STEP export.');
            setDiagnostics([]);
            return;
        }

        await runAction('Export active STEP', async () => {
            const text = await exportDefinitionStep(documentId, activeOccurrence.definitionId);
            setStepExportText(text);
        });
    }, [activeBodyId, documentId, occurrences, runAction]);

    const handleImportStep = useCallback(async () => {
        if (!documentId) {
            return;
        }

        await runAction('Import STEP', async () => {
            const imported = await importStep(documentId, stepImportText, stepImportName.trim().length === 0 ? undefined : stepImportName.trim());
            setStepExportText('');
            await refreshSummaryAndActiveTessellation(imported.occurrenceId);
            setStepImportName(imported.name ?? 'Imported');
            setPickStatus('idle');
            setPickMessage(`Imported occurrence ${imported.occurrenceId} is now active.`);
            setPickDiagnostics([]);
            setPickHits([]);
        });
    }, [documentId, refreshSummaryAndActiveTessellation, runAction, stepImportName, stepImportText]);

    const handleExportSnapshot = useCallback(async () => {
        if (!documentId) {
            return;
        }

        await runAction('Export snapshot', async () => {
            const snapshot = await exportDocumentSnapshot(documentId);
            setSnapshotExportText(JSON.stringify(snapshot, null, 2));
        });
    }, [documentId, runAction]);

    const handleImportSnapshot = useCallback(async () => {
        if (!documentId || snapshotImportText.trim().length === 0) {
            return;
        }

        await runAction('Import snapshot', async () => {
            const parsed = JSON.parse(snapshotImportText) as DocumentSnapshotDto;
            const result = await importDocumentSnapshot(documentId, parsed);
            setSnapshotExportText('');
            setStepExportText('');
            await refreshSummaryAndActiveTessellation(parsed.occurrences?.[0]?.occurrenceId ?? undefined);
            setPickStatus('idle');
            setPickMessage(`Snapshot import applied (${result.definitionCount} definitions, ${result.occurrenceCount} occurrences).`);
            setPickDiagnostics([]);
            setPickHits([]);
        });
    }, [documentId, refreshSummaryAndActiveTessellation, runAction, snapshotImportText]);

    const handleUseActiveBodyAsTarget = useCallback(() => {
        if (activeBodyId) {
            setBooleanTargetBodyId(activeBodyId);
        }
    }, [activeBodyId]);

    const handleUseActiveBodyAsTool = useCallback(() => {
        if (activeBodyId) {
            setBooleanToolBodyId(activeBodyId);
        }
    }, [activeBodyId]);

    const handleExecuteBoolean = useCallback(async () => {
        if (!documentId) {
            return;
        }

        if (!booleanTargetBodyId || !booleanToolBodyId) {
            setStatus('error');
            setStatusMessage('Boolean operation requires both target and tool occurrences.');
            setDiagnostics([]);
            return;
        }

        if (booleanTargetBodyId === booleanToolBodyId) {
            setStatus('error');
            setStatusMessage('Boolean target and tool must be different occurrences.');
            setDiagnostics([]);
            return;
        }

        await runAction(`Boolean ${booleanOperation}`, async () => {
            const result = await executeBoolean(documentId, {
                leftBodyId: booleanTargetBodyId,
                rightBodyId: booleanToolBodyId,
                operation: BOOLEAN_OP_TO_API[booleanOperation],
            });

            await refreshSummaryAndActiveTessellation(result.bodyId);
            setBooleanTargetBodyId(result.bodyId);
            setPickStatus('idle');
            setPickMessage(`Boolean ${booleanOperation} succeeded. Result body ${result.bodyId} is now active.`);
            setPickDiagnostics([]);
            setPickHits([]);
            setStatusMessage(`Boolean ${booleanOperation} succeeded: target ${booleanTargetBodyId}, tool ${booleanToolBodyId}, result ${result.bodyId}.`);
        });
    }, [booleanOperation, booleanTargetBodyId, booleanToolBodyId, documentId, refreshSummaryAndActiveTessellation, runAction]);

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
                : `Picked ${pickResponse.hits[0].entityKind} on occurrence ${pickResponse.hits[0].occurrenceId}.`);
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
    const canImportStep = Boolean(documentId && stepImportText.trim().length > 0 && status !== 'loading');
    const canImportSnapshot = Boolean(documentId && snapshotImportText.trim().length > 0 && status !== 'loading');
    const canExecuteBoolean = Boolean(
        documentId
        && bodyIds.length >= 2
        && booleanTargetBodyId
        && booleanToolBodyId
        && booleanTargetBodyId !== booleanToolBodyId
        && status !== 'loading');

    return (
        <div className="app-shell">
            <header className="toolbar">
                <h1>Aetheris Modeling UI (M20)</h1>
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
                        {bodyIds.length === 0 ? <p>No occurrences in document.</p> : (
                            <ul>
                                {bodyIds.map((bodyId) => {
                                    const occurrence = occurrences.find((item) => item.occurrenceId === bodyId);
                                    const label = occurrence
                                        ? `${bodyId} (def ${occurrence.definitionId.slice(0, 8)}, t=[${occurrence.translation.x.toFixed(2)}, ${occurrence.translation.y.toFixed(2)}, ${occurrence.translation.z.toFixed(2)}])`
                                        : bodyId;

                                    return <li key={bodyId}>
                                        <button
                                            type="button"
                                            className={bodyId === activeBodyId ? 'active-row' : ''}
                                            onClick={() => void handleSelectBody(bodyId)}
                                            disabled={status === 'loading'}>
                                            {label}
                                        </button>
                                    </li>;
                                })}
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

                    <section>
                        <h3>Boolean (Two-Body)</h3>
                        <div className="form-grid boolean-grid">
                            <label>
                                Target Body
                                <select value={booleanTargetBodyId} onChange={(event) => setBooleanTargetBodyId(event.target.value)}>
                                    <option value="">Select target body</option>
                                    {bodyIds.map((bodyId) => <option key={`target-${bodyId}`} value={bodyId}>{bodyId}</option>)}
                                </select>
                            </label>
                            <label>
                                Tool Body
                                <select value={booleanToolBodyId} onChange={(event) => setBooleanToolBodyId(event.target.value)}>
                                    <option value="">Select tool body</option>
                                    {bodyIds.map((bodyId) => <option key={`tool-${bodyId}`} value={bodyId}>{bodyId}</option>)}
                                </select>
                            </label>
                            <label>
                                Operation
                                <select value={booleanOperation} onChange={(event) => setBooleanOperation(event.target.value as BooleanOperationUi)}>
                                    <option value="Union">Union</option>
                                    <option value="Subtract">Subtract</option>
                                    <option value="Intersect">Intersect</option>
                                </select>
                            </label>
                        </div>
                        <div className="toolbar-actions boolean-actions">
                            <button type="button" onClick={handleUseActiveBodyAsTarget} disabled={!activeBodyId || status === 'loading'}>Use Active as Target</button>
                            <button type="button" onClick={handleUseActiveBodyAsTool} disabled={!activeBodyId || status === 'loading'}>Use Active as Tool</button>
                        </div>
                        <button type="button" onClick={() => void handleExecuteBoolean()} disabled={!canExecuteBoolean}>Execute Boolean</button>
                        {bodyIds.length < 2 ? <p>Need at least 2 bodies to run a boolean.</p> : null}
                        {booleanTargetBodyId && booleanToolBodyId && booleanTargetBodyId === booleanToolBodyId ? (
                            <p>Target and tool must be different body IDs.</p>
                        ) : null}
                    </section>


                    <section>
                        <h3>Snapshot</h3>
                        <button type="button" onClick={() => void handleExportSnapshot()} disabled={!documentId || status === 'loading'}>
                            Export Snapshot
                        </button>
                        <label className="textarea-label">
                            Exported Snapshot JSON
                            <textarea value={snapshotExportText} readOnly placeholder="Exported snapshot JSON will appear here." rows={7} />
                        </label>
                        <label className="textarea-label">
                            Import Snapshot JSON
                            <textarea value={snapshotImportText} onChange={(event) => setSnapshotImportText(event.target.value)} placeholder="Paste snapshot JSON here." rows={7} />
                        </label>
                        <button type="button" onClick={() => void handleImportSnapshot()} disabled={!canImportSnapshot}>Import Snapshot</button>
                    </section>

                    <section>
                        <h3>STEP I/O</h3>
                        <button type="button" onClick={() => void handleExportActiveStep()} disabled={!activeBodyId || status === 'loading'}>
                            Export Active (STEP)
                        </button>
                        <label className="textarea-label">
                            Exported STEP
                            <textarea value={stepExportText} readOnly placeholder="Exported STEP text will appear here." rows={7} />
                        </label>
                        <label className="textarea-label">
                            Import STEP Text
                            <textarea value={stepImportText} onChange={(event) => setStepImportText(event.target.value)} placeholder="Paste STEP text here." rows={7} />
                        </label>
                        <label>
                            Imported Name
                            <input type="text" value={stepImportName} onChange={(event) => setStepImportName(event.target.value)} />
                        </label>
                        <button type="button" onClick={() => void handleImportStep()} disabled={!canImportStep}>Import STEP</button>
                    </section>

                    <h2>Debug / Status</h2>
                    <p><strong>Request status:</strong> {status}</p>
                    <p><strong>Message:</strong> {statusMessage}</p>
                    <p><strong>Document ID:</strong> {documentId ?? 'None'}</p>
                    <p><strong>Active occurrence ID:</strong> {activeBodyId ?? 'None'}</p>
                    <p><strong>Occurrence count:</strong> {bodyIds.length}</p>
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
