import { useCallback, useEffect, useMemo, useState } from 'react';
import './App.css';
import {
    ApiError,
    createBox,
    createDocument,
    executeBoolean,
    exportDefinitionStep,
    getDocumentSummary,
    importStep,
    pickBody,
    tessellateBody,
    translateBody,
    type BooleanOperation,
    type BodyOccurrenceSummaryDto,
    type DiagnosticDto,
    type PickHitDto,
    type TessellationResponseDto,
} from './api/aetherisApi';
import { StepImportDropzone } from './components/StepImportDropzone';
import { Button } from './components/ui/button';
import { ViewerViewport } from './viewer/ViewerViewport';
import { mapTessellationToRenderData } from './viewer/tessellationMapper';
import { STEP_UPLOAD_LIMIT_BYTES, STEP_UPLOAD_LIMIT_MB, formatMegabytes } from './config/stepUpload';

type RequestStatus = 'idle' | 'loading' | 'success' | 'error';
type BooleanOperationUi = 'Union' | 'Subtract' | 'Intersect';
type TopLevelTab = 'viewer' | 'modeling-demo';
type ServerStatus = 'connecting' | 'connected' | 'disconnected' | 'error';
type DocumentStatus = 'creating' | 'ready' | 'error';
type ImportStatus = 'idle' | 'creating' | 'importing' | 'success' | 'error';

const BOOLEAN_OP_TO_API: Record<BooleanOperationUi, BooleanOperation> = {
    Union: 'union',
    Subtract: 'subtract',
    Intersect: 'intersect',
};

function App() {
    const [activeTab, setActiveTab] = useState<TopLevelTab>('viewer');
    const [documentId, setDocumentId] = useState<string | null>(null);
    const [bodyIds, setBodyIds] = useState<string[]>([]);
    const [occurrences, setOccurrences] = useState<BodyOccurrenceSummaryDto[]>([]);
    const [activeBodyId, setActiveBodyId] = useState<string | null>(null);
    const [tessellation, setTessellation] = useState<TessellationResponseDto | null>(null);
    const [status, setStatus] = useState<RequestStatus>('idle');
    const [statusMessage, setStatusMessage] = useState<string>('Ready. Create a document to begin.');
    const [serverStatus, setServerStatus] = useState<ServerStatus>('connecting');
    const [documentStatus, setDocumentStatus] = useState<DocumentStatus>('creating');
    const [importStatus, setImportStatus] = useState<ImportStatus>('creating');
    const [importStatusMessage, setImportStatusMessage] = useState('Preparing workspace…');
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
    const [stepImportFile, setStepImportFile] = useState<File | null>(null);
    const [stepDropzoneResetToken, setStepDropzoneResetToken] = useState(0);
    const [stepCanonicalHash, setStepCanonicalHash] = useState<string | null>(null);
    const [copyHashMessage, setCopyHashMessage] = useState('');
    const [isImporting, setIsImporting] = useState(false);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [isResetting, setIsResetting] = useState(false);
    const [isGridVisible, setIsGridVisible] = useState(true);
    const [isCoordVisible, setIsCoordVisible] = useState(true);

    const resetSessionState = useCallback(() => {
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
        setStepImportFile(null);
        setStepDropzoneResetToken((value) => value + 1);
        setStepCanonicalHash(null);
        setCopyHashMessage('');
        setDiagnostics([]);
        setImportStatusMessage('Preparing workspace…');
    }, []);

    const createFreshDocument = useCallback(async () => {
        setServerStatus('connecting');
        setDocumentStatus('creating');
        setImportStatus('creating');
        setImportStatusMessage('Preparing workspace…');
        setStatus('loading');
        setStatusMessage('Preparing workspace...');

        try {
            const created = await createDocument('STEP 242 Viewer UI');
            setDocumentId(created.documentId);
            setServerStatus('connected');
            setDocumentStatus('ready');
            setImportStatus('idle');
            setImportStatusMessage('Ready. Select a file to import.');
            setStatus('success');
            setStatusMessage('Workspace ready.');
        } catch (error) {
            const apiError = error instanceof ApiError
                ? error
                : new ApiError((error as Error).message || 'Unexpected error.', []);
            setDocumentId(null);
            setServerStatus('error');
            setDocumentStatus('error');
            setImportStatus('error');
            setImportStatusMessage(`Import error: ${apiError.message}`);
            setStatus('error');
            setStatusMessage(apiError.message);
            setDiagnostics(apiError.diagnostics);
        }
    }, []);

    const runAction = useCallback(async (actionName: string, action: () => Promise<void>) => {
        setStatus('loading');
        setStatusMessage(`${actionName}...`);
        setDiagnostics([]);

        try {
            await action();
            setStatus('success');
            setStatusMessage(`${actionName} complete.`);
            return true;
        } catch (error) {
            const apiError = error instanceof ApiError
                ? error
                : new ApiError((error as Error).message || 'Unexpected error.', []);
            setStatus('error');
            setStatusMessage(apiError.message);
            setDiagnostics(apiError.diagnostics);
            return false;
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
        setIsResetting(true);
        resetSessionState();
        await createFreshDocument();
        setIsResetting(false);
    }, [createFreshDocument, resetSessionState]);

    useEffect(() => {
        void handleCreateDocument();
    }, [handleCreateDocument]);

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

        setIsRefreshing(true);
        await runAction('Refresh tessellation', async () => {
            const tessellated = await tessellateBody(documentId, activeBodyId);
            setTessellation(tessellated);
        });
        setIsRefreshing(false);
    }, [activeBodyId, documentId, runAction]);

    const activeOccurrence = useMemo(
        () => occurrences.find((item) => item.occurrenceId === activeBodyId) ?? null,
        [activeBodyId, occurrences]);

    const handleExportActiveStep = useCallback(async () => {
        if (!documentId || !activeBodyId) {
            return;
        }

        if (!activeOccurrence) {
            setStatus('error');
            setStatusMessage('Active occurrence metadata is unavailable for STEP export.');
            setDiagnostics([]);
            return;
        }

        await runAction('Export active STEP', async () => {
            const exported = await exportDefinitionStep(documentId, activeOccurrence.definitionId);
            setStepExportText(exported.stepText);
            setStepCanonicalHash(exported.canonicalHash);
            setCopyHashMessage('');
        });
    }, [activeBodyId, activeOccurrence, documentId, runAction]);

    const handleDownloadCanonicalStep = useCallback(async () => {
        if (!documentId || !activeOccurrence) {
            return;
        }

        await runAction('Download canonical STEP 242', async () => {
            const exported = await exportDefinitionStep(documentId, activeOccurrence.definitionId);
            const blob = new Blob([exported.stepText], { type: 'application/step; charset=utf-8' });
            const objectUrl = URL.createObjectURL(blob);

            try {
                const anchor = document.createElement('a');
                anchor.href = objectUrl;
                anchor.download = `aetheris-${activeOccurrence.definitionId}.step`;
                document.body.appendChild(anchor);
                anchor.click();
                document.body.removeChild(anchor);
            } finally {
                URL.revokeObjectURL(objectUrl);
            }
        });
    }, [activeOccurrence, documentId, runAction]);

    const handleCopyCanonicalHash = useCallback(async () => {
        if (!stepCanonicalHash) {
            return;
        }

        try {
            await navigator.clipboard.writeText(stepCanonicalHash);
            setCopyHashMessage('Copied');
        } catch {
            setCopyHashMessage('Clipboard unavailable');
        }
    }, [stepCanonicalHash]);

    const handleImportStep = useCallback(async () => {
        if (!documentId || !stepImportFile || documentStatus !== 'ready' || serverStatus !== 'connected') {
            return;
        }

        if (stepImportFile.size <= 0) {
            setStatus('error');
            setStatusMessage('Selected STEP file is empty.');
            setDiagnostics([]);
            setImportStatus('error');
            setImportStatusMessage('Import error: Selected STEP file is empty.');
            return;
        }

        if (stepImportFile.size > STEP_UPLOAD_LIMIT_BYTES) {
            const limitMessage = `Selected STEP file is too large (${formatMegabytes(stepImportFile.size)}). Limit is ${STEP_UPLOAD_LIMIT_MB} MB.`;
            setStatus('error');
            setStatusMessage(limitMessage);
            setDiagnostics([]);
            setImportStatus('error');
            setImportStatusMessage(`Import error: ${limitMessage}`);
            return;
        }

        setImportStatus('importing');
        setImportStatusMessage('Importing STEP…');
        setIsImporting(true);

        try {
            const didImport = await runAction('Import STEP', async () => {
                const stepText = await stepImportFile.text();
                if (stepText.trim().length === 0) {
                    throw new ApiError('Selected STEP file is empty.', []);
                }

                const imported = await importStep(documentId, stepText);
                setStepExportText('');
                await refreshSummaryAndActiveTessellation(imported.occurrenceId);
                const exported = await exportDefinitionStep(documentId, imported.definitionId);
                setStepCanonicalHash(exported.canonicalHash);
                setPickStatus('idle');
                setPickMessage(`Imported occurrence ${imported.occurrenceId} is now active.`);
                setPickDiagnostics([]);
                setPickHits([]);
                setCopyHashMessage('');
            });
            setImportStatus(didImport ? 'success' : 'error');
            setImportStatusMessage(didImport ? 'Import complete.' : 'Import error: Request failed.');
        } finally {
            setIsImporting(false);
        }
    }, [documentId, documentStatus, refreshSummaryAndActiveTessellation, runAction, serverStatus, stepImportFile]);

    const handleStepFileAccepted = useCallback((selected: File) => {
        setStepImportFile(selected);
    }, []);

    const handleStepFileValidationError = useCallback((_message: string) => {
        setStepImportFile(null);
    }, []);

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
    const canImportStep = Boolean(
        serverStatus === 'connected'
        && documentStatus === 'ready'
        && stepImportFile
        && !isImporting
        && status !== 'loading');
    const canExecuteBoolean = Boolean(
        documentId
        && bodyIds.length >= 2
        && booleanTargetBodyId
        && booleanToolBodyId
        && booleanTargetBodyId !== booleanToolBodyId
        && status !== 'loading');
    const serverStatusLabel: Record<ServerStatus, string> = {
        connecting: 'Server: Connecting',
        connected: 'Server: Connected',
        disconnected: 'Server: Disconnected',
        error: 'Server: Error',
    };
    const documentStatusLabel: Record<DocumentStatus, string> = {
        creating: 'Document: Preparing',
        ready: 'Document: Ready',
        error: 'Document: Error',
    };
    const importStatusTone = importStatus === 'error' ? 'error' : (importStatus === 'success' ? 'success' : 'neutral');

    return (
        <div className="app-shell">
            <header className="top-bar">
                <div className="top-bar__header-row">
                    <div className="top-bar__wordmark" aria-label="AETHERIS CADMATA">
                        <span className="top-bar__wordmark-primary">AETHERIS</span>
                        <span className="top-bar__wordmark-secondary">CADMATA</span>
                    </div>
                    <div className="top-bar__actions-block">
                        <div className="top-bar__actions">
                            <Button type="button" variant="outline" onClick={() => void handleCreateDocument()} disabled={status === 'loading'}>
                                {isResetting ? 'Preparing…' : 'New Document'}
                            </Button>
                            <Button
                                type="button"
                                variant="outline"
                                onClick={() => void handleRefreshTessellation()}
                                disabled={documentStatus !== 'ready' || !activeBodyId || status === 'loading' || isRefreshing}>
                                Refresh Tessellation
                            </Button>
                        </div>
                        <div className="status-row" role="status" aria-live="polite">
                            <span className={`status-pill status-pill--${serverStatus}`}>{serverStatusLabel[serverStatus]}</span>
                            <span className={`status-pill status-pill--${documentStatus}`}>{documentStatusLabel[documentStatus]}</span>
                        </div>
                    </div>
                </div>
                <div className="top-bar__tabs-row">
                    <div className="top-bar__tabs" role="tablist" aria-label="Top-level product surface">
                        <Button
                            type="button"
                            role="tab"
                            variant={activeTab === 'viewer' ? 'default' : 'secondary'}
                            aria-selected={activeTab === 'viewer'}
                            className={activeTab === 'viewer' ? 'tab-button active' : 'tab-button'}
                            onClick={() => setActiveTab('viewer')}>
                            STEP 242 Viewer
                        </Button>
                        <Button
                            type="button"
                            role="tab"
                            variant={activeTab === 'modeling-demo' ? 'default' : 'secondary'}
                            aria-selected={activeTab === 'modeling-demo'}
                            className={activeTab === 'modeling-demo' ? 'tab-button active' : 'tab-button'}
                            onClick={() => setActiveTab('modeling-demo')}>
                            <span>Modeling Demo</span> <span className="experimental-badge">(Experimental)</span>
                        </Button>
                    </div>
                </div>
            </header>

            <main className="main-layout">
                <section className="viewport-column">
                    <div className="viewport-frame">
                        <div className="viewport-controls" role="group" aria-label="Viewport display controls">
                            <button
                                type="button"
                                className={isGridVisible ? 'viewport-segmented__button is-active' : 'viewport-segmented__button'}
                                onClick={() => setIsGridVisible((value) => !value)}
                                aria-pressed={isGridVisible}>
                                GRID
                            </button>
                            <button
                                type="button"
                                className={isCoordVisible ? 'viewport-segmented__button is-active' : 'viewport-segmented__button'}
                                onClick={() => setIsCoordVisible((value) => !value)}
                                aria-pressed={isCoordVisible}>
                                COORD
                            </button>
                        </div>
                        <ViewerViewport
                            sceneData={sceneData}
                            highlightedFaceId={highlightedFaceId}
                            highlightedEdgeId={highlightedEdgeId}
                            showGrid={isGridVisible}
                            showAxisGuide={isCoordVisible}
                            onPickRay={(origin, direction) => void handlePickRay(origin, direction)}
                        />
                    </div>
                </section>

                <aside className="tool-rail">
                    {activeTab === 'viewer' ? (
                        <>
                            <section className="tool-section tool-section--import">
                                <h2 className="section-title">Step Import</h2>
                                <StepImportDropzone
                                    resetToken={stepDropzoneResetToken}
                                    onFileAccepted={handleStepFileAccepted}
                                    onValidationError={handleStepFileValidationError}
                                />
                                <Button type="button" onClick={() => void handleImportStep()} disabled={!canImportStep}>Import STEP 242</Button>
                                <div className={`import-status-box import-status-box--${importStatusTone}`}>
                                    <p className="import-status-box__label"><strong>Import Status</strong></p>
                                    {importStatus === 'error' ? (
                                        <>
                                            <p className="import-status-box__summary">Import failed</p>
                                            <p className="import-status-box__detail">{statusMessage}</p>
                                            {diagnostics.length === 0 ? null : (
                                                <ul className="import-status-box__details-list">
                                                    {diagnostics.map((diagnostic, index) => (
                                                        <li key={`${diagnostic.code}-${index}`}>
                                                            [{diagnostic.severity}] {diagnostic.code}: {diagnostic.message}
                                                        </li>
                                                    ))}
                                                </ul>
                                            )}
                                        </>
                                    ) : (
                                        <p>{importStatusMessage}</p>
                                    )}
                                </div>
                                {importStatus === 'error' || diagnostics.length === 0 ? null : (
                                    <ul>
                                        {diagnostics.map((diagnostic, index) => (
                                            <li key={`${diagnostic.code}-${index}`}>
                                                [{diagnostic.severity}] {diagnostic.code}: {diagnostic.message}
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </section>

                            <section className="tool-section">
                                <h2 className="section-title">Step Export</h2>
                                <div className="stack-row">
                                    <Button type="button" variant="outline" onClick={() => void handleDownloadCanonicalStep()} disabled={!documentId || !activeOccurrence || status === 'loading'}>
                                        Download Canonical 242
                                    </Button>
                                    <Button type="button" variant="outline" onClick={() => void handleExportActiveStep()} disabled={!activeBodyId || status === 'loading'}>
                                        Export Active (STEP)
                                    </Button>
                                </div>
                                <details>
                                    <summary>Copy STEP text</summary>
                                    <label className="textarea-label">
                                        Canonical STEP Text
                                        <textarea value={stepExportText} readOnly placeholder="Exported STEP text will appear here." rows={7} />
                                    </label>
                                </details>
                            </section>

                            <section className="tool-section audit-panel">
                                <h2 className="section-title">Inspector</h2>
                                <div className="inspector-row">
                                    <span>Canonical SHA256</span>
                                    <code className="mono-value">{stepCanonicalHash ?? 'Not available'}</code>
                                    <Button type="button" size="sm" variant="outline" onClick={() => void handleCopyCanonicalHash()} disabled={!stepCanonicalHash}>Copy</Button>
                                </div>
                                {copyHashMessage ? <p>{copyHashMessage}</p> : null}
                                <p><strong>Definition ID:</strong> {activeOccurrence?.definitionId ?? 'None'}</p>
                                <p><strong>Occurrence ID:</strong> {activeBodyId ?? 'None'}</p>
                                <p><strong>Face count:</strong> {tessellation?.facePatches.length ?? 0}</p>
                                <p><strong>Edge count:</strong> {tessellation?.edgePolylines.length ?? 0}</p>
                                <p><strong>Shell count:</strong> {activeBodyId ? 1 : 0}</p>
                            </section>
                        </>
                    ) : (
                        <>
                            <section className="tool-section">
                                <h2 className="section-title">Modeling Demo Notice</h2>
                                <p>Modeling Demo (Non-production)</p>
                                <p className="demo-notice">This is a demo environment. Not part of Viewer v0 contract.</p>
                            </section>

                            <section className="tool-section">
                                <h2 className="section-title">Create Box</h2>
                                <div className="form-grid">
                                    <label>Width <input type="number" value={boxWidth} onChange={(event) => setBoxWidth(event.target.value)} /></label>
                                    <label>Height <input type="number" value={boxHeight} onChange={(event) => setBoxHeight(event.target.value)} /></label>
                                    <label>Depth <input type="number" value={boxDepth} onChange={(event) => setBoxDepth(event.target.value)} /></label>
                                </div>
                                <button type="button" onClick={() => void handleCreateBox()} disabled={!documentId || status === 'loading'}>Create Box</button>
                            </section>

                            <section className="tool-section">
                                <h2 className="section-title">Body List</h2>
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

                            <section className="tool-section">
                                <h2 className="section-title">Translate Active Body</h2>
                                <div className="form-grid">
                                    <label>X <input type="number" value={tx} onChange={(event) => setTx(event.target.value)} /></label>
                                    <label>Y <input type="number" value={ty} onChange={(event) => setTy(event.target.value)} /></label>
                                    <label>Z <input type="number" value={tz} onChange={(event) => setTz(event.target.value)} /></label>
                                </div>
                                <button type="button" onClick={() => void handleApplyTranslation()} disabled={!activeBodyId || status === 'loading'}>Apply Translation</button>
                            </section>

                            <section className="tool-section">
                                <h2 className="section-title">Boolean (Two-body)</h2>
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
                                <div className="stack-row boolean-actions">
                                    <button type="button" onClick={handleUseActiveBodyAsTarget} disabled={!activeBodyId || status === 'loading'}>Use Active as Target</button>
                                    <button type="button" onClick={handleUseActiveBodyAsTool} disabled={!activeBodyId || status === 'loading'}>Use Active as Tool</button>
                                </div>
                                <button type="button" onClick={() => void handleExecuteBoolean()} disabled={!canExecuteBoolean}>Execute Boolean</button>
                                {bodyIds.length < 2 ? <p>Need at least 2 bodies to run a boolean.</p> : null}
                                {booleanTargetBodyId && booleanToolBodyId && booleanTargetBodyId === booleanToolBodyId ? (
                                    <p>Target and tool must be different body IDs.</p>
                                ) : null}
                            </section>

                            <section className="tool-section">
                                <h2 className="section-title">Debug/Status</h2>
                                <p><strong>Request status:</strong> {status}</p>
                                <p><strong>Message:</strong> {statusMessage}</p>
                                <p><strong>Document ID:</strong> {documentId ?? 'None'}</p>
                                <p><strong>Active occurrence ID:</strong> {activeBodyId ?? 'None'}</p>
                                <p><strong>Occurrence count:</strong> {bodyIds.length}</p>
                                <p><strong>Face patches:</strong> {tessellation?.facePatches.length ?? 0}</p>
                                <p><strong>Edge polylines:</strong> {tessellation?.edgePolylines.length ?? 0}</p>
                                <h3 className="section-title section-title--sub">Pick Diagnostics (active body only)</h3>
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
                                <h3 className="section-title section-title--sub">Pick Diagnostics</h3>
                                {pickDiagnostics.length === 0 ? <p>None</p> : (
                                    <ul>
                                        {pickDiagnostics.map((diagnostic, index) => (
                                            <li key={`pick-${diagnostic.code}-${index}`}>
                                                [{diagnostic.severity}] {diagnostic.code}: {diagnostic.message}
                                            </li>
                                        ))}
                                    </ul>
                                )}
                                <h3 className="section-title section-title--sub">Diagnostics</h3>
                                {diagnostics.length === 0 ? <p>None</p> : (
                                    <ul>
                                        {diagnostics.map((diagnostic, index) => (
                                            <li key={`${diagnostic.code}-${index}`}>
                                                [{diagnostic.severity}] {diagnostic.code}: {diagnostic.message}
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </section>
                        </>
                    )}
                </aside>
            </main>
        </div>
    );
}

export default App;
