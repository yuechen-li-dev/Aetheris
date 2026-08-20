import { useEffect, useMemo, useState } from "react";
import { ApiError, invokeGalleryTemplate, listGalleryTemplates, type ForgeTemplateDescriptionDto, type GalleryArtifactDto } from "../api/aetherisApi";
import { Button } from "./ui/button";

const DEFAULT_SELECTED = "Standard.Products.Mechanical.MountingPlate";

function schemaDefaults(template: ForgeTemplateDescriptionDto | undefined): Record<string, string | number> {
	if (!template) return {};
	return Object.fromEntries(template.parameters
		.flatMap((parameter) => parameter.fields ?? [parameter])
		.filter((field) => field.default !== null && field.default !== undefined)
		.map((field) => [field.name, field.type === "number" || field.type === "integer" ? Number(field.default) : field.default!]));
}

function download(artifact: GalleryArtifactDto) {
	const url = URL.createObjectURL(new Blob([artifact.content], { type: artifact.contentType }));
	const anchor = document.createElement("a"); anchor.href = url; anchor.download = artifact.name; anchor.click(); URL.revokeObjectURL(url);
}

export function ProductGallery({ onPreview }: { onPreview: (step: string, name: string) => Promise<void> }) {
	const [templates, setTemplates] = useState<ForgeTemplateDescriptionDto[]>([]);
	const [selectedId, setSelectedId] = useState(DEFAULT_SELECTED);
	const [values, setValues] = useState<Record<string, string | number>>({});
	const [artifacts, setArtifacts] = useState<GalleryArtifactDto[]>([]);
	const [status, setStatus] = useState("Loading Standard Library…");
	const selected = useMemo(() => templates.find((item) => item.id === selectedId), [templates, selectedId]);
	const fields = selected?.parameters.flatMap((parameter) => parameter.fields ?? [parameter]) ?? [];
	useEffect(() => { void listGalleryTemplates().then((items) => { setTemplates(items); setValues(schemaDefaults(items.find((item) => item.id === DEFAULT_SELECTED))); setStatus(`${items.length} product families available.`); }).catch((error: Error) => setStatus(error.message)); }, []);
	const choose = (id: string) => { setSelectedId(id); setValues(schemaDefaults(templates.find((item) => item.id === id))); setArtifacts([]); setStatus("Ready to generate."); };
	const generate = async () => {
		if (!selected) return; setStatus("Generating through Forge.Host…");
		try {
			const result = await invokeGalleryTemplate(selected.id, values, selected.artifacts); setArtifacts(result.artifacts);
			const step = result.artifacts.find((artifact) => artifact.kind === "StepAp242"); if (step) await onPreview(step.content, `${selected.displayName}.step`);
			setStatus(`Generated in ${result.executionMilliseconds.toFixed(1)} ms · SHA256 ${step?.sha256.slice(0, 12) ?? "n/a"}.`);
		} catch (error) {
			const failure = error instanceof ApiError ? error : new ApiError((error as Error).message, []); setStatus(failure.diagnostics.map((item) => item.message).join(" · ") || failure.message);
		}
	};
	return <section className="tool-section product-gallery">
		<p className="product-gallery__eyebrow">AETHERIS STANDARD LIBRARY</p><h2>Engineering Product Gallery</h2>
		<div className="product-gallery__cards">{templates.map((template) => <button type="button" key={template.id} className={template.id === selectedId ? "product-card active-row" : "product-card"} onClick={() => choose(template.id)}><strong>{template.displayName}</strong><span>{template.outputKind === "SheetMetal" ? "formed + flat" : "STEP AP242"}</span></button>)}</div>
		{selected ? <><h3>{selected.displayName}</h3><p>{selected.documentation}</p><div className="form-grid product-gallery__form">
			{fields.map((field) => <label key={field.name}>{field.name}{field.unit ? ` (${field.unit})` : ""}{field.allowedValues ? <select value={String(values[field.name] ?? "")} onChange={(event) => setValues((current) => ({ ...current, [field.name]: event.target.value }))}>{field.allowedValues.map((value) => <option key={value}>{value}</option>)}</select> : <input type="text" value={String(values[field.name] ?? "")} onChange={(event) => setValues((current) => ({ ...current, [field.name]: field.type === "number" ? Number(event.target.value) : event.target.value }))} />}</label>)}
		</div><div className="stack-row"><Button type="button" onClick={() => void generate()}>Generate / Update Part</Button>{artifacts.map((artifact) => <Button key={artifact.kind} type="button" variant="secondary" onClick={() => download(artifact)}>Download {artifact.kind}</Button>)}</div><p role="status" className="demo-notice">{status}</p></> : <p>{status}</p>}
	</section>;
}
