import type { DiagnosticDto } from "../api/aetherisApi";
import { Button } from "./ui/button";

/** Keep engineering status separate from verbose importer/debug evidence. */
export function DiagnosticSummary({ diagnostics }: { diagnostics: DiagnosticDto[] }) {
	if (diagnostics.length === 0) return null;
	const warnings = diagnostics.filter(d => d.severity.toLowerCase() === "warning").length;
	const infos = diagnostics.filter(d => ["info", "information"].includes(d.severity.toLowerCase())).length;
	// Unknown severities stay visible rather than being silently hidden.
	const errors = diagnostics.filter(d => !["warning", "info", "information"].includes(d.severity.toLowerCase()));
	const count = (n: number, label: string) => n ? `${n} ${label}${n === 1 ? "" : "s"}` : "";
	const counts = [count(errors.length, "error"), count(warnings, "warning"), count(infos, "informational message")].filter(Boolean).join(", ");
	const download = () => {
		const content = `Cadmata diagnostics\nCaptured: ${new Date().toISOString()}\n${counts}\n\n`
			+ diagnostics.map(d => `[${d.severity}] ${d.code}${d.source ? ` (${d.source})` : ""}: ${d.message}`).join("\n\n");
		const url = URL.createObjectURL(new Blob([content], { type: "text/plain;charset=utf-8" }));
		const link = document.createElement("a");
		link.href = url;
		link.download = "cadmata-diagnostics.log";
		link.click();
		setTimeout(() => URL.revokeObjectURL(url), 1000);
	};
	return (
		<div className="diagnostic-summary">
			<p>{counts}. Full details are available in the diagnostic log.</p>
			{errors.length > 0 && (
				<ul>
					{errors.map((d, i) => (
						<li key={`${d.code}-${i}`}>[{d.severity}] {d.code}: {d.message}</li>
					))}
				</ul>
			)}
			<Button type="button" variant="outline" onClick={download}>Download diagnostic log</Button>
		</div>
	);
}
