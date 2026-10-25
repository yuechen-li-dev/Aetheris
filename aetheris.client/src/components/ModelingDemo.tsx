import { useState } from "react";
import { composeBlock } from "../api/aetherisApi";
import { Button } from "./ui/button";

export function ModelingDemo({ onPreview }: { onPreview(step: string, name: string): Promise<boolean> }) {
	const [values, setValues] = useState({ width: 80, depth: 50, height: 8, padWidth: 30, padDepth: 20, padHeight: 12 });
	const [busy, setBusy] = useState(false);
	const [message, setMessage] = useState("Build a base with a centered raised block. All dimensions are in mm.");
	const [source, setSource] = useState("");
	const generate = async () => {
		if (busy) return;
		setBusy(true);
		try {
			const built = await composeBlock(values);
			setSource(built.source);
			if (!await onPreview(built.stepText, built.exportPath)) throw new Error("Build succeeded, but display failed. See import diagnostics.");
			setMessage("Composed block built. Edit dimensions and build again to update it.");
		} catch (error) { setMessage(error instanceof Error ? error.message : "Build failed."); }
		finally { setBusy(false); }
	};
	return <section className="tool-section">
		<h2 className="section-title">Compose blocks</h2>
		<p>Construct one part from a base and a raised block using Firmament Compose.</p>
		<div className="form-grid">
			{(Object.keys(values) as (keyof typeof values)[]).map(key => <label key={key}>
				{({ width: "Base width", depth: "Base depth", height: "Base height", padWidth: "Raised width", padDepth: "Raised depth", padHeight: "Raised height" })[key]} (mm)
				<input type="number" min="0.1" step="0.1" value={values[key]} onChange={event => setValues(current => ({ ...current, [key]: Number(event.target.value) }))} />
			</label>)}
		</div>
		<Button type="button" disabled={busy} onClick={() => void generate()}>{busy ? "Building…" : "Build composed part"}</Button>
		<p role="status">{message}</p>
		{source && <details><summary>Firmament source</summary><pre style={{ whiteSpace: "pre-wrap" }}>{source}</pre></details>}
	</section>;
}
