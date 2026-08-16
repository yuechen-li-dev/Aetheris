/* eslint-disable react-refresh/only-export-components -- the presentation contract and its pure deterministic layout helpers are tested together with the rendering adapter. */
import { Html, Line } from "@react-three/drei";
import { useFrame, useThree } from "@react-three/fiber";
import { useEffect, useMemo, useRef, useState } from "react";
import { Vector3 } from "three";
import type { CadmataEntity, CadmataVisualizationArtifact } from "./conceptVisualization";
import { formatPmiLabel, SEMANTIC_PMI_KINDS } from "./semanticInspection";
import type { ViewportTheme } from "./viewportTheme";

export type PmiCategory = "datums" | "dimensions" | "geometricTolerances" | "engineeringAnnotations";
export type PmiVisibility = Record<PmiCategory, boolean>;
export const DEFAULT_PMI_VISIBILITY: PmiVisibility = {
	datums: true,
	dimensions: false,
	geometricTolerances: true,
	engineeringAnnotations: false,
};

type Point = [number, number, number];
type ScreenPoint = { x: number; y: number; z: number };
type LayoutItem = { entity: CadmataEntity; anchor: Point; width: number; height: number };
type Placement = { entity: CadmataEntity; anchor: Point; label: Point; hidden: boolean };

function anchor(entity: CadmataEntity): Point | null {
	const geometry = entity.geometry;
	if (geometry?.type === "circle") return [geometry.center.x, geometry.center.y, geometry.center.z];
	if (geometry?.type === "point") return [geometry.point.x, geometry.point.y, geometry.point.z];
	if (geometry?.type === "polyline" && geometry.points.length) {
		const point = geometry.points[geometry.points.length - 1];
		return [point.x, point.y, point.z];
	}
	return null;
}

export function pmiCategory(entity: CadmataEntity): PmiCategory | null {
	if (entity.kind === "Datum") return "datums";
	if (entity.kind === "Dimension" || entity.kind === "Diameter" || entity.kind === "HoleDiameter") return "dimensions";
	if (entity.kind === "Position") return "geometricTolerances";
	if (entity.kind === "Annotation") return "engineeringAnnotations";
	return null;
}

function priority(entity: CadmataEntity) {
	const category = pmiCategory(entity);
	return category === "datums" ? 0 : category === "geometricTolerances" ? 1 : category === "dimensions" ? 2 : 3;
}

function estimatedSize(entity: CadmataEntity) {
	const category = pmiCategory(entity);
	if (category === "datums") return { width: 86, height: 38 };
	if (category === "geometricTolerances") return { width: 190, height: 54 };
	if (category === "engineeringAnnotations") return { width: 230, height: 82 };
	return { width: 166, height: 48 };
}

export function semanticPmiItems(artifact: CadmataVisualizationArtifact, visibility: PmiVisibility): LayoutItem[] {
	return artifact.entities
		.filter((entity) => SEMANTIC_PMI_KINDS.has(entity.kind))
		.filter((entity) => {
			const category = pmiCategory(entity);
			return category !== null && visibility[category];
		})
		.map((entity) => {
			const resolvedAnchor = anchor(entity);
			return resolvedAnchor ? { entity, anchor: resolvedAnchor, ...estimatedSize(entity) } : null;
		})
		.filter((item): item is LayoutItem => item !== null)
		.sort((left, right) => priority(left.entity) - priority(right.entity) || left.entity.stableId.localeCompare(right.entity.stableId));
}

function overlaps(a: { x: number; y: number; width: number; height: number }, b: { x: number; y: number; width: number; height: number }) {
	return Math.abs(a.x - b.x) < (a.width + b.width) / 2 + 8 && Math.abs(a.y - b.y) < (a.height + b.height) / 2 + 6;
}

/** Deterministic bounded greedy screen-space placement. */
export function layoutPmiCallouts(
	items: readonly (LayoutItem & { screenAnchor: ScreenPoint })[],
	width: number,
	height: number,
	manualOffsets: ReadonlyMap<string, { x: number; y: number }>,
) {
	const placed: { x: number; y: number; width: number; height: number }[] = [];
	return items.map((item, index) => {
		const manual = manualOffsets.get(item.entity.stableId);
		const radialCandidates = Array.from({ length: 32 }, (_, attempt) => {
			const ring = Math.floor(attempt / 8);
			const angle = ((attempt + index * 3) % 8) * (Math.PI / 4);
			const radius = 68 + ring * 58;
			return { x: item.screenAnchor.x + Math.cos(angle) * radius, y: item.screenAnchor.y + Math.sin(angle) * radius };
		});
		const gridCandidates = Array.from({ length: 10 }, (_, slot) => ({
			x: slot % 2 === 0 ? item.width / 2 + 18 : width - item.width / 2 - 18,
			y: 106 + Math.floor(slot / 2) * Math.max(item.height + 14, 92),
		}));
		const candidates = manual
			? [{ x: item.screenAnchor.x + manual.x, y: item.screenAnchor.y + manual.y }]
			: [...radialCandidates, ...gridCandidates];
		let candidate = candidates.find((point) => {
			const box = { ...point, width: item.width, height: item.height };
			return point.x - item.width / 2 >= 8 && point.x + item.width / 2 <= width - 8 && point.y - item.height / 2 >= 56 && point.y + item.height / 2 <= height - 8 && !placed.some((other) => overlaps(box, other));
		});
		const hidden = !candidate && priority(item.entity) >= 3 && items.length > 10;
		candidate ??= candidates[candidates.length - 1];
		candidate = {
			x: Math.max(item.width / 2 + 8, Math.min(width - item.width / 2 - 8, candidate.x)),
			y: Math.max(item.height / 2 + 56, Math.min(height - item.height / 2 - 8, candidate.y)),
		};
		if (!hidden) placed.push({ ...candidate, width: item.width, height: item.height });
		return { ...candidate, hidden };
	});
}

function CalloutContent({ entity }: { entity: CadmataEntity }) {
	if (entity.kind === "Datum") return <><strong className="pmi-callout__datum">{entity.label.replace(/^Datum\s+/i, "")}</strong><span>DATUM</span></>;
	if (entity.kind === "Position") return <><strong>POSITION · ⌀{String(entity.metadata?.nominal ?? "?")} {String(entity.metadata?.unit ?? "mm")}</strong><small>{String(entity.metadata?.datumRefs ?? "No datum frame")} · {String(entity.metadata?.target ?? "")}</small></>;
	if (entity.kind === "Annotation") return <><strong>{entity.label}</strong><small>{String(entity.metadata?.text ?? "")}</small></>;
	return <><strong>{formatPmiLabel(entity)} {String(entity.metadata?.unit ?? "")}</strong><small>{String(entity.metadata?.target ?? "")}</small></>;
}

export function PmiAnnotationLayer({
	artifact,
	visible,
	visibility,
	selectedIds,
	onSelect,
	theme,
}: {
	artifact: CadmataVisualizationArtifact | null;
	visible: boolean;
	visibility: PmiVisibility;
	selectedIds: Set<string>;
	onSelect: (id: string) => void;
	theme: ViewportTheme;
}) {
	const { camera, size } = useThree();
	const items = useMemo(() => artifact ? semanticPmiItems(artifact, visibility) : [], [artifact, visibility]);
	const [placements, setPlacements] = useState<Placement[]>([]);
	const [manualOffsets, setManualOffsets] = useState<Map<string, { x: number; y: number }>>(new Map());
	const drag = useRef<{ id: string; originX: number; originY: number; initial: { x: number; y: number } } | null>(null);
	const lastSignature = useRef("");

	useEffect(() => {
		const move = (event: PointerEvent) => {
			if (!drag.current) return;
			const current = drag.current;
			setManualOffsets((previous) => new Map(previous).set(current.id, {
				x: current.initial.x + event.clientX - current.originX,
				y: current.initial.y + event.clientY - current.originY,
			}));
		};
		const up = () => { drag.current = null; };
		window.addEventListener("pointermove", move);
		window.addEventListener("pointerup", up);
		return () => { window.removeEventListener("pointermove", move); window.removeEventListener("pointerup", up); };
	}, []);

	useFrame(() => {
		if (!visible || items.length === 0) return;
		camera.updateMatrixWorld(false);
		const signature = `${size.width}:${size.height}:${camera.matrixWorld.elements.map((value) => value.toFixed(4)).join(",")}:${camera.projectionMatrix.elements.map((value) => value.toFixed(4)).join(",")}:${items.map((item) => item.entity.stableId).join("|")}:${JSON.stringify([...manualOffsets])}`;
		if (signature === lastSignature.current) return;
		lastSignature.current = signature;
		const projected = items.map((item) => {
			const screen = new Vector3(...item.anchor).project(camera);
			return { ...item, screenAnchor: { x: (screen.x + 1) * size.width / 2, y: (1 - screen.y) * size.height / 2, z: screen.z } };
		});
		const layout = layoutPmiCallouts(projected, size.width, size.height, manualOffsets);
		setPlacements(projected.map((item, index) => {
			const screen = layout[index];
			const world = new Vector3(screen.x / size.width * 2 - 1, 1 - screen.y / size.height * 2, item.screenAnchor.z).unproject(camera);
			return { entity: item.entity, anchor: item.anchor, label: [world.x, world.y, world.z], hidden: screen.hidden };
		}));
	});

	if (!visible || !artifact) return null;
	return <group name="semantic-pmi-annotations">
		{placements.filter((placement) => !placement.hidden).map(({ entity, anchor: target, label }) => {
			const category = pmiCategory(entity)!;
			const selected = selectedIds.has(entity.stableId);
			const color = selected ? theme.annotation.selected : category === "datums" ? theme.annotation.datum : category === "dimensions" ? theme.annotation.dimension : theme.annotation.text;
			const wholePart = !(entity.topology?.faceIds?.length);
			return <group key={entity.stableId}>
				<Line points={[target, label]} color={theme.annotation.leader} lineWidth={selected ? 2.5 : 1.2} depthTest={false} />
				<Html position={label} center style={{ pointerEvents: "auto" }} zIndexRange={selected ? [90, 80] : [60, 10]}>
					<button
						className={`pmi-callout pmi-callout--${category}${wholePart ? " pmi-callout--global" : ""}${selected ? " is-selected" : ""}`}
						type="button"
						onPointerDown={(event) => {
							event.stopPropagation();
							const initial = manualOffsets.get(entity.stableId) ?? { x: 0, y: 0 };
							drag.current = { id: entity.stableId, originX: event.clientX, originY: event.clientY, initial };
						}}
						onClick={(event) => { event.stopPropagation(); onSelect(entity.stableId); }}
						style={{ color, background: theme.annotation.background, borderColor: color }}
						aria-label={`Inspect ${entity.label}`}
						title="Select; drag to adjust presentation only"
					>
						<CalloutContent entity={entity} />
					</button>
				</Html>
			</group>;
		})}
	</group>;
}
