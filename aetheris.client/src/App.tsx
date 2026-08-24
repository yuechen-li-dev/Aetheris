import {
	useCallback,
	useEffect,
	useMemo,
	useRef,
	useState,
	type CSSProperties,
	type ReactNode,
} from "react";
import { useDeusMachine } from "machinalayout/react";
import { machinaRegion } from "./application/shellLayout";
import "./App.css";
import {
	ApiError,
	claimStartupStep,
	createBox,
	createDocument,
	executeBoolean,
	exportDefinitionStep,
	getDocumentSummary,
	importStep,
	loadCadmataFixture,
	maximizePaperclips,
	pickBody,
	prepareBodyDisplay,
	prepareAssemblyDisplay,
	translateBody,
	type BooleanOperation,
	type BodyOccurrenceSummaryDto,
	type DiagnosticDto,
	type PickHitDto,
	type DisplayPreparationResponseDto,
	type AssemblyDisplayPacketDto,
	type PaperclipDemoResponseDto,
} from "./api/aetherisApi";
import { StepImportDropzone } from "./components/StepImportDropzone";
import { ProductGallery } from "./components/ProductGallery";
import { PropertyTable, type PropertyRecord } from "./components/PropertyTable";
import { SemanticInspector } from "./components/SemanticInspector";
import { Button } from "./components/ui/button";
import { AetherisViewport } from "./viewer/AetherisViewport";
import { buildDisplaySceneData } from "./viewer/displaySceneBuilder";
import {
	STEP_UPLOAD_LIMIT_BYTES,
	STEP_UPLOAD_LIMIT_MB,
	formatMegabytes,
} from "./config/stepUpload";
import { DEFAULT_CADMATA_LAYERS, type CadmataLayerVisibility } from "./viewer/CadmataOverlay";
import {
	parseCadmataVisualizationArtifact,
	resolveCadmataSelection,
	type CadmataVisualizationArtifact,
} from "./viewer/conceptVisualization";
import { resolvePublishedBrepEntity } from "./viewer/semanticInspection";
import { documentMachine, documentPhase } from "./application/documentMachine";
import { shellThemeCssVariables } from "./theme/shellTheme";
import { viewportThemeById, VIEWPORT_THEMES, type ViewportThemeId } from "./viewer/viewportTheme";
import {
	loadViewportThemePreference,
	saveViewportThemePreference,
} from "./viewer/viewportThemePreference";
import { DEFAULT_PMI_VISIBILITY, type PmiCategory, type PmiVisibility } from "./viewer/PmiAnnotationLayer";

type RequestStatus = "idle" | "loading" | "success" | "error";
type BooleanOperationUi = "Union" | "Subtract" | "Intersect";
type TopLevelTab = "gallery" | "viewer" | "paperclips" | "modeling-demo";
type ServerStatus = "connecting" | "connected" | "disconnected" | "error";
type DocumentStatus = "creating" | "ready" | "error";
type ImportStatus = "idle" | "creating" | "importing" | "success" | "error";

interface DisplayStatusSummary {
	summary: string;
	wireOnlyFaceCount: number;
	diagnosticOnlyFaceCount: number;
}

function AssemblyProductTree({
	packet,
	selectedId,
	onSelect,
}: {
	packet: AssemblyDisplayPacketDto;
	selectedId: string | null;
	onSelect: (stableId: string) => void;
}) {
	const children = new Map<string, typeof packet.occurrences>();
	for (const occurrence of packet.occurrences) {
		const key = occurrence.parentStableId ?? "";
		children.set(key, [...(children.get(key) ?? []), occurrence]);
	}
	const render = (parent: string, depth: number): ReactNode =>
		(children.get(parent) ?? []).map((occurrence) => (
			<div key={occurrence.stableId}>
				<button
					type="button"
					className={
						selectedId === occurrence.stableId
							? "semantic-tree__item active-row"
							: "semantic-tree__item"
					}
					style={{ paddingLeft: `${depth * 14 + 6}px` }}
					onClick={() => onSelect(occurrence.stableId)}
				>
					{occurrence.kind === "Assembly" ? "▾" : "◇"} {occurrence.name}
				</button>
				{render(occurrence.stableId, depth + 1)}
			</div>
		));
	return (
		<div className="semantic-tree" aria-label="Assembly product tree">
			{render("", 0)}
		</div>
	);
}

interface RefreshDisplayResult {
	preparation: DisplayPreparationResponseDto | null;
	error: ApiError | null;
}

const BOOLEAN_OP_TO_API: Record<BooleanOperationUi, BooleanOperation> = {
	Union: "union",
	Subtract: "subtract",
	Intersect: "intersect",
};

function createDisplayStatusSummary(
	preparation: DisplayPreparationResponseDto | null,
): DisplayStatusSummary | null {
	if (!preparation) {
		return null;
	}

	const wireOnlyFaceCount =
		preparation.faces?.filter((face) => face.status === "WireframeOnly").length ?? 0;
	const diagnosticOnlyFaceCount =
		preparation.faces?.filter((face) => face.status === "DiagnosticOnly").length ?? 0;

	if (preparation.status === "Partial") {
		return {
			summary: `Import complete. Display partial: ${wireOnlyFaceCount} wire-only face(s), ${diagnosticOnlyFaceCount} diagnostic-only face(s).`,
			wireOnlyFaceCount,
			diagnosticOnlyFaceCount,
		};
	}

	if (preparation.lane === "mixed-fallback") {
		return {
			summary: "Import complete. Display: mixed analytic + bounded mesh fallback.",
			wireOnlyFaceCount,
			diagnosticOnlyFaceCount,
		};
	}

	if (preparation.lane === "fallback-only") {
		return {
			summary: "Import complete. Display: bounded mesh fallback.",
			wireOnlyFaceCount,
			diagnosticOnlyFaceCount,
		};
	}

	return {
		summary: "Import complete.",
		wireOnlyFaceCount,
		diagnosticOnlyFaceCount,
	};
}

function App() {
	const { dispatch: dispatchDocumentEvent, state: documentLifecycleState } = useDeusMachine(
		documentMachine,
		() => ({
			source: null,
			documentId: null,
			error: null,
			revision: 0,
		}),
	);
	const [activeTab, setActiveTab] = useState<TopLevelTab>("viewer");
	const [viewportThemeId, setViewportThemeId] = useState<ViewportThemeId>(
		loadViewportThemePreference,
	);
	const [documentId, setDocumentId] = useState<string | null>(null);
	const [bodyIds, setBodyIds] = useState<string[]>([]);
	const [occurrences, setOccurrences] = useState<BodyOccurrenceSummaryDto[]>([]);
	const [activeBodyId, setActiveBodyId] = useState<string | null>(null);
	const [displayPreparation, setDisplayPreparation] =
		useState<DisplayPreparationResponseDto | null>(null);
	const [assemblyPacket, setAssemblyPacket] = useState<AssemblyDisplayPacketDto | null>(null);
	const [selectedAssemblyOccurrenceId, setSelectedAssemblyOccurrenceId] = useState<string | null>(
		null,
	);
	const [status, setStatus] = useState<RequestStatus>("idle");
	const [statusMessage, setStatusMessage] = useState<string>("Ready. Create a document to begin.");
	const [serverStatus, setServerStatus] = useState<ServerStatus>("connecting");
	const [documentStatus, setDocumentStatus] = useState<DocumentStatus>("creating");
	const [importStatus, setImportStatus] = useState<ImportStatus>("creating");
	const [importStatusMessage, setImportStatusMessage] = useState("Preparing workspace…");
	const [diagnostics, setDiagnostics] = useState<DiagnosticDto[]>([]);
	const [pickStatus, setPickStatus] = useState<RequestStatus>("idle");
	const [pickMessage, setPickMessage] = useState<string>(
		"Click in the viewport to run nearest-hit pick.",
	);
	const [pickDiagnostics, setPickDiagnostics] = useState<DiagnosticDto[]>([]);
	const [pickHits, setPickHits] = useState<PickHitDto[]>([]);
	const [boxWidth, setBoxWidth] = useState("1.75");
	const [boxHeight, setBoxHeight] = useState("1.25");
	const [boxDepth, setBoxDepth] = useState("1.1");
	const [paperclipWireDiameter, setPaperclipWireDiameter] = useState("1");
	const [paperclipOuterLegLength, setPaperclipOuterLegLength] = useState("15");
	const [paperclipInnerLegLength, setPaperclipInnerLegLength] = useState("14");
	const [paperclipOuterBendRadius, setPaperclipOuterBendRadius] = useState("5");
	const [paperclipInnerBendRadius, setPaperclipInnerBendRadius] = useState("3");
	const [paperclipResult, setPaperclipResult] = useState<PaperclipDemoResponseDto | null>(null);
	const [tx, setTx] = useState("0");
	const [ty, setTy] = useState("0");
	const [tz, setTz] = useState("0");
	const [booleanTargetBodyId, setBooleanTargetBodyId] = useState<string>("");
	const [booleanToolBodyId, setBooleanToolBodyId] = useState<string>("");
	const [booleanOperation, setBooleanOperation] = useState<BooleanOperationUi>("Union");
	const [stepExportText, setStepExportText] = useState("");
	const [stepImportFile, setStepImportFile] = useState<File | null>(null);
	const [stepDropzoneResetToken, setStepDropzoneResetToken] = useState(0);
	const [stepCanonicalHash, setStepCanonicalHash] = useState<string | null>(null);
	const [copyHashMessage, setCopyHashMessage] = useState("");
	const [isImporting, setIsImporting] = useState(false);
	const [isRefreshing, setIsRefreshing] = useState(false);
	const [isResetting, setIsResetting] = useState(false);
	const [isGridVisible, setIsGridVisible] = useState(true);
	const [isCoordVisible, setIsCoordVisible] = useState(true);
	const [cadmataArtifact, setCadmataArtifact] = useState<CadmataVisualizationArtifact | null>(null);
	const [cadmataLayers, setCadmataLayers] =
		useState<CadmataLayerVisibility>(DEFAULT_CADMATA_LAYERS);
	const [selectedCadmataId, setSelectedCadmataId] = useState<string | null>(null);
	const [isPmiVisible, setIsPmiVisible] = useState(true);
	const [pmiVisibility, setPmiVisibility] = useState<PmiVisibility>(DEFAULT_PMI_VISIBILITY);
	const startupStepClaimed = useRef(false);
	const viewportTheme = useMemo(() => viewportThemeById(viewportThemeId), [viewportThemeId]);
	useEffect(() => saveViewportThemePreference(viewportThemeId), [viewportThemeId]);
	const shellThemeVariables = useMemo(() => shellThemeCssVariables() as CSSProperties, []);
	const lifecyclePhase = documentPhase(documentLifecycleState);

	const resetSessionState = useCallback(() => {
		dispatchDocumentEvent({ type: "Close" });
		setBodyIds([]);
		setActiveBodyId(null);
		setOccurrences([]);
		setDisplayPreparation(null);
		setPickStatus("idle");
		setPickMessage("Click in the viewport to run nearest-hit pick.");
		setPickDiagnostics([]);
		setPickHits([]);
		setBooleanTargetBodyId("");
		setBooleanToolBodyId("");
		setBooleanOperation("Union");
		setStepExportText("");
		setStepImportFile(null);
		setStepDropzoneResetToken((value) => value + 1);
		setStepCanonicalHash(null);
		setCopyHashMessage("");
		setDiagnostics([]);
		setCadmataArtifact(null);
		setSelectedCadmataId(null);
		setIsPmiVisible(true);
		setPmiVisibility(DEFAULT_PMI_VISIBILITY);
		setImportStatusMessage("Preparing workspace…");
	}, [dispatchDocumentEvent]);

	const createFreshDocument = useCallback(async () => {
		setServerStatus("connecting");
		setDocumentStatus("creating");
		setImportStatus("creating");
		setImportStatusMessage("Preparing workspace…");
		setStatus("loading");
		setStatusMessage("Preparing workspace...");

		try {
			const created = await createDocument("STEP 242 Viewer UI");
			setDocumentId(created.documentId);
			setServerStatus("connected");
			setDocumentStatus("ready");
			setImportStatus("idle");
			setImportStatusMessage("Ready. Select a file to import.");
			setStatus("success");
			setStatusMessage("Workspace ready.");
		} catch (error) {
			const apiError =
				error instanceof ApiError
					? error
					: new ApiError((error as Error).message || "Unexpected error.", []);
			setDocumentId(null);
			setServerStatus("error");
			setDocumentStatus("error");
			setImportStatus("error");
			setImportStatusMessage(`Import error: ${apiError.message}`);
			setStatus("error");
			setStatusMessage(apiError.message);
			setDiagnostics(apiError.diagnostics);
		}
	}, []);

	const runAction = useCallback(async (actionName: string, action: () => Promise<void>) => {
		setStatus("loading");
		setStatusMessage(`${actionName}...`);
		setDiagnostics([]);

		try {
			await action();
			setStatus("success");
			setStatusMessage(`${actionName} complete.`);
			return true;
		} catch (error) {
			const apiError =
				error instanceof ApiError
					? error
					: new ApiError((error as Error).message || "Unexpected error.", []);
			setStatus("error");
			setStatusMessage(apiError.message);
			setDiagnostics(apiError.diagnostics);
			return false;
		}
	}, []);

	const refreshSummaryAndActiveTessellation = useCallback(
		async (targetBodyId?: string, suppressDisplayErrors = false): Promise<RefreshDisplayResult> => {
			if (!documentId) {
				return { preparation: null, error: null };
			}

			const summary = await getDocumentSummary(documentId);
			const selected =
				targetBodyId ??
				(summary.bodyIds.includes(activeBodyId ?? "")
					? activeBodyId
					: (summary.bodyIds[0] ?? null));

			setBodyIds(summary.bodyIds);
			setOccurrences(summary.occurrences ?? []);
			setActiveBodyId(selected ?? null);

			if (selected) {
				try {
					const preparedDisplay = await prepareBodyDisplay(documentId, selected);
					setDisplayPreparation(preparedDisplay);
					return { preparation: preparedDisplay, error: null };
				} catch (error) {
					const apiError =
						error instanceof ApiError
							? error
							: new ApiError(
									(error as Error).message || "Unexpected display preparation error.",
									[],
								);
					setDisplayPreparation(null);
					if (!suppressDisplayErrors) {
						throw apiError;
					}

					return { preparation: null, error: apiError };
				}
			} else {
				setDisplayPreparation(null);
			}

			return { preparation: null, error: null };
		},
		[activeBodyId, documentId],
	);

	const handleCreateDocument = useCallback(async () => {
		setIsResetting(true);
		resetSessionState();
		await createFreshDocument();
		setIsResetting(false);
	}, [createFreshDocument, resetSessionState]);

	useEffect(() => {
		// Document creation is the startup synchronization with the server.
		// eslint-disable-next-line react-hooks/set-state-in-effect
		void handleCreateDocument();
	}, [handleCreateDocument]);

	const handleCreateBox = useCallback(async () => {
		if (!documentId) {
			return;
		}

		const width = Number(boxWidth);
		const height = Number(boxHeight);
		const depth = Number(boxDepth);

		if (
			width <= 0 ||
			height <= 0 ||
			depth <= 0 ||
			Number.isNaN(width) ||
			Number.isNaN(height) ||
			Number.isNaN(depth)
		) {
			setStatus("error");
			setStatusMessage("Box dimensions must be positive numbers.");
			return;
		}

		await runAction("Create box", async () => {
			const created = await createBox(documentId, width, height, depth);
			await refreshSummaryAndActiveTessellation(created.bodyId);
			setPickStatus("idle");
			setPickMessage("Click in the viewport to run nearest-hit pick.");
			setPickDiagnostics([]);
			setPickHits([]);
		});
	}, [boxDepth, boxHeight, boxWidth, documentId, refreshSummaryAndActiveTessellation, runAction]);

	const handleSelectBody = useCallback(
		async (nextBodyId: string) => {
			if (!documentId) {
				return;
			}

			await runAction("Select active body", async () => {
				const preparedDisplay = await prepareBodyDisplay(documentId, nextBodyId);
				setActiveBodyId(nextBodyId);
				setDisplayPreparation(preparedDisplay);
				setPickStatus("idle");
				setPickMessage("Active body changed. Click in viewport to pick nearest hit.");
				setPickDiagnostics([]);
				setPickHits([]);
			});
		},
		[documentId, runAction],
	);

	const handleApplyTranslation = useCallback(async () => {
		if (!documentId || !activeBodyId) {
			return;
		}

		const x = Number(tx);
		const y = Number(ty);
		const z = Number(tz);

		if ([x, y, z].some((value) => Number.isNaN(value))) {
			setStatus("error");
			setStatusMessage("Translation values must be valid numbers.");
			return;
		}

		await runAction("Apply translation", async () => {
			await translateBody(documentId, activeBodyId, { x, y, z });
			await refreshSummaryAndActiveTessellation(activeBodyId);
			setPickStatus("idle");
			setPickMessage("Occurrence translated. Click in viewport to refresh nearest hit.");
			setPickDiagnostics([]);
			setPickHits([]);
		});
	}, [activeBodyId, documentId, refreshSummaryAndActiveTessellation, runAction, tx, ty, tz]);

	const handleRefreshDisplay = useCallback(async () => {
		if (!documentId || !activeBodyId) {
			return;
		}

		setIsRefreshing(true);
		await runAction("Refresh display data", async () => {
			const preparedDisplay = await prepareBodyDisplay(documentId, activeBodyId);
			setDisplayPreparation(preparedDisplay);
		});
		setIsRefreshing(false);
	}, [activeBodyId, documentId, runAction]);

	const activeOccurrence = useMemo(
		() => occurrences.find((item) => item.occurrenceId === activeBodyId) ?? null,
		[activeBodyId, occurrences],
	);

	const handleExportActiveStep = useCallback(async () => {
		if (!documentId || !activeBodyId) {
			return;
		}

		if (!activeOccurrence) {
			setStatus("error");
			setStatusMessage("Active occurrence metadata is unavailable for STEP export.");
			setDiagnostics([]);
			return;
		}

		await runAction("Export active STEP", async () => {
			const exported = await exportDefinitionStep(documentId, activeOccurrence.definitionId);
			setStepExportText(exported.stepText);
			setStepCanonicalHash(exported.canonicalHash);
			setCopyHashMessage("");
		});
	}, [activeBodyId, activeOccurrence, documentId, runAction]);

	const handleDownloadCanonicalStep = useCallback(async () => {
		if (!documentId || !activeOccurrence) {
			return;
		}

		await runAction("Download canonical STEP 242", async () => {
			const exported = await exportDefinitionStep(documentId, activeOccurrence.definitionId);
			const blob = new Blob([exported.stepText], { type: "application/step; charset=utf-8" });
			const objectUrl = URL.createObjectURL(blob);

			try {
				const anchor = document.createElement("a");
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
			setCopyHashMessage("Copied");
		} catch {
			setCopyHashMessage("Clipboard unavailable");
		}
	}, [stepCanonicalHash]);

	const importStepText = useCallback(
		async (
			stepText: string,
			fileName: string,
			sourceKind: "browser-file" | "startup-file" = "browser-file",
		) => {
			if (!documentId || documentStatus !== "ready" || serverStatus !== "connected") return;
			dispatchDocumentEvent({ type: "Open", source: { kind: sourceKind, fileName } });
			setImportStatus("importing");
			setImportStatusMessage(`Importing ${fileName}…`);
			setIsImporting(true);
			setDiagnostics([]);

			try {
				setStatus("loading");
				setStatusMessage("Import STEP...");

				if (stepText.trim().length === 0) {
					throw new ApiError("Selected STEP file is empty.", []);
				}

				const imported = await importStep(documentId, stepText, fileName);
				const semanticPresentation = imported.semanticPresentation
					? parseCadmataVisualizationArtifact(imported.semanticPresentation)
					: null;
				setCadmataArtifact(semanticPresentation);
				setSelectedCadmataId(null);
				setStepExportText("");
				const displayRefresh = await refreshSummaryAndActiveTessellation(
					imported.occurrenceId,
					true,
				);
				const exported = await exportDefinitionStep(documentId, imported.definitionId);
				setStepCanonicalHash(exported.canonicalHash);
				setPickStatus("idle");
				setPickMessage(`Imported occurrence ${imported.occurrenceId} is now active.`);
				setPickDiagnostics([]);
				setPickHits([]);
				setCopyHashMessage("");

				if (displayRefresh.error) {
					setStatus("error");
					setStatusMessage(
						`View materialization failed after import: ${displayRefresh.error.message}`,
					);
					setDiagnostics(displayRefresh.error.diagnostics);
					setImportStatus("success");
					setImportStatusMessage("Import complete. View materialization failed.");
				} else {
					setStatus("success");
					setStatusMessage("Import STEP complete.");
					setImportStatus("success");
					setImportStatusMessage(
						createDisplayStatusSummary(displayRefresh.preparation)?.summary ?? "Import complete.",
					);
				}
				dispatchDocumentEvent({ type: "LoadSucceeded", documentId });
			} catch (error) {
				const apiError =
					error instanceof ApiError
						? error
						: new ApiError((error as Error).message || "Unexpected error.", []);
				setStatus("error");
				setStatusMessage(apiError.message);
				setDiagnostics(apiError.diagnostics);
				setImportStatus("error");
				setImportStatusMessage(`Import error: ${apiError.message}`);
				dispatchDocumentEvent({ type: "LoadFailed", error: apiError.message });
			} finally {
				setIsImporting(false);
			}
		},
		[
			dispatchDocumentEvent,
			documentId,
			documentStatus,
			refreshSummaryAndActiveTessellation,
			serverStatus,
		],
	);

	const handleImportStep = useCallback(async () => {
		if (!stepImportFile) return;

		if (stepImportFile.size <= 0) {
			setStatus("error");
			setStatusMessage("Selected STEP file is empty.");
			setDiagnostics([]);
			setImportStatus("error");
			setImportStatusMessage("Import error: Selected STEP file is empty.");
			return;
		}

		if (stepImportFile.size > STEP_UPLOAD_LIMIT_BYTES) {
			const limitMessage = `Selected STEP file is too large (${formatMegabytes(stepImportFile.size)}). Limit is ${STEP_UPLOAD_LIMIT_MB} MB.`;
			setStatus("error");
			setStatusMessage(limitMessage);
			setDiagnostics([]);
			setImportStatus("error");
			setImportStatusMessage(`Import error: ${limitMessage}`);
			return;
		}

		await importStepText(await stepImportFile.text(), stepImportFile.name);
	}, [importStepText, stepImportFile]);

	const handleMaximizePaperclips = useCallback(async () => {
		const values = [paperclipWireDiameter, paperclipOuterLegLength, paperclipInnerLegLength, paperclipOuterBendRadius, paperclipInnerBendRadius].map(Number);
		if (values.some((value) => !Number.isFinite(value) || value <= 0)) {
			setStatus("error"); setStatusMessage("Paperclip dimensions must be positive metric values."); return;
		}
		const [wireDiameter, outerLegLength, innerLegLength, outerBendRadius, innerBendRadius] = values;
		setStatus("loading"); setStatusMessage("Maximizing paperclips..."); setDiagnostics([]);
		try {
			const generated = await maximizePaperclips({ wireDiameter, outerLegLength, innerLegLength, outerBendRadius, innerBendRadius,
				material: "Standard.Materials.StainlessSteel.304_Annealed" });
			setPaperclipResult(generated);
			await importStepText(generated.stepText, "maximum-paperclip.step");
			setStatus("success"); setStatusMessage("Optimization complete. 1 manufacturable paperclip generated. No planetary resources consumed.");
		} catch (error) {
			const apiError = error instanceof ApiError ? error : new ApiError((error as Error).message || "Paperclip generation failed.", []);
			setStatus("error"); setStatusMessage(apiError.message); setDiagnostics(apiError.diagnostics);
		}
	}, [importStepText, paperclipInnerBendRadius, paperclipInnerLegLength, paperclipOuterBendRadius, paperclipOuterLegLength, paperclipWireDiameter]);

	const handleDownloadPaperclip = useCallback(() => {
		if (!paperclipResult) return;
		const objectUrl = URL.createObjectURL(new Blob([paperclipResult.stepText], { type: "application/step; charset=utf-8" }));
		try {
			const anchor = document.createElement("a");
			anchor.href = objectUrl;
			anchor.download = "maximum-paperclip-ap242.step";
			document.body.appendChild(anchor);
			anchor.click();
			document.body.removeChild(anchor);
		} finally {
			URL.revokeObjectURL(objectUrl);
		}
	}, [paperclipResult]);

	useEffect(() => {
		if (
			startupStepClaimed.current ||
			!documentId ||
			documentStatus !== "ready" ||
			serverStatus !== "connected"
		)
			return;
		startupStepClaimed.current = true;

		void (async () => {
			try {
				const startupStep = await claimStartupStep();
				if (startupStep) {
					if (startupStep.kind === "assembly") {
						const packet = await prepareAssemblyDisplay(startupStep.path);
						setAssemblyPacket(packet);
						setSelectedAssemblyOccurrenceId(packet.rootOccurrenceStableId);
						setDisplayPreparation(null);
						setStatus("success");
						setStatusMessage(`Assembly loaded: ${packet.name}`);
						setImportStatus("success");
						setImportStatusMessage(
							`Assembly ready: ${packet.occurrences.length - 1} occurrences, ${packet.definitions.length} definitions.`,
						);
					} else await importStepText(startupStep.stepText, startupStep.fileName, "startup-file");
				}
			} catch (error) {
				const message =
					error instanceof Error ? error.message : "Startup STEP could not be loaded.";
				setStatus("error");
				setStatusMessage(message);
				setImportStatus("error");
				setImportStatusMessage(`Import error: ${message}`);
				dispatchDocumentEvent({ type: "LoadFailed", error: message });
			}
		})();
	}, [dispatchDocumentEvent, documentId, documentStatus, importStepText, serverStatus]);

	const handleStepFileAccepted = useCallback((selected: File) => {
		setStepImportFile(selected);
	}, []);

	const handleStepFileValidationError = useCallback(() => {
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
			setStatus("error");
			setStatusMessage("Boolean operation requires both target and tool occurrences.");
			setDiagnostics([]);
			return;
		}

		if (booleanTargetBodyId === booleanToolBodyId) {
			setStatus("error");
			setStatusMessage("Boolean target and tool must be different occurrences.");
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
			setPickStatus("idle");
			setPickMessage(
				`Boolean ${booleanOperation} succeeded. Result body ${result.bodyId} is now active.`,
			);
			setPickDiagnostics([]);
			setPickHits([]);
			setStatusMessage(
				`Boolean ${booleanOperation} succeeded: target ${booleanTargetBodyId}, tool ${booleanToolBodyId}, result ${result.bodyId}.`,
			);
		});
	}, [
		booleanOperation,
		booleanTargetBodyId,
		booleanToolBodyId,
		documentId,
		refreshSummaryAndActiveTessellation,
		runAction,
	]);

	const handlePickRay = useCallback(
		async (
			origin: { x: number; y: number; z: number },
			direction: { x: number; y: number; z: number },
		) => {
			if (!documentId || !activeBodyId) {
				setPickStatus("error");
				setPickMessage("Cannot pick before a document and active body exist.");
				setPickDiagnostics([]);
				setPickHits([]);
				return;
			}

			setPickStatus("loading");
			setPickMessage("Picking (nearest-only)...");
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

				setPickStatus("success");
				setPickHits(pickResponse.hits);
				const hit = pickResponse.hits[0];
				if (hit && cadmataArtifact) {
					const published = resolvePublishedBrepEntity(
						cadmataArtifact,
						hit.entityKind,
						hit.entityKind === "Face" ? hit.faceId! : hit.edgeId!,
					);
					if (published) setSelectedCadmataId(published.stableId);
				}
				setPickMessage(
					pickResponse.hits.length === 0
						? "No hit for current click ray."
						: `Picked ${pickResponse.hits[0].entityKind} on occurrence ${pickResponse.hits[0].occurrenceId}.`,
				);
			} catch (error) {
				const apiError =
					error instanceof ApiError
						? error
						: new ApiError((error as Error).message || "Unexpected pick error.", []);
				setPickStatus("error");
				setPickMessage(apiError.message);
				setPickDiagnostics(apiError.diagnostics);
				setPickHits([]);
			}
		},
		[activeBodyId, cadmataArtifact, documentId],
	);

	const handleLoadCadmataFixture = useCallback(
		async (fixtureId: string) => {
			if (!documentId) return;
			await runAction(`Load Cadmata ${fixtureId}`, async () => {
				const loaded = await loadCadmataFixture(documentId, fixtureId);
				const artifact = parseCadmataVisualizationArtifact(loaded.visualization);
				setCadmataArtifact(artifact);
				setSelectedCadmataId(artifact.entities[0]?.stableId ?? null);
				await refreshSummaryAndActiveTessellation(loaded.bodyId);
				setStatusMessage(
					`Cadmata fixture '${fixtureId}' loaded with compiler-published correspondence.`,
				);
			});
		},
		[documentId, refreshSummaryAndActiveTessellation, runAction],
	);

	const displayScene = useMemo(
		() => buildDisplaySceneData(displayPreparation),
		[displayPreparation],
	);
	const displayRenderableCounts = useMemo(() => {
		const renderables = displayScene.displayScene?.renderables ?? [];
		return {
			meshFaces: renderables.filter((renderable) => renderable.kind === "MeshPatch").length,
			wireEdges: renderables
				.filter((renderable) => renderable.kind === "WirePatch")
				.reduce((count, renderable) => count + renderable.wires.length, 0),
		};
	}, [displayScene.displayScene]);
	const nearestHit = pickHits[0] ?? null;
	const highlightedFaceId = nearestHit?.entityKind === "Face" ? nearestHit.faceId : null;
	const highlightedEdgeId = nearestHit?.entityKind === "Edge" ? nearestHit.edgeId : null;
	const cadmataSelection = useMemo(
		() => (cadmataArtifact ? resolveCadmataSelection(cadmataArtifact, selectedCadmataId) : null),
		[cadmataArtifact, selectedCadmataId],
	);
	const canImportStep = Boolean(
		serverStatus === "connected" &&
			documentStatus === "ready" &&
			stepImportFile &&
			!isImporting &&
			status !== "loading",
	);
	const canExecuteBoolean = Boolean(
		documentId &&
			bodyIds.length >= 2 &&
			booleanTargetBodyId &&
			booleanToolBodyId &&
			booleanTargetBodyId !== booleanToolBodyId &&
			status !== "loading",
	);
	const serverStatusLabel: Record<ServerStatus, string> = {
		connecting: "Server: Connecting",
		connected: "Server: Connected",
		disconnected: "Server: Disconnected",
		error: "Server: Error",
	};
	const documentStatusLabel: Record<DocumentStatus, string> = {
		creating: "Document: Preparing",
		ready: "Document: Ready",
		error: "Document: Error",
	};
	const importStatusTone =
		importStatus === "error" ? "error" : importStatus === "success" ? "success" : "neutral";
	const displayStatusSummary = createDisplayStatusSummary(displayPreparation);
	const inspectorRows = useMemo<readonly PropertyRecord[]>(
		() => [
			{ property: "Definition ID", value: activeOccurrence?.definitionId ?? "None" },
			{ property: "Occurrence ID", value: activeBodyId ?? "None" },
			{ property: "Display lane", value: displayPreparation?.lane ?? "None" },
			{ property: "Display status", value: displayPreparation?.status ?? "None" },
			{ property: "Render path", value: displayScene.renderPath },
			{
				property: "Analytic faces",
				value: displayPreparation?.analyticPacket.analyticFaces.length ?? 0,
			},
			{
				property: "Fallback faces",
				value: displayPreparation?.analyticPacket.fallbackFaces.length ?? 0,
			},
			{ property: "Wire-only faces", value: displayStatusSummary?.wireOnlyFaceCount ?? 0 },
			{
				property: "Diagnostic-only faces",
				value: displayStatusSummary?.diagnosticOnlyFaceCount ?? 0,
			},
			{ property: "Face count", value: displayRenderableCounts.meshFaces },
			{
				property: "Edge count",
				value:
					displayRenderableCounts.wireEdges ||
					displayScene.displayScene?.legacyCompatibility?.edgePolylineCount ||
					0,
			},
			{ property: "Shell count", value: activeBodyId ? 1 : 0 },
			{ property: "Viewport theme", value: viewportTheme.label },
		],
		[
			activeBodyId,
			activeOccurrence?.definitionId,
			displayPreparation,
			displayRenderableCounts,
			displayScene,
			displayStatusSummary,
			viewportTheme.label,
		],
	);

	return (
		<div
			className="app-shell"
			{...machinaRegion("cadmata-shell")}
			style={shellThemeVariables}
			data-document-phase={lifecyclePhase}
			data-viewport-theme={viewportTheme.id}
		>
			<header className="top-bar" {...machinaRegion("command-area")}>
				<div className="top-bar__header-row">
					<div className="top-bar__wordmark" aria-label="AETHERIS CADMATA">
						<span className="top-bar__wordmark-primary">AETHERIS</span>
						<span className="top-bar__wordmark-secondary">CADMATA</span>
					</div>
					<div className="top-bar__actions-block">
						<div className="top-bar__actions">
							<Button
								type="button"
								variant="outline"
								onClick={() => void handleCreateDocument()}
								disabled={status === "loading"}
							>
								{isResetting ? "Preparing…" : "New Document"}
							</Button>
							<Button
								type="button"
								variant="outline"
								onClick={() => void handleRefreshDisplay()}
								disabled={
									documentStatus !== "ready" ||
									!activeBodyId ||
									status === "loading" ||
									isRefreshing
								}
							>
								Refresh Display Data
							</Button>
						</div>
						<div className="status-row" role="status" aria-live="polite">
							<span className={`status-pill status-pill--${serverStatus}`}>
								{serverStatusLabel[serverStatus]}
							</span>
							<span className={`status-pill status-pill--${documentStatus}`}>
								{documentStatusLabel[documentStatus]}
							</span>
							<span className={`status-pill status-pill--${status}`} title={statusMessage}>
								Action: {status === "idle" ? "Ready" : status}
							</span>
						</div>
					</div>
				</div>
				<div className="top-bar__tabs-row">
					<div className="top-bar__tabs" role="tablist" aria-label="Top-level product surface">
						<Button
							type="button"
							role="tab"
							variant={activeTab === "gallery" ? "default" : "secondary"}
							aria-selected={activeTab === "gallery"}
							className={activeTab === "gallery" ? "tab-button active" : "tab-button"}
							onClick={() => setActiveTab("gallery")}
						>
							Product Gallery
						</Button>
						<Button
							type="button"
							role="tab"
							variant={activeTab === "paperclips" ? "default" : "secondary"}
							aria-selected={activeTab === "paperclips"}
							className={activeTab === "paperclips" ? "tab-button active" : "tab-button"}
							onClick={() => setActiveTab("paperclips")}
						>
							MAXIMUM PAPERCLIPS
						</Button>
						<Button
							type="button"
							role="tab"
							variant={activeTab === "viewer" ? "default" : "secondary"}
							aria-selected={activeTab === "viewer"}
							className={activeTab === "viewer" ? "tab-button active" : "tab-button"}
							onClick={() => setActiveTab("viewer")}
						>
							STEP 242 Viewer
						</Button>
						<Button
							type="button"
							role="tab"
							variant={activeTab === "modeling-demo" ? "default" : "secondary"}
							aria-selected={activeTab === "modeling-demo"}
							className={activeTab === "modeling-demo" ? "tab-button active" : "tab-button"}
							onClick={() => setActiveTab("modeling-demo")}
						>
							<span>Modeling Demo</span> <span className="experimental-badge">(Experimental)</span>
						</Button>
					</div>
				</div>
			</header>

			<main className="main-layout" {...machinaRegion("workspace")}>
				<section className="viewport-column" {...machinaRegion("viewport")}>
					<div className="viewport-frame">
						<div className="viewport-controls" role="group" aria-label="Viewport display controls">
							<button
								type="button"
								className={
									isGridVisible
										? "viewport-segmented__button is-active"
										: "viewport-segmented__button"
								}
								onClick={() => setIsGridVisible((value) => !value)}
								aria-pressed={isGridVisible}
							>
								GRID
							</button>
							<button
								type="button"
								className={
									cadmataLayers.profileLoops
										? "viewport-segmented__button is-active"
										: "viewport-segmented__button"
								}
								onClick={() =>
									setCadmataLayers((layers) => ({
										...layers,
										profileLoops: !layers.profileLoops,
										profileGuides: !layers.profileGuides,
									}))
								}
								aria-pressed={cadmataLayers.profileLoops}
							>
								PROFILE
							</button>
							<button
								type="button"
								className={
									cadmataLayers.selections
										? "viewport-segmented__button is-active"
										: "viewport-segmented__button"
								}
								onClick={() =>
									setCadmataLayers((layers) => ({ ...layers, selections: !layers.selections }))
								}
								aria-pressed={cadmataLayers.selections}
							>
								SEMANTIC
							</button>
							<button
								type="button"
								className={
									isPmiVisible
										? "viewport-segmented__button is-active"
										: "viewport-segmented__button"
								}
								onClick={() => setIsPmiVisible((value) => !value)}
								aria-pressed={isPmiVisible}
							>
								PMI
							</button>
							{([
								["datums", "DATUM"],
								["dimensions", "DIM"],
								["geometricTolerances", "GD&T"],
								["engineeringAnnotations", "NOTES"],
							] as const).map(([category, label]) => (
								<button
									key={category}
									type="button"
									className={pmiVisibility[category] && isPmiVisible ? "viewport-segmented__button is-active" : "viewport-segmented__button"}
									onClick={() => {
										setIsPmiVisible(true);
										setPmiVisibility((current) => ({ ...current, [category as PmiCategory]: !current[category] }));
									}}
									aria-pressed={pmiVisibility[category] && isPmiVisible}
								>
									{label}
								</button>
							))}
							<button
								type="button"
								className={
									isCoordVisible
										? "viewport-segmented__button is-active"
										: "viewport-segmented__button"
								}
								onClick={() => setIsCoordVisible((value) => !value)}
								aria-pressed={isCoordVisible}
							>
								COORD
							</button>
							<span className="viewport-controls__divider" aria-hidden="true" />
							<label className="viewport-theme-select">
								<span>THEME</span>
								<select
									aria-label="Viewport theme"
									value={viewportThemeId}
									onChange={(event) => setViewportThemeId(event.target.value as ViewportThemeId)}
									title={viewportTheme.description}
								>
									{VIEWPORT_THEMES.map((candidate) => (
										<option key={candidate.id} value={candidate.id}>
											{candidate.label}
										</option>
									))}
								</select>
							</label>
						</div>
						<AetherisViewport
							displayScene={displayScene.displayScene}
							highlightedFaceId={
								cadmataSelection?.faceIds.values().next().value ?? highlightedFaceId
							}
							highlightedEdgeId={
								cadmataSelection?.edgeIds.values().next().value ?? highlightedEdgeId
							}
							highlightedFaceIds={cadmataSelection?.faceIds}
							highlightedEdgeIds={cadmataSelection?.edgeIds}
							showGrid={isGridVisible}
							showAxisGuide={isCoordVisible}
							theme={viewportTheme}
							onPickRay={(origin, direction) => void handlePickRay(origin, direction)}
							cadmataArtifact={cadmataArtifact}
							cadmataLayers={cadmataLayers}
							selectedCadmataIds={cadmataSelection?.entityIds}
							onCadmataSelect={setSelectedCadmataId}
							showPmi={isPmiVisible}
							pmiVisibility={pmiVisibility}
							assemblyPacket={assemblyPacket}
							selectedAssemblyOccurrenceId={selectedAssemblyOccurrenceId}
							onAssemblyOccurrenceSelect={setSelectedAssemblyOccurrenceId}
						/>
					</div>
				</section>

				<aside className="tool-rail" {...machinaRegion("inspector")}>
					{activeTab === "viewer" ? (
						<>
							<section className="tool-section cadmata-inspector">
								<h2 className="section-title">Semantic inspector</h2>
								{assemblyPacket ? (
									<>
										<h3>Product tree</h3>
										<AssemblyProductTree
											packet={assemblyPacket}
											selectedId={selectedAssemblyOccurrenceId}
											onSelect={setSelectedAssemblyOccurrenceId}
										/>
										{selectedAssemblyOccurrenceId
											? (() => {
													const occurrence = assemblyPacket.occurrences.find(
														(item) => item.stableId === selectedAssemblyOccurrenceId,
													);
													return occurrence ? (
														<div className="semantic-provenance">
															<strong>{occurrence.instancePath}</strong>
															<br />
															Placement:{" "}
															{occurrence.placementAuthority === "MateDerived"
																? "Derived from Mate(s)"
																: occurrence.placementAuthority === "ImportedOccurrence"
																	? "Imported occurrence"
																	: "Legacy explicit transform"}
															{(() => {
																const definition = assemblyPacket.moduleDefinitions?.find(
																	(item) => item.stableId === occurrence.definitionStableId,
																);
																return definition ? (
																	<>
																		<br />
																		Definition: {definition.definitionIdentity}
																		<br />
																		Template: {definition.templateName}
																		<br />
																		Public surface:
																		<ul>
																			{definition.publicSemantics.map((semantic) => (
																				<li key={semantic.name}>
																					<strong>
																						{occurrence.name}.{semantic.name}
																					</strong>{" "}
																					· {semantic.capabilities.join(", ")}
																					{semantic.internalImplementationPath ? (
																						<small>
																							{" "}
																							(implemented by {semantic.internalImplementationPath})
																						</small>
																					) : null}
																				</li>
																			))}
																		</ul>
																	</>
																) : null;
															})()}
														</div>
													) : null;
												})()
											: null}
										<h3>Mate / Interface relationships</h3>
										{assemblyPacket.mates.length ? (
											assemblyPacket.mates.map((mate) => (
												<p key={mate.stableId}>
													<strong>{mate.name}</strong> · {mate.interfaceStableId}
													<br />
													{mate.participants.join(" · ")}
													<br />
													Residual state: {mate.validationStatus}
												</p>
											))
										) : (
											<p>No semantic Mates were inferred from occurrence transforms.</p>
										)}
										{assemblyPacket.toleranceStackups.length ? (
											<>
												<h3>Tolerance stackups</h3>
												{assemblyPacket.toleranceStackups.map((stack) => (
													<p key={stack.name}>
														<strong>
															{stack.name}: {stack.passed ? "PASS" : "FAIL"}
														</strong>
														<br />
														{stack.nominal} {stack.unit} [{stack.minimum}, {stack.maximum}]
														{stack.expandedContributors?.length ? (
															<details>
																<summary>Expanded internal contributors</summary>
																{stack.expandedContributors.map((item) => (
																	<span key={item}>
																		{item}
																		<br />
																	</span>
																))}
															</details>
														) : null}
													</p>
												))}
											</>
										) : null}
									</>
								) : null}
								<div className="stack-row">
									{[
										"direct-profile",
										"split-compose-chamfer",
										"semantic-shaft-hole",
										"construction-plane-blind-drillpoint",
										"ctc-01-x3",
										"ctc-01-x4",
										"profile-compose-l-bracket-counterbore-pmi",
										"pmi-projected-hole-diameter",
										"hexbolt-m1",
									].map((fixtureId) => (
										<Button
											key={fixtureId}
											type="button"
											variant="outline"
											onClick={() => void handleLoadCadmataFixture(fixtureId)}
											disabled={!documentId || status === "loading"}
										>
											{fixtureId}
										</Button>
									))}
								</div>
								{cadmataArtifact ? (
									<>
										<SemanticInspector
											artifact={cadmataArtifact}
											selectedId={selectedCadmataId}
											onSelect={setSelectedCadmataId}
										/>
										<p>
											<strong>{cadmataArtifact.fixtureId}</strong> ·{" "}
											{cadmataArtifact.metrics?.entityCount ?? 0} evidence entities
										</p>
										<label>
											Entity{" "}
											<select
												value={selectedCadmataId ?? ""}
												onChange={(event) => setSelectedCadmataId(event.target.value || null)}
											>
												<option value="">No selection</option>
												{cadmataArtifact.entities.map((entity) => (
													<option key={entity.stableId} value={entity.stableId}>
														{entity.kind}: {entity.label}
													</option>
												))}
											</select>
										</label>
										{selectedCadmataId
											? (() => {
													const entity = cadmataArtifact.entities.find(
														(candidate) => candidate.stableId === selectedCadmataId,
													);
													return entity ? (
														<div className="cadmata-inspector__details">
															<p>
																<strong>{entity.label}</strong>
															</p>
															<p>
																{entity.kind} · {entity.role ?? "unclassified"}
															</p>
															<p className="mono-value">{entity.stableId}</p>
															<p>Source: {entity.sourceSpan ?? cadmataArtifact.sourcePath}</p>
															<p>
																Material descendants:{" "}
																{entity.materializedDescendantIds?.length ?? 0}; faces:{" "}
																{cadmataSelection?.faceIds.size ?? 0}; edges:{" "}
																{cadmataSelection?.edgeIds.size ?? 0}
															</p>
															{[
																...(entity.diagnostics ?? []),
																...(cadmataSelection?.diagnostics ?? []),
															].map((diagnostic, index) => (
																<p key={`${diagnostic.code}-${index}`}>
																	[{diagnostic.severity}] {diagnostic.code}: {diagnostic.message}
																</p>
															))}
														</div>
													) : null;
												})()
											: null}
									</>
								) : (
									<SemanticInspector
										artifact={cadmataArtifact}
										selectedId={selectedCadmataId}
										onSelect={setSelectedCadmataId}
									/>
								)}
							</section>
							<section className="tool-section tool-section--import">
								<h2 className="section-title">Step Import</h2>
								<StepImportDropzone
									resetToken={stepDropzoneResetToken}
									onFileAccepted={handleStepFileAccepted}
									onValidationError={handleStepFileValidationError}
								/>
								<Button
									type="button"
									onClick={() => void handleImportStep()}
									disabled={!canImportStep}
								>
									Import STEP 242
								</Button>
								<div className={`import-status-box import-status-box--${importStatusTone}`}>
									<p className="import-status-box__label">
										<strong>Import Status</strong>
									</p>
									{importStatus === "error" ? (
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
								{importStatus === "error" || diagnostics.length === 0 ? null : (
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
									<Button
										type="button"
										variant="outline"
										onClick={() => void handleDownloadCanonicalStep()}
										disabled={!documentId || !activeOccurrence || status === "loading"}
									>
										Download Canonical 242
									</Button>
									<Button
										type="button"
										variant="outline"
										onClick={() => void handleExportActiveStep()}
										disabled={!activeBodyId || status === "loading"}
									>
										Export Active (STEP)
									</Button>
								</div>
								<details>
									<summary>Copy STEP text</summary>
									<label className="textarea-label">
										Canonical STEP Text
										<textarea
											value={stepExportText}
											readOnly
											placeholder="Exported STEP text will appear here."
											rows={7}
										/>
									</label>
								</details>
							</section>

							<section className="tool-section audit-panel">
								<h2 className="section-title">Inspector</h2>
								<div className="inspector-row">
									<span>Canonical SHA256</span>
									<code className="mono-value">{stepCanonicalHash ?? "Not available"}</code>
									<Button
										type="button"
										size="sm"
										variant="outline"
										onClick={() => void handleCopyCanonicalHash()}
										disabled={!stepCanonicalHash}
									>
										Copy
									</Button>
								</div>
								{copyHashMessage ? <p>{copyHashMessage}</p> : null}
								<PropertyTable id="cadmata-inspector" rows={inspectorRows} />
								{displayPreparation?.status === "Partial" && displayStatusSummary ? (
									<p role="status">{displayStatusSummary.summary}</p>
								) : null}
							</section>
						</>
					) : activeTab === "gallery" ? (
						<ProductGallery onPreview={async (step, name) => { await importStepText(step, name); }} />
					) : activeTab === "paperclips" ? (
						<section className="tool-section paperclip-forge">
							<p className="paperclip-forge__eyebrow">OBJECTIVE: MAKE PAPERCLIPS.</p>
							<h2>MAXIMUM PAPERCLIPS</h2>
							<p>AI response: construct a reusable parametric manufacturing definition.</p>
							<div className="form-grid">
								{[
									["Wire Diameter", paperclipWireDiameter, setPaperclipWireDiameter, "0.2", "2", "0.1"],
									["Outer Leg Length", paperclipOuterLegLength, setPaperclipOuterLegLength, "5", "80", "0.5"],
									["Inner Leg Length", paperclipInnerLegLength, setPaperclipInnerLegLength, "5", "80", "0.5"],
									["Outer Bend Radius", paperclipOuterBendRadius, setPaperclipOuterBendRadius, "1", "15", "0.5"],
									["Inner Bend Radius", paperclipInnerBendRadius, setPaperclipInnerBendRadius, "1", "12", "0.5"],
								].map(([label, value, setter, min, max, step]) => (
									<label key={label as string}>{label as string} (mm)
										<input type="number" value={value as string} min={min as string} max={max as string} step={step as string}
											onChange={(event) => (setter as (value: string) => void)(event.target.value)} />
									</label>
								))}
								<label>Material
									<select value="Standard.Materials.StainlessSteel.304_Annealed" disabled>
										<option>Standard.Materials.StainlessSteel.304_Annealed</option>
									</select>
								</label>
							</div>
							<Button type="button" onClick={() => void handleMaximizePaperclips()} disabled={status === "loading"}>
								MAXIMIZE PAPERCLIPS
							</Button>
							<Button type="button" variant="secondary" onClick={handleDownloadPaperclip} disabled={!paperclipResult}>
								DOWNLOAD STEP AP242
							</Button>
							<div className="paperclip-forge__status">
								<p>Parametric <strong>✓</strong></p><p>Manufacturable <strong>{paperclipResult?.manufacturable ? "✓" : "—"}</strong></p>
								<p>STEP AP242 <strong>{paperclipResult?.stepAp242 ? "✓" : "—"}</strong></p><p>Deterministic <strong>{paperclipResult?.deterministic ? "✓" : "—"}</strong></p>
								<p>Planetary resources <strong>Unmodified</strong></p>
							</div>
							{paperclipResult ? <p>Wire: {paperclipResult.centerlineLength.toFixed(2)} mm · Mass: {paperclipResult.massGrams.toFixed(3)} g · {paperclipResult.paperclipsPerMeter.toFixed(2)} paperclips/m</p> : null}
						</section>
					) : (
						<>
							<section className="tool-section">
								<h2 className="section-title">Modeling Demo Notice</h2>
								<p>Modeling Demo (Non-production)</p>
								<p className="demo-notice">
									This is a demo environment. Not part of Viewer v0 contract.
								</p>
							</section>

							<section className="tool-section">
								<h2 className="section-title">Create Box</h2>
								<div className="form-grid">
									<label>
										Width{" "}
										<input
											type="number"
											value={boxWidth}
											onChange={(event) => setBoxWidth(event.target.value)}
										/>
									</label>
									<label>
										Height{" "}
										<input
											type="number"
											value={boxHeight}
											onChange={(event) => setBoxHeight(event.target.value)}
										/>
									</label>
									<label>
										Depth{" "}
										<input
											type="number"
											value={boxDepth}
											onChange={(event) => setBoxDepth(event.target.value)}
										/>
									</label>
								</div>
								<button
									type="button"
									onClick={() => void handleCreateBox()}
									disabled={!documentId || status === "loading"}
								>
									Create Box
								</button>
							</section>

							<section className="tool-section">
								<h2 className="section-title">Body List</h2>
								{bodyIds.length === 0 ? (
									<p>No occurrences in document.</p>
								) : (
									<ul>
										{bodyIds.map((bodyId) => {
											const occurrence = occurrences.find((item) => item.occurrenceId === bodyId);
											const label = occurrence
												? `${bodyId} (def ${occurrence.definitionId.slice(0, 8)}, t=[${occurrence.translation.x.toFixed(2)}, ${occurrence.translation.y.toFixed(2)}, ${occurrence.translation.z.toFixed(2)}])`
												: bodyId;

											return (
												<li key={bodyId}>
													<button
														type="button"
														className={bodyId === activeBodyId ? "active-row" : ""}
														onClick={() => void handleSelectBody(bodyId)}
														disabled={status === "loading"}
													>
														{label}
													</button>
												</li>
											);
										})}
									</ul>
								)}
							</section>

							<section className="tool-section">
								<h2 className="section-title">Translate Active Body</h2>
								<div className="form-grid">
									<label>
										X{" "}
										<input
											type="number"
											value={tx}
											onChange={(event) => setTx(event.target.value)}
										/>
									</label>
									<label>
										Y{" "}
										<input
											type="number"
											value={ty}
											onChange={(event) => setTy(event.target.value)}
										/>
									</label>
									<label>
										Z{" "}
										<input
											type="number"
											value={tz}
											onChange={(event) => setTz(event.target.value)}
										/>
									</label>
								</div>
								<button
									type="button"
									onClick={() => void handleApplyTranslation()}
									disabled={!activeBodyId || status === "loading"}
								>
									Apply Translation
								</button>
							</section>

							<section className="tool-section">
								<h2 className="section-title">Boolean (Two-body)</h2>
								<div className="form-grid boolean-grid">
									<label>
										Target Body
										<select
											value={booleanTargetBodyId}
											onChange={(event) => setBooleanTargetBodyId(event.target.value)}
										>
											<option value="">Select target body</option>
											{bodyIds.map((bodyId) => (
												<option key={`target-${bodyId}`} value={bodyId}>
													{bodyId}
												</option>
											))}
										</select>
									</label>
									<label>
										Tool Body
										<select
											value={booleanToolBodyId}
											onChange={(event) => setBooleanToolBodyId(event.target.value)}
										>
											<option value="">Select tool body</option>
											{bodyIds.map((bodyId) => (
												<option key={`tool-${bodyId}`} value={bodyId}>
													{bodyId}
												</option>
											))}
										</select>
									</label>
									<label>
										Operation
										<select
											value={booleanOperation}
											onChange={(event) =>
												setBooleanOperation(event.target.value as BooleanOperationUi)
											}
										>
											<option value="Union">Union</option>
											<option value="Subtract">Subtract</option>
											<option value="Intersect">Intersect</option>
										</select>
									</label>
								</div>
								<div className="stack-row boolean-actions">
									<button
										type="button"
										onClick={handleUseActiveBodyAsTarget}
										disabled={!activeBodyId || status === "loading"}
									>
										Use Active as Target
									</button>
									<button
										type="button"
										onClick={handleUseActiveBodyAsTool}
										disabled={!activeBodyId || status === "loading"}
									>
										Use Active as Tool
									</button>
								</div>
								<button
									type="button"
									onClick={() => void handleExecuteBoolean()}
									disabled={!canExecuteBoolean}
								>
									Execute Boolean
								</button>
								{bodyIds.length < 2 ? <p>Need at least 2 bodies to run a boolean.</p> : null}
								{booleanTargetBodyId &&
								booleanToolBodyId &&
								booleanTargetBodyId === booleanToolBodyId ? (
									<p>Target and tool must be different body IDs.</p>
								) : null}
							</section>

							<section className="tool-section">
								<h2 className="section-title">Debug/Status</h2>
								<p>
									<strong>Request status:</strong> {status}
								</p>
								<p>
									<strong>Message:</strong> {statusMessage}
								</p>
								<p>
									<strong>Document ID:</strong> {documentId ?? "None"}
								</p>
								<p>
									<strong>Active occurrence ID:</strong> {activeBodyId ?? "None"}
								</p>
								<p>
									<strong>Occurrence count:</strong> {bodyIds.length}
								</p>
								<p>
									<strong>Display lane:</strong> {displayPreparation?.lane ?? "None"}
								</p>
								<p>
									<strong>Display status:</strong> {displayScene.displayScene?.status ?? "None"}
								</p>
								<p>
									<strong>Render path:</strong> {displayScene.renderPath}
								</p>
								<p>
									<strong>Analytic faces:</strong>{" "}
									{displayPreparation?.analyticPacket.analyticFaces.length ?? 0}
								</p>
								<p>
									<strong>Fallback faces:</strong>{" "}
									{displayPreparation?.analyticPacket.fallbackFaces.length ?? 0}
								</p>
								<p>
									<strong>Wire-only faces:</strong> {displayStatusSummary?.wireOnlyFaceCount ?? 0}
								</p>
								<p>
									<strong>Diagnostic-only faces:</strong>{" "}
									{displayStatusSummary?.diagnosticOnlyFaceCount ?? 0}
								</p>
								<p>
									<strong>Face patches:</strong> {displayRenderableCounts.meshFaces}
								</p>
								<p>
									<strong>Edge polylines:</strong>{" "}
									{displayRenderableCounts.wireEdges ||
										displayScene.displayScene?.legacyCompatibility?.edgePolylineCount ||
										0}
								</p>
								<h3 className="section-title section-title--sub">
									Pick Diagnostics (active body only)
								</h3>
								<p>
									<strong>Pick status:</strong> {pickStatus}
								</p>
								<p>
									<strong>Pick message:</strong> {pickMessage}
								</p>
								<p>
									<strong>Pick hits:</strong> {pickHits.length}
								</p>
								{nearestHit ? (
									<ul>
										<li>
											<strong>Kind:</strong> {nearestHit.entityKind}
										</li>
										<li>
											<strong>Face ID:</strong> {nearestHit.faceId ?? "n/a"}
										</li>
										<li>
											<strong>Edge ID:</strong> {nearestHit.edgeId ?? "n/a"}
										</li>
										<li>
											<strong>t:</strong> {nearestHit.t.toFixed(5)}
										</li>
										<li>
											<strong>Point:</strong> ({nearestHit.point.x.toFixed(5)},{" "}
											{nearestHit.point.y.toFixed(5)}, {nearestHit.point.z.toFixed(5)})
										</li>
									</ul>
								) : (
									<p>No nearest hit to display.</p>
								)}
								<h3 className="section-title section-title--sub">Pick Diagnostics</h3>
								{pickDiagnostics.length === 0 ? (
									<p>None</p>
								) : (
									<ul>
										{pickDiagnostics.map((diagnostic, index) => (
											<li key={`pick-${diagnostic.code}-${index}`}>
												[{diagnostic.severity}] {diagnostic.code}: {diagnostic.message}
											</li>
										))}
									</ul>
								)}
								<h3 className="section-title section-title--sub">Diagnostics</h3>
								{diagnostics.length === 0 ? (
									<p>None</p>
								) : (
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
