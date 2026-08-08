import { Html, Line } from "@react-three/drei";
import type { CadmataEntity, CadmataVisualizationArtifact } from "./conceptVisualization";
import { formatPmiLabel } from "./semanticInspection";
import type { ViewportTheme } from "./viewportTheme";

function anchor(entity: CadmataEntity): [number, number, number] | null {
	const geometry = entity.geometry;
	if (geometry?.type === "circle") return [geometry.center.x, geometry.center.y, geometry.center.z];
	if (geometry?.type === "point") return [geometry.point.x, geometry.point.y, geometry.point.z];
	if (geometry?.type === "polyline" && geometry.points.length) { const p = geometry.points[geometry.points.length - 1]; return [p.x, p.y, p.z]; }
	return null;
}

export function PmiAnnotationLayer({ artifact, visible, selectedIds, onSelect, theme }: { artifact: CadmataVisualizationArtifact | null; visible: boolean; selectedIds: Set<string>; onSelect: (id: string) => void; theme: ViewportTheme }) {
	if (!visible || !artifact) return null;
	return <group name="semantic-pmi-annotations">{artifact.entities.filter((entity) => entity.kind === "HoleDiameter" || entity.kind === "Datum").map((entity) => {
		const target = anchor(entity); if (!target) return null;
		const isDatum = entity.kind === "Datum"; const selected = selectedIds.has(entity.stableId);
		const offset: [number, number, number] = [target[0] + (isDatum ? -12 : 12), target[1] + 2.4, target[2] + 0.2];
		const color = selected ? theme.annotation.selected : isDatum ? theme.annotation.datum : theme.annotation.dimension;
		return <group key={entity.stableId}><Line points={[target, offset]} color={theme.annotation.leader} lineWidth={selected ? 2.5 : 1.3} />
			<Html position={offset} center style={{ pointerEvents: "auto" }}><button className="pmi-callout" type="button" onClick={(event) => { event.stopPropagation(); onSelect(entity.stableId); }} style={{ color, background: theme.annotation.background, borderColor: color }} aria-label={`Inspect ${entity.label}`}><span>{isDatum ? `DATUM ${entity.label}` : formatPmiLabel(entity)}</span>{!isDatum && entity.metadata?.datumRefs ? <small>{String(entity.metadata.datumRefs)}</small> : null}</button></Html>
		</group>;
	})}</group>;
}
