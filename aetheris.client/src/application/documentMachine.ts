import { createDeusSnapshot, stepDeusMachine } from "machinalayout/deus";
import { M } from "machinalayout/machina";

export type DocumentSource =
	| { kind: "browser-file"; fileName: string }
	| { kind: "startup-file"; fileName: string }
	| { kind: "generated"; name: string };

export interface DocumentBoard {
	source: DocumentSource | null;
	documentId: string | null;
	error: string | null;
	revision: number;
}

export type DocumentEvent =
	| { type: "Open"; source: DocumentSource }
	| { type: "LoadSucceeded"; documentId: string }
	| { type: "LoadFailed"; error: string }
	| { type: "Close" }
	| { type: "Reload" };

const EMPTY = ["document", "empty"] as const;
const LOADING = ["document", "loading"] as const;
const READY = ["document", "ready"] as const;
const FAILED = ["document", "failed"] as const;

export const documentMachine = M.machine<DocumentBoard, DocumentEvent>({
	initial: EMPTY,
	states: [
		M.state<DocumentBoard, DocumentEvent>(EMPTY),
		M.state<DocumentBoard, DocumentEvent>(LOADING),
		M.state<DocumentBoard, DocumentEvent>(READY),
		M.state<DocumentBoard, DocumentEvent>(FAILED),
	],
	transitions: [
		M.on<DocumentBoard, DocumentEvent>(
			"Open",
			["document"],
			LOADING,
			(board, event) => {
				if (event.type !== "Open") return;
				board.source = event.source;
				board.error = null;
				board.revision += 1;
			},
			{ key: "document.open" },
		),
		M.on<DocumentBoard, DocumentEvent>(
			"LoadSucceeded",
			LOADING,
			READY,
			(board, event) => {
				if (event.type !== "LoadSucceeded") return;
				board.documentId = event.documentId;
				board.error = null;
			},
			{ key: "document.loaded" },
		),
		M.on<DocumentBoard, DocumentEvent>(
			"LoadFailed",
			LOADING,
			FAILED,
			(board, event) => {
				if (event.type !== "LoadFailed") return;
				board.error = event.error;
			},
			{ key: "document.failed" },
		),
		M.on<DocumentBoard, DocumentEvent>(
			"Close",
			["document"],
			EMPTY,
			(board) => {
				board.source = null;
				board.documentId = null;
				board.error = null;
			},
			{ key: "document.close" },
		),
		M.on<DocumentBoard, DocumentEvent>("Reload", READY, LOADING, undefined, {
			key: "document.reload",
			when: (board) => board.source !== null,
			reason: "A ready document can reload only when its source intent is retained.",
		}),
		M.on<DocumentBoard, DocumentEvent>(
			"Reload",
			FAILED,
			LOADING,
			(board) => {
				board.error = null;
			},
			{
				key: "document.retry",
				when: (board) => board.source !== null,
			},
		),
	],
});

export function createDocumentSnapshot() {
	return createDeusSnapshot(documentMachine, {
		source: null,
		documentId: null,
		error: null,
		revision: 0,
	});
}

export function reduceDocument(
	snapshot: ReturnType<typeof createDocumentSnapshot>,
	event: DocumentEvent,
) {
	return stepDeusMachine(documentMachine, snapshot, event).snapshot;
}

export function documentPhase(state: readonly string[]): "empty" | "loading" | "ready" | "failed" {
	return state.at(-1) as "empty" | "loading" | "ready" | "failed";
}
