import { describe, expect, it } from "vitest";
import {
	createDocumentSnapshot,
	documentPhase,
	reduceDocument,
} from "../application/documentMachine";

describe("documentMachine", () => {
	it("models open, success, reload, failure, and close explicitly", () => {
		let snapshot = createDocumentSnapshot();
		expect(documentPhase(snapshot.state)).toBe("empty");

		snapshot = reduceDocument(snapshot, {
			type: "Open",
			source: { kind: "startup-file", fileName: "plate.step" },
		});
		expect(documentPhase(snapshot.state)).toBe("loading");
		expect(snapshot.board.source).toEqual({ kind: "startup-file", fileName: "plate.step" });

		snapshot = reduceDocument(snapshot, { type: "LoadSucceeded", documentId: "doc-1" });
		expect(documentPhase(snapshot.state)).toBe("ready");
		snapshot = reduceDocument(snapshot, { type: "Reload" });
		expect(documentPhase(snapshot.state)).toBe("loading");
		snapshot = reduceDocument(snapshot, { type: "LoadFailed", error: "bad STEP" });
		expect(documentPhase(snapshot.state)).toBe("failed");
		expect(snapshot.board.error).toBe("bad STEP");
		snapshot = reduceDocument(snapshot, { type: "Close" });
		expect(documentPhase(snapshot.state)).toBe("empty");
		expect(snapshot.board.source).toBeNull();
	});
});
