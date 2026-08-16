/* eslint-disable react-refresh/only-export-components -- layer contracts and the overlay intentionally share one small module. */
import { Line } from "@react-three/drei";
import { useMemo } from "react";
import { DoubleSide, Vector3 } from "three";
import type {
	CadmataEntity,
	CadmataLayer,
	CadmataVisualizationArtifact,
} from "./conceptVisualization";
import type { ViewportTheme } from "./viewportTheme";
import { SEMANTIC_PMI_KINDS } from "./semanticInspection";

export type CadmataLayerVisibility = Record<CadmataLayer, boolean>;
export const DEFAULT_CADMATA_LAYERS: CadmataLayerVisibility = {
	material: true,
	brepEdges: true,
	conceptPoints: true,
	conceptAxes: true,
	conceptRegions: true,
	conceptPlanes: true,
	constructionPlanes: true,
	profileGuides: true,
	profileLoops: true,
	composeRegions: true,
	selections: true,
	diagnostics: true,
};

function colorFor(entity: CadmataEntity, selected: boolean, theme: ViewportTheme) {
	if (selected) return theme.overlay.selection;
	if (entity.layer === "profileGuides" || entity.layer === "profileLoops")
		return theme.overlay.profile;
	if (entity.layer === "composeRegions") return theme.overlay.compose;
	if (entity.layer === "diagnostics") return theme.overlay.diagnostic;
	return theme.overlay.concept;
}
function OverlayEntity({
	entity,
	selected,
	onSelect,
	theme,
}: {
	entity: CadmataEntity;
	selected: boolean;
	onSelect: (id: string) => void;
	theme: ViewportTheme;
}) {
	const geometry = entity.geometry;
	const color = colorFor(entity, selected, theme);
	const points = useMemo(
		() =>
			geometry?.type === "polyline"
				? geometry.points.map((p) => [p.x, p.y, p.z] as [number, number, number])
				: [],
		[geometry],
	);
	if (!geometry) return null;
	if (geometry.type === "point")
		return (
			<mesh
				position={[geometry.point.x, geometry.point.y, geometry.point.z]}
				onClick={(event) => {
					event.stopPropagation();
					onSelect(entity.stableId);
				}}
			>
				<sphereGeometry args={[1.8, 12, 8]} />
				<meshBasicMaterial color={color} depthTest={false} />
			</mesh>
		);
	if (geometry.type === "circle") {
		const count = 48;
		const points = Array.from({ length: count + 1 }, (_, i) => {
			const a = (i / count) * Math.PI * 2;
			return [
				geometry.center.x + Math.cos(a) * geometry.radius,
				geometry.center.y + Math.sin(a) * geometry.radius,
				geometry.center.z,
			] as [number, number, number];
		});
		return (
			<Line
				points={points}
				color={color}
				lineWidth={selected ? 3.5 : 1.5}
				onClick={(event) => {
					event.stopPropagation();
					onSelect(entity.stableId);
				}}
			/>
		);
	}
	if (geometry.type === "plane")
		return (
			<mesh
				position={[geometry.origin.x, geometry.origin.y, geometry.origin.z]}
				onClick={(event) => {
					event.stopPropagation();
					onSelect(entity.stableId);
				}}
			>
				<planeGeometry
					args={[
						new Vector3(geometry.u.x, geometry.u.y, geometry.u.z).length() * 2,
						new Vector3(geometry.v.x, geometry.v.y, geometry.v.z).length() * 2,
					]}
				/>
				<meshBasicMaterial
					color={color}
					transparent
					opacity={0.12}
					side={DoubleSide}
					depthWrite={false}
				/>
			</mesh>
		);
	return (
		<Line
			points={geometry.closed ? [...points, points[0]] : points}
			color={color}
			lineWidth={selected ? 4 : entity.layer === "profileLoops" ? 2.5 : 1.25}
			onClick={(event) => {
				event.stopPropagation();
				onSelect(entity.stableId);
			}}
		/>
	);
}
export function CadmataOverlay({
	artifact,
	layers,
	selectedIds,
	onSelect,
	theme,
}: {
	artifact: CadmataVisualizationArtifact | null;
	layers: CadmataLayerVisibility;
	selectedIds: Set<string>;
	onSelect: (id: string) => void;
	theme: ViewportTheme;
}) {
	if (!artifact) return null;
	return (
		<group name="CadmataCompilerOverlays">
			{artifact.entities
				.filter((entity) => layers[entity.layer] && !SEMANTIC_PMI_KINDS.has(entity.kind) && entity.kind !== "EngineeringTarget")
				.map((entity) => (
					<OverlayEntity
						key={entity.stableId}
						entity={entity}
						selected={selectedIds.has(entity.stableId)}
						onSelect={onSelect}
						theme={theme}
					/>
				))}
		</group>
	);
}
