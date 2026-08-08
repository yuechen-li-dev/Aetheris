import { Line, OrbitControls, Text } from "@react-three/drei";
import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { matchKind } from "machinalayout/match";
import { useEffect, useMemo, useRef, useState } from "react";
import {
	ACESFilmicToneMapping,
	BufferAttribute,
	BufferGeometry,
	DoubleSide,
	Float32BufferAttribute,
	LineBasicMaterial,
	MeshStandardMaterial,
	OrthographicCamera,
	Raycaster,
	SRGBColorSpace,
	Vector2,
	Vector3,
} from "three";
import type { DisplayScene } from "./displayRenderables";
import { computeDisplaySceneBounds, computeOrthographicCameraFit } from "./displaySceneBounds";
import { buildAdaptiveGridPlan, type GridBounds } from "./logarithmicGrid";
import { CadmataOverlay, type CadmataLayerVisibility } from "./CadmataOverlay";
import type { CadmataVisualizationArtifact } from "./conceptVisualization";
import { ATELIER_VIEWPORT_THEME, type ViewportTheme } from "./viewportTheme";

function intersectGround(origin: Vector3, direction: Vector3, y: number): Vector3 | null {
	if (Math.abs(direction.y) < 1e-6) return null;
	const distance = (y - origin.y) / direction.y;
	return Number.isFinite(distance) ? origin.clone().addScaledVector(direction, distance) : null;
}

function visibleGroundBounds(
	camera: OrthographicCamera,
	y: number,
	extentScale: number,
): GridBounds {
	const zoom = Math.max(camera.zoom, 0.0001);
	const halfWidth = Math.abs(camera.right - camera.left) / (2 * zoom);
	const halfHeight = Math.abs(camera.top - camera.bottom) / (2 * zoom);
	const right = new Vector3();
	const up = new Vector3();
	const forward = new Vector3();
	camera.updateMatrixWorld(false);
	camera.matrixWorld.extractBasis(right, up, forward);
	const direction = forward.negate().normalize();
	const hits = [-1, 1]
		.flatMap((x) =>
			[-1, 1].map((z) =>
				intersectGround(
					camera.position
						.clone()
						.addScaledVector(right, x * halfWidth)
						.addScaledVector(up, z * halfHeight),
					direction,
					y,
				),
			),
		)
		.filter((hit): hit is Vector3 => hit !== null);

	if (hits.length < 2) {
		const extent = Math.max(12 / zoom, 4);
		return {
			minX: camera.position.x - extent,
			maxX: camera.position.x + extent,
			minZ: camera.position.z - extent,
			maxZ: camera.position.z + extent,
		};
	}
	const minX = Math.min(...hits.map((hit) => hit.x));
	const maxX = Math.max(...hits.map((hit) => hit.x));
	const minZ = Math.min(...hits.map((hit) => hit.z));
	const maxZ = Math.max(...hits.map((hit) => hit.z));
	const margin = Math.max(maxX - minX, maxZ - minZ) * Math.max(0, extentScale - 1) * 0.5;
	return { minX: minX - margin, maxX: maxX + margin, minZ: minZ - margin, maxZ: maxZ + margin };
}

function GridSegments({
	positions,
	color,
	opacity,
}: {
	positions: Float32Array;
	color: string;
	opacity: number;
}) {
	const geometry = useMemo(() => {
		const next = new BufferGeometry();
		next.setAttribute("position", new Float32BufferAttribute(positions, 3));
		return next;
	}, [positions]);
	const material = useMemo(
		() => new LineBasicMaterial({ color, transparent: true, opacity, depthWrite: false }),
		[color, opacity],
	);
	useEffect(
		() => () => {
			geometry.dispose();
			material.dispose();
		},
		[geometry, material],
	);
	return <lineSegments geometry={geometry} material={material} frustumCulled={false} />;
}

function AdaptiveLogGrid({ theme }: { theme: ViewportTheme }) {
	const { camera, gl } = useThree();
	const [revision, setRevision] = useState(0);
	const last = useMemo(() => ({ x: Number.NaN, z: Number.NaN, zoom: Number.NaN }), []);

	useFrame(() => {
		const orthographic = camera as OrthographicCamera;
		const zoom = Math.max(orthographic.zoom, 0.0001);
		const visibleSpan = Math.max(Math.abs(orthographic.right - orthographic.left) / zoom, 1);
		const moved =
			!Number.isFinite(last.x) ||
			Math.hypot(camera.position.x - last.x, camera.position.z - last.z) > visibleSpan * 0.04;
		const zoomed =
			!Number.isFinite(last.zoom) ||
			Math.abs(zoom - last.zoom) / Math.max(last.zoom, 0.0001) > 0.04;
		if (moved || zoomed) {
			last.x = camera.position.x;
			last.z = camera.position.z;
			last.zoom = zoom;
			setRevision((value) => value + 1);
		}
	});

	const plan = useMemo(() => {
		void revision;
		const style = theme.gridStyle;
		return buildAdaptiveGridPlan({
			bounds: visibleGroundBounds(camera as OrthographicCamera, style.yOffset, style.extentScale),
			targetCellCount: style.targetCellCount,
			maxLinesPerAxis: style.maxLinesPerAxis,
			majorStep: style.majorStep,
			y: style.yOffset,
		});
	}, [camera, revision, theme]);
	useEffect(() => {
		if (typeof window !== "undefined" && new URLSearchParams(window.location.search).has("perf")) {
			publishPerformance(gl.domElement, {
				...(window.__cadmataPerformance ?? {}),
				gridLineCount: plan.lineCount,
				gridDrawCalls: plan.drawCallCount,
				gridAllocatedBytes: plan.allocatedBytes,
			});
		}
	}, [gl, plan]);

	return (
		<group
			name="adaptive-log-grid"
			userData={{
				lineCount: plan.lineCount,
				drawCallCount: plan.drawCallCount,
				allocatedBytes: plan.allocatedBytes,
			}}
		>
			{plan.layers.map((layer, index) => (
				<group key={`${layer.spacing}-${index}`}>
					{layer.minorLineCount > 0 ? (
						<GridSegments
							positions={layer.minorPositions}
							color={theme.gridStyle.minorColor}
							opacity={theme.gridStyle.minorOpacity * layer.weight}
						/>
					) : null}
					{layer.majorLineCount > 0 ? (
						<GridSegments
							positions={layer.majorPositions}
							color={theme.gridStyle.majorColor}
							opacity={theme.gridStyle.majorOpacity * layer.weight}
						/>
					) : null}
				</group>
			))}
		</group>
	);
}

function publishPerformance(
	canvas: HTMLCanvasElement,
	patch: NonNullable<Window["__cadmataPerformance"]>,
) {
	let current: NonNullable<Window["__cadmataPerformance"]> = {};
	try {
		current = JSON.parse(canvas.dataset.cadmataPerformance ?? "{}") as typeof current;
	} catch {
		current = {};
	}
	const next = { ...current, ...patch };
	canvas.dataset.cadmataPerformance = JSON.stringify(next);
	window.__cadmataPerformance = next;
}

declare global {
	interface Window {
		__cadmataPerformance?: {
			averageFrameMs?: number;
			frameSamples?: number;
			drawCalls?: number;
			triangles?: number;
			geometries?: number;
			textures?: number;
			gridLineCount?: number;
			gridDrawCalls?: number;
			gridAllocatedBytes?: number;
		};
	}
}

function ViewportPerformanceProbe({ resetKey }: { resetKey: unknown }) {
	const { gl } = useThree();
	const previousTime = useRef<number | null>(null);
	const totalFrameTime = useRef(0);
	const samples = useRef(0);
	const enabled = useMemo(
		() => typeof window !== "undefined" && new URLSearchParams(window.location.search).has("perf"),
		[],
	);
	useEffect(() => {
		previousTime.current = null;
		totalFrameTime.current = 0;
		samples.current = 0;
	}, [resetKey]);

	useFrame(() => {
		if (!enabled) return;
		const now = performance.now();
		if (previousTime.current !== null) {
			const frameTime = now - previousTime.current;
			if (frameTime < 250) {
				totalFrameTime.current += frameTime;
				samples.current += 1;
			}
		}
		previousTime.current = now;
		if (samples.current > 0 && samples.current % 30 === 0) {
			publishPerformance(gl.domElement, {
				averageFrameMs: totalFrameTime.current / samples.current,
				frameSamples: samples.current,
				drawCalls: gl.info.render.calls,
				triangles: gl.info.render.triangles,
				geometries: gl.info.memory.geometries,
				textures: gl.info.memory.textures,
			});
		}
	});

	return null;
}

export interface AetherisViewportProps {
	displayScene?: DisplayScene | null;
	highlightedFaceId?: number | null;
	highlightedEdgeId?: number | null;
	highlightedFaceIds?: Set<number>;
	highlightedEdgeIds?: Set<number>;
	showGrid?: boolean;
	showAxisGuide?: boolean;
	theme?: ViewportTheme;
	onPickRay?: (
		origin: { x: number; y: number; z: number },
		direction: { x: number; y: number; z: number },
	) => void;
	cadmataArtifact?: CadmataVisualizationArtifact | null;
	cadmataLayers?: CadmataLayerVisibility;
	selectedCadmataIds?: Set<string>;
	onCadmataSelect?: (stableId: string) => void;
}

function FaceMesh({
	positions,
	normals,
	indices,
	isHighlighted,
	theme,
}: {
	positions: Float32Array;
	normals: Float32Array;
	indices: Uint32Array;
	isHighlighted: boolean;
	theme: ViewportTheme;
}) {
	const geometry = useMemo(() => {
		const next = new BufferGeometry();
		next.setAttribute("position", new BufferAttribute(positions, 3));
		next.setAttribute("normal", new BufferAttribute(normals, 3));
		next.setIndex(new BufferAttribute(indices, 1));
		next.computeBoundingSphere();
		return next;
	}, [indices, normals, positions]);
	const material = useMemo(
		() =>
			new MeshStandardMaterial({
				color: isHighlighted ? theme.selectedMaterial.color : theme.objectMaterial.color,
				emissive: isHighlighted ? theme.selectedMaterial.emissive : "#000000",
				emissiveIntensity: isHighlighted ? theme.selectedMaterial.emissiveIntensity : 0,
				metalness: theme.objectMaterial.metalness,
				roughness: theme.objectMaterial.roughness,
				side: DoubleSide,
			}),
		[isHighlighted, theme],
	);
	useEffect(
		() => () => {
			geometry.dispose();
			material.dispose();
		},
		[geometry, material],
	);
	return (
		<mesh
			geometry={geometry}
			material={material}
			castShadow={theme.shadowStyle.enabled}
			receiveShadow={theme.shadowStyle.enabled}
		/>
	);
}

function AxisGuide({ theme }: { theme: ViewportTheme }) {
	const end = 2;
	return (
		<group>
			<Line
				points={[
					[0, 0, 0],
					[end, 0, 0],
				]}
				color={theme.axis.x}
				lineWidth={1}
			/>
			<Line
				points={[
					[0, 0, 0],
					[0, end, 0],
				]}
				color={theme.axis.y}
				lineWidth={1}
			/>
			<Line
				points={[
					[0, 0, 0],
					[0, 0, end],
				]}
				color={theme.axis.z}
				lineWidth={1}
			/>
			{(["X", "Y", "Z"] as const).map((label, index) => (
				<Text
					key={label}
					position={index === 0 ? [2.14, 0, 0] : index === 1 ? [0, 2.14, 0] : [0, 0, 2.14]}
					fontSize={0.16}
					color={theme.axis.label}
				>
					{label}
				</Text>
			))}
		</group>
	);
}

function EdgeLine({
	points,
	isHighlighted,
	theme,
}: {
	points: Float32Array;
	isHighlighted: boolean;
	theme: ViewportTheme;
}) {
	const linePoints = useMemo(
		() =>
			Array.from(
				{ length: points.length / 3 },
				(_, index) =>
					[points[index * 3], points[index * 3 + 1], points[index * 3 + 2]] as [
						number,
						number,
						number,
					],
			),
		[points],
	);
	return (
		<Line
			points={linePoints}
			color={isHighlighted ? theme.edgeStyle.selectedColor : theme.edgeStyle.color}
			lineWidth={isHighlighted ? theme.edgeStyle.selectedWidth : theme.edgeStyle.width}
		/>
	);
}

function PickRayCapture({ onPickRay }: { onPickRay?: AetherisViewportProps["onPickRay"] }) {
	const { camera, gl } = useThree();
	useEffect(() => {
		if (!onPickRay) return;
		const raycaster = new Raycaster();
		const pointer = new Vector2();
		const handleClick = (event: MouseEvent) => {
			if (event.button !== 0) return;
			const rect = gl.domElement.getBoundingClientRect();
			pointer.set(
				((event.clientX - rect.left) / rect.width) * 2 - 1,
				-((event.clientY - rect.top) / rect.height) * 2 + 1,
			);
			raycaster.setFromCamera(pointer, camera);
			onPickRay({ ...raycaster.ray.origin }, { ...raycaster.ray.direction });
		};
		gl.domElement.addEventListener("click", handleClick);
		return () => gl.domElement.removeEventListener("click", handleClick);
	}, [camera, gl.domElement, onPickRay]);
	return null;
}

function FitCameraToScene({ displayScene }: { displayScene: DisplayScene | null }) {
	const { camera, controls, size } = useThree();
	const bounds = useMemo(() => computeDisplaySceneBounds(displayScene), [displayScene]);
	useEffect(() => {
		if (!(camera instanceof OrthographicCamera) || !bounds.isValid) return;
		const fit = computeOrthographicCameraFit(
			bounds,
			Math.abs(camera.right - camera.left) || size.width || 1,
			Math.abs(camera.top - camera.bottom) || size.height || 1,
		);
		if (!fit) return;
		camera.position.set(...fit.position);
		camera.zoom = fit.zoom;
		camera.near = fit.near;
		camera.far = fit.far;
		camera.lookAt(...fit.target);
		camera.updateProjectionMatrix();
		camera.updateMatrixWorld(false);
		if (controls && typeof controls === "object" && "target" in controls) {
			const orbit = controls as { target: Vector3; update?: () => void };
			orbit.target.set(...fit.target);
			orbit.update?.();
		}
	}, [bounds, camera, controls, size.height, size.width]);
	return null;
}

function RendererConfiguration({ theme }: { theme: ViewportTheme }) {
	const { gl } = useThree();
	useEffect(() => {
		gl.outputColorSpace = SRGBColorSpace;
		gl.toneMapping = ACESFilmicToneMapping;
		gl.toneMappingExposure = theme.environment.toneMappingExposure;
	}, [gl, theme]);
	return null;
}

export function AetherisViewport({
	displayScene = null,
	highlightedFaceId = null,
	highlightedEdgeId = null,
	highlightedFaceIds,
	highlightedEdgeIds,
	showGrid = true,
	showAxisGuide = true,
	theme = ATELIER_VIEWPORT_THEME,
	onPickRay,
	cadmataArtifact = null,
	cadmataLayers,
	selectedCadmataIds = new Set(),
	onCadmataSelect = () => undefined,
}: AetherisViewportProps) {
	return (
		<Canvas
			style={{ display: "block", width: "100%", height: "100%", background: theme.sceneBackground }}
			orthographic
			shadows={theme.shadowStyle.enabled}
			camera={{
				position: [...theme.cameraPresentation.position],
				zoom: theme.cameraPresentation.zoom,
				near: -10000,
				far: 10000,
			}}
			gl={{ alpha: false, antialias: true }}
			onCreated={({ gl }) => {
				if (new URLSearchParams(window.location.search).has("perf")) {
					publishPerformance(gl.domElement, {
						drawCalls: gl.info.render.calls,
						triangles: gl.info.render.triangles,
						geometries: gl.info.memory.geometries,
						textures: gl.info.memory.textures,
					});
				}
			}}
		>
			<color attach="background" args={[theme.sceneBackground]} />
			{theme.fog.enabled ? (
				<fog attach="fog" args={[theme.fog.color, theme.fog.near, theme.fog.far]} />
			) : null}
			<RendererConfiguration theme={theme} />
			<ViewportPerformanceProbe resetKey={displayScene} />
			<ambientLight intensity={theme.lights.ambient} />
			<hemisphereLight
				args={[
					theme.lights.hemisphereSky,
					theme.lights.hemisphereGround,
					theme.lights.hemisphereIntensity,
				]}
			/>
			<directionalLight
				position={[...theme.lights.keyPosition]}
				color={theme.lights.keyColor}
				intensity={theme.lights.keyIntensity}
				castShadow={theme.shadowStyle.enabled}
			/>
			<directionalLight
				position={[...theme.lights.fillPosition]}
				color={theme.lights.fillColor}
				intensity={theme.lights.fillIntensity}
			/>
			<FitCameraToScene displayScene={displayScene} />
			{showGrid ? <AdaptiveLogGrid theme={theme} /> : null}
			{showAxisGuide ? <AxisGuide theme={theme} /> : null}
			{displayScene?.renderables.flatMap((renderable) =>
				matchKind(renderable, {
					AnalyticPatch: (patch) => [
						<FaceMesh
							key={`analytic-${patch.faceId}`}
							positions={patch.previewMesh.positions}
							normals={patch.previewMesh.normals}
							indices={patch.previewMesh.indices}
							isHighlighted={
								highlightedFaceIds?.has(patch.faceId) ?? highlightedFaceId === patch.faceId
							}
							theme={theme}
						/>,
					],
					MeshPatch: (patch) => [
						<FaceMesh
							key={`mesh-${patch.faceId}`}
							positions={patch.mesh.positions}
							normals={patch.mesh.normals}
							indices={patch.mesh.indices}
							isHighlighted={
								highlightedFaceIds?.has(patch.faceId) ?? highlightedFaceId === patch.faceId
							}
							theme={theme}
						/>,
					],
					WirePatch: (patch) =>
						patch.wires.map((wire) => (
							<EdgeLine
								key={`wire-${patch.faceId}-${wire.edgeId}`}
								points={wire.points}
								isHighlighted={
									(highlightedFaceIds?.has(patch.faceId) ?? highlightedFaceId === patch.faceId) ||
									(highlightedEdgeIds?.has(wire.edgeId) ?? highlightedEdgeId === wire.edgeId)
								}
								theme={theme}
							/>
						)),
					DiagnosticPatch: () => [],
				}),
			)}
			{cadmataLayers ? (
				<CadmataOverlay
					artifact={cadmataArtifact}
					layers={cadmataLayers}
					selectedIds={selectedCadmataIds}
					onSelect={onCadmataSelect}
				/>
			) : null}
			<PickRayCapture onPickRay={onPickRay} />
			<OrbitControls makeDefault enablePan enableZoom />
		</Canvas>
	);
}
