import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { DiagnosticSummary } from "../components/DiagnosticSummary";

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it("summarizes nonfatal details and downloads every original diagnostic", async () => {
	const diagnostics = [
		{ code: "Unknown", severity: "Info", source: "Importer.Surface", message: "Spline #4617 reduced within the 0.1 mm budget; analytic candidates rejected." },
		{ code: "IncompleteCheck", severity: "Warning", source: "Importer.Qualification", message: "Engineering checks incomplete." },
		{ code: "ValidationFailed", severity: "Error", source: "Viewer.Timeout", message: "View materialization failed." },
	];
	const createObjectURL = vi.fn<(blob: Blob) => string>(() => "blob:diagnostics");
	vi.stubGlobal("URL", { createObjectURL, revokeObjectURL: vi.fn() });
	const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
	render(<DiagnosticSummary diagnostics={diagnostics} />);
	expect(screen.getByText(/1 error, 1 warning, 1 informational message/)).toBeTruthy();
	expect(screen.queryByText(/Spline #4617/)).toBeNull();
	expect(screen.queryByText("Engineering checks incomplete.")).toBeNull();
	expect(screen.getByText("[Error] ValidationFailed: View materialization failed.")).toBeTruthy();
	fireEvent.click(screen.getByRole("button", { name: "Download diagnostic log" }));
	expect((click.mock.instances[0] as HTMLAnchorElement).download).toBe("cadmata-diagnostics.log");
	const blob = createObjectURL.mock.calls[0][0] as Blob;
	const text = await new Promise<string>((resolve, reject) => {
		const reader = new FileReader();
		reader.onload = () => resolve(String(reader.result));
		reader.onerror = reject;
		reader.readAsText(blob);
	});
	for (const d of diagnostics) expect(text).toContain(`[${d.severity}] ${d.code} (${d.source}): ${d.message}`);
});

it("leaves an unfamiliar severity visible and keeps a quiet import quiet", () => {
	const { rerender } = render(<DiagnosticSummary diagnostics={[]} />);
	expect(screen.queryByRole("button")).toBeNull();
	rerender(<DiagnosticSummary diagnostics={[{ code: "FatalImport", severity: "Fatal", source: null, message: "Cannot import body." }]} />);
	expect(screen.getByText("[Fatal] FatalImport: Cannot import body.")).toBeTruthy();
});
