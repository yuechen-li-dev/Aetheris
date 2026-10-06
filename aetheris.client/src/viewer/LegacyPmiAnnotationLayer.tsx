import { Html, Line } from "@react-three/drei";
import { useFrame, useThree } from "@react-three/fiber";
import { useEffect, useMemo, useRef, useState } from "react";
import { Vector3 } from "three";
import type { CadmataEntity, CadmataVisualizationArtifact } from "./conceptVisualization";
import type { ViewportTheme } from "./viewportTheme";
import { semanticPmiItems, layoutPmiCallouts, pmiCategory, CalloutContent, type PmiVisibility } from "./pmiPresentation";
type Point = [number, number, number];
type Placement = { entity: CadmataEntity; anchor: Point; label: Point; hidden: boolean };
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
