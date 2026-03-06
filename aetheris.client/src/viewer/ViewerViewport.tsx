import { Line, OrbitControls, Text } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { BufferAttribute, BufferGeometry, Color, DoubleSide, MeshStandardMaterial, OrthographicCamera, Raycaster, Vector2, Vector3 } from 'three';
import type { RenderSceneData } from './tessellationMapper';
import { selectLogarithmicGridScales } from './logarithmicGrid';

const VIEWPORT_THEME = {
    surfaceColor: '#969ba1',
    edgeColor: '#2e2e2e',
    edgeWidth: 1.35,
    gridMinorCoreColor: '#6b6a67',
    gridMinorFadeColor: '#9a9894',
    gridMajorCoreColor: '#555450',
    gridMajorFadeColor: '#8a8782',
    gridMinorCoreOpacity: 0.34,
    gridMinorFadeOpacity: 0.2,
    gridMajorCoreOpacity: 0.5,
    gridMajorFadeOpacity: 0.28,
    gridMinorCoreWidth: 0.7,
    gridMinorFadeWidth: 2,
    gridMajorCoreWidth: 1,
    gridMajorFadeWidth: 2.8,
    gridMajorStep: 5,
    gridTargetCellCount: 14,
    gridExtentScale: 2.2,
    gridYOffset: 0.001,
    ambientIntensity: 0.12,
    directionalIntensity: 0.88,
    axisLength: 2,
    axisLineWidth: 1,
    axisXColor: '#3f6f8f',
    axisYColor: '#4d7a4d',
    axisZColor: '#8c6a2c',
    axisLabelColor: '#4a4a4a',
    axisLabelSize: 0.16,
    selectionFaceColor: '#f59e0b',
    selectionEdgeColor: '#f59e0b',
    selectionEdgeWidth: 3,
} as const;

const GRID_CORNER_DEFINITIONS = [
    { label: 'top-left', ndc: new Vector2(-1, 1), color: '#ef4444' },
    { label: 'top-right', ndc: new Vector2(1, 1), color: '#22c55e' },
    { label: 'bottom-left', ndc: new Vector2(-1, -1), color: '#3b82f6' },
    { label: 'bottom-right', ndc: new Vector2(1, -1), color: '#f59e0b' },
] as const;

interface GridCornerDiagnostic {
    label: string;
    ndc: { x: number; y: number };
    rayOrigin: Vector3;
    rayDirection: Vector3;
    hitPoint: Vector3 | null;
    intersectsPlane: boolean;
    color: string;
}

function parseGridDebugFlag(): boolean {
    if (typeof window === 'undefined') {
        return false;
    }

    const debugParam = new URLSearchParams(window.location.search).get('gridDebug');
    return debugParam === '1' || debugParam === 'true';
}

function intersectRayWithHorizontalPlane(rayOrigin: Vector3, rayDirection: Vector3, planeY: number): Vector3 | null {
    const epsilon = 1e-6;
    if (Math.abs(rayDirection.y) < epsilon) {
        return null;
    }

    const t = (planeY - rayOrigin.y) / rayDirection.y;
    if (!Number.isFinite(t) || t < 0) {
        return null;
    }

    return rayOrigin.clone().addScaledVector(rayDirection, t);
}

function DraftingGrid() {
    const { camera } = useThree();
    const gridDebugEnabled = useMemo(() => parseGridDebugFlag(), []);
    const debugSnapshotLoggedRef = useRef(false);
    const [cameraSnapshot, setCameraSnapshot] = useState({ x: camera.position.x, z: camera.position.z, zoom: (camera as OrthographicCamera).zoom ?? 1 });

    useFrame(() => {
        const orthographicCamera = camera as OrthographicCamera;
        setCameraSnapshot((previousSnapshot) => {
            const nextSnapshot = {
                x: camera.position.x,
                z: camera.position.z,
                zoom: orthographicCamera.zoom ?? 1,
            };
            const changed = Math.abs(nextSnapshot.x - previousSnapshot.x) > 0.01
                || Math.abs(nextSnapshot.z - previousSnapshot.z) > 0.01
                || Math.abs(nextSnapshot.zoom - previousSnapshot.zoom) > 0.01;

            return changed ? nextSnapshot : previousSnapshot;
        });
    });

    const {
        minorLines,
        majorLines,
        cornerDiagnostics,
        bounds,
    } = useMemo(() => {
        const orthographicCamera = camera as OrthographicCamera;
        const cameraForward = orthographicCamera.getWorldDirection(new Vector3()).normalize();
        const cornerRays = new Raycaster();

        const diagnostics: GridCornerDiagnostic[] = GRID_CORNER_DEFINITIONS.map((cornerDefinition) => {
            cornerRays.setFromCamera(cornerDefinition.ndc, orthographicCamera);
            const rayOrigin = cornerRays.ray.origin.clone();
            const rayDirection = cameraForward.clone();
            const hitPoint = intersectRayWithHorizontalPlane(rayOrigin, rayDirection, VIEWPORT_THEME.gridYOffset);
            return {
                label: cornerDefinition.label,
                ndc: { x: cornerDefinition.ndc.x, y: cornerDefinition.ndc.y },
                rayOrigin,
                rayDirection,
                hitPoint,
                intersectsPlane: hitPoint !== null,
                color: cornerDefinition.color,
            };
        });

        const hitPoints = diagnostics
            .map((diagnostic) => diagnostic.hitPoint)
            .filter((point): point is Vector3 => point !== null);

        let minX: number;
        let maxX: number;
        let minZ: number;
        let maxZ: number;

        if (hitPoints.length === 4) {
            minX = Math.min(...hitPoints.map((point) => point.x));
            maxX = Math.max(...hitPoints.map((point) => point.x));
            minZ = Math.min(...hitPoints.map((point) => point.z));
            maxZ = Math.max(...hitPoints.map((point) => point.z));
        } else {
            const fallbackExtent = Math.max(10 / Math.max(orthographicCamera.zoom ?? 1, 0.0001), 1);
            minX = camera.position.x - fallbackExtent;
            maxX = camera.position.x + fallbackExtent;
            minZ = camera.position.z - fallbackExtent;
            maxZ = camera.position.z + fallbackExtent;
        }

        const baseSpan = Math.max(maxX - minX, maxZ - minZ, 1);
        const margin = baseSpan * 0.2;
        const expandedMinX = minX - margin;
        const expandedMaxX = maxX + margin;
        const expandedMinZ = minZ - margin;
        const expandedMaxZ = maxZ + margin;
        const worldSpan = Math.max(expandedMaxX - expandedMinX, expandedMaxZ - expandedMinZ);
        const gridSelection = selectLogarithmicGridScales(worldSpan, VIEWPORT_THEME.gridTargetCellCount);

        const minor: ReactNode[] = [];
        const major: ReactNode[] = [];
        const pushGridLayerLines = (spacing: number, weight: number, layerPrefix: string) => {
            if (weight <= 0.001) {
                return;
            }

            const xStart = Math.floor(expandedMinX / spacing);
            const xEnd = Math.ceil(expandedMaxX / spacing);
            const zStart = Math.floor(expandedMinZ / spacing);
            const zEnd = Math.ceil(expandedMaxZ / spacing);

            for (let xIndex = xStart; xIndex <= xEnd; xIndex += 1) {
                const x = xIndex * spacing;
                const points: [[number, number, number], [number, number, number]] = [
                    [x, VIEWPORT_THEME.gridYOffset, expandedMinZ],
                    [x, VIEWPORT_THEME.gridYOffset, expandedMaxZ],
                ];
                const isMajor = xIndex % VIEWPORT_THEME.gridMajorStep === 0;
                const target = isMajor ? major : minor;
                const linePrefix = isMajor ? 'major' : 'minor';
                const alphaWeight = Math.min(Math.max(weight, 0), 1);

                target.push(
                    <Line
                        key={`${layerPrefix}-${linePrefix}-fade-x-${xIndex}`}
                        points={points}
                        color={isMajor ? VIEWPORT_THEME.gridMajorFadeColor : VIEWPORT_THEME.gridMinorFadeColor}
                        transparent
                        opacity={(isMajor ? VIEWPORT_THEME.gridMajorFadeOpacity : VIEWPORT_THEME.gridMinorFadeOpacity) * alphaWeight}
                        lineWidth={isMajor ? VIEWPORT_THEME.gridMajorFadeWidth : VIEWPORT_THEME.gridMinorFadeWidth}
                    />,
                );
                target.push(
                    <Line
                        key={`${layerPrefix}-${linePrefix}-core-x-${xIndex}`}
                        points={points}
                        color={isMajor ? VIEWPORT_THEME.gridMajorCoreColor : VIEWPORT_THEME.gridMinorCoreColor}
                        transparent
                        opacity={(isMajor ? VIEWPORT_THEME.gridMajorCoreOpacity : VIEWPORT_THEME.gridMinorCoreOpacity) * alphaWeight}
                        lineWidth={isMajor ? VIEWPORT_THEME.gridMajorCoreWidth : VIEWPORT_THEME.gridMinorCoreWidth}
                    />,
                );
            }

            for (let zIndex = zStart; zIndex <= zEnd; zIndex += 1) {
                const z = zIndex * spacing;
                const points: [[number, number, number], [number, number, number]] = [
                    [expandedMinX, VIEWPORT_THEME.gridYOffset, z],
                    [expandedMaxX, VIEWPORT_THEME.gridYOffset, z],
                ];
                const isMajor = zIndex % VIEWPORT_THEME.gridMajorStep === 0;
                const target = isMajor ? major : minor;
                const linePrefix = isMajor ? 'major' : 'minor';
                const alphaWeight = Math.min(Math.max(weight, 0), 1);

                target.push(
                    <Line
                        key={`${layerPrefix}-${linePrefix}-fade-z-${zIndex}`}
                        points={points}
                        color={isMajor ? VIEWPORT_THEME.gridMajorFadeColor : VIEWPORT_THEME.gridMinorFadeColor}
                        transparent
                        opacity={(isMajor ? VIEWPORT_THEME.gridMajorFadeOpacity : VIEWPORT_THEME.gridMinorFadeOpacity) * alphaWeight}
                        lineWidth={isMajor ? VIEWPORT_THEME.gridMajorFadeWidth : VIEWPORT_THEME.gridMinorFadeWidth}
                    />,
                );
                target.push(
                    <Line
                        key={`${layerPrefix}-${linePrefix}-core-z-${zIndex}`}
                        points={points}
                        color={isMajor ? VIEWPORT_THEME.gridMajorCoreColor : VIEWPORT_THEME.gridMinorCoreColor}
                        transparent
                        opacity={(isMajor ? VIEWPORT_THEME.gridMajorCoreOpacity : VIEWPORT_THEME.gridMinorCoreOpacity) * alphaWeight}
                        lineWidth={isMajor ? VIEWPORT_THEME.gridMajorCoreWidth : VIEWPORT_THEME.gridMinorCoreWidth}
                    />,
                );
            }
        };

        pushGridLayerLines(gridSelection.primarySpacing, gridSelection.primaryWeight, 'primary');
        pushGridLayerLines(gridSelection.secondarySpacing, gridSelection.secondaryWeight, 'secondary');

        return {
            minorLines: minor,
            majorLines: major,
            cornerDiagnostics: diagnostics,
            bounds: {
                minX: expandedMinX,
                maxX: expandedMaxX,
                minZ: expandedMinZ,
                maxZ: expandedMaxZ,
            },
        };
    }, [cameraSnapshot, camera]);

    useEffect(() => {
        if (!gridDebugEnabled || debugSnapshotLoggedRef.current) {
            return;
        }

        debugSnapshotLoggedRef.current = true;
        // eslint-disable-next-line no-console
        console.table(cornerDiagnostics.map((diagnostic) => ({
            corner: diagnostic.label,
            ndcX: diagnostic.ndc.x,
            ndcY: diagnostic.ndc.y,
            originX: diagnostic.rayOrigin.x,
            originY: diagnostic.rayOrigin.y,
            originZ: diagnostic.rayOrigin.z,
            directionX: diagnostic.rayDirection.x,
            directionY: diagnostic.rayDirection.y,
            directionZ: diagnostic.rayDirection.z,
            hit: diagnostic.intersectsPlane,
            hitX: diagnostic.hitPoint?.x ?? null,
            hitY: diagnostic.hitPoint?.y ?? null,
            hitZ: diagnostic.hitPoint?.z ?? null,
        })));
    }, [cornerDiagnostics, gridDebugEnabled]);

    const debugMarkers = useMemo(() => {
        if (!gridDebugEnabled) {
            return null;
        }

        return cornerDiagnostics
            .filter((diagnostic) => diagnostic.hitPoint !== null)
            .map((diagnostic) => {
                const hit = diagnostic.hitPoint as Vector3;
                const markerSize = 0.22;
                return (
                    <group key={`grid-corner-marker-${diagnostic.label}`}>
                        <Line
                            points={[[hit.x - markerSize, VIEWPORT_THEME.gridYOffset, hit.z], [hit.x + markerSize, VIEWPORT_THEME.gridYOffset, hit.z]]}
                            color={diagnostic.color}
                            lineWidth={3}
                        />
                        <Line
                            points={[[hit.x, VIEWPORT_THEME.gridYOffset, hit.z - markerSize], [hit.x, VIEWPORT_THEME.gridYOffset, hit.z + markerSize]]}
                            color={diagnostic.color}
                            lineWidth={3}
                        />
                        <Text
                            position={[hit.x, VIEWPORT_THEME.gridYOffset + 0.03, hit.z]}
                            color={diagnostic.color}
                            fontSize={0.15}
                            anchorX="center"
                            anchorY="bottom"
                        >
                            {diagnostic.label}
                        </Text>
                    </group>
                );
            });
    }, [cornerDiagnostics, gridDebugEnabled]);

    const debugBoundsOverlay = useMemo(() => {
        if (!gridDebugEnabled) {
            return null;
        }

        const outlineColor = new Color('#a855f7');
        return (
            <Line
                points={[
                    [bounds.minX, VIEWPORT_THEME.gridYOffset + 0.01, bounds.minZ],
                    [bounds.maxX, VIEWPORT_THEME.gridYOffset + 0.01, bounds.minZ],
                    [bounds.maxX, VIEWPORT_THEME.gridYOffset + 0.01, bounds.maxZ],
                    [bounds.minX, VIEWPORT_THEME.gridYOffset + 0.01, bounds.maxZ],
                    [bounds.minX, VIEWPORT_THEME.gridYOffset + 0.01, bounds.minZ],
                ]}
                color={outlineColor}
                lineWidth={2.6}
            />
        );
    }, [bounds.maxX, bounds.maxZ, bounds.minX, bounds.minZ, gridDebugEnabled]);

    return (
        <group>
            {minorLines}
            {majorLines}
            {debugMarkers}
            {debugBoundsOverlay}
        </group>
    );
}

interface ViewerViewportProps {
    sceneData: RenderSceneData | null;
    highlightedFaceId?: number | null;
    highlightedEdgeId?: number | null;
    onPickRay?: (origin: { x: number; y: number; z: number }, direction: { x: number; y: number; z: number }) => void;
}

function FaceMesh({ positions, normals, indices, isHighlighted }: { positions: Float32Array; normals: Float32Array; indices: Uint32Array; isHighlighted: boolean }) {
    const geometry = useMemo(() => {
        const meshGeometry = new BufferGeometry();
        meshGeometry.setAttribute('position', new BufferAttribute(positions, 3));
        meshGeometry.setAttribute('normal', new BufferAttribute(normals, 3));
        meshGeometry.setIndex(new BufferAttribute(indices, 1));
        meshGeometry.computeBoundingSphere();
        return meshGeometry;
    }, [indices, normals, positions]);

    const material = useMemo(
        () => new MeshStandardMaterial({
            color: isHighlighted ? VIEWPORT_THEME.selectionFaceColor : VIEWPORT_THEME.surfaceColor,
            metalness: 0,
            roughness: 0.95,
            side: DoubleSide,
        }),
        [isHighlighted],
    );

    return <mesh geometry={geometry} material={material} />;
}

function AxisGuide() {
    const axisEnd = VIEWPORT_THEME.axisLength;
    const labelOffset = 0.14;

    return (
        <group>
            <Line points={[[0, 0, 0], [axisEnd, 0, 0]]} color={VIEWPORT_THEME.axisXColor} lineWidth={VIEWPORT_THEME.axisLineWidth} />
            <Line points={[[0, 0, 0], [0, axisEnd, 0]]} color={VIEWPORT_THEME.axisYColor} lineWidth={VIEWPORT_THEME.axisLineWidth} />
            <Line points={[[0, 0, 0], [0, 0, axisEnd]]} color={VIEWPORT_THEME.axisZColor} lineWidth={VIEWPORT_THEME.axisLineWidth} />
            <Text
                position={[axisEnd + labelOffset, 0, 0]}
                fontSize={VIEWPORT_THEME.axisLabelSize}
                color={VIEWPORT_THEME.axisLabelColor}
                anchorX="left"
                anchorY="middle"
            >
                X
            </Text>
            <Text
                position={[0, axisEnd + labelOffset, 0]}
                fontSize={VIEWPORT_THEME.axisLabelSize}
                color={VIEWPORT_THEME.axisLabelColor}
                anchorX="center"
                anchorY="bottom"
            >
                Y
            </Text>
            <Text
                position={[0, 0, axisEnd + labelOffset]}
                fontSize={VIEWPORT_THEME.axisLabelSize}
                color={VIEWPORT_THEME.axisLabelColor}
                anchorX="center"
                anchorY="middle"
            >
                Z
            </Text>
        </group>
    );
}

function EdgeLine({ points, isHighlighted }: { points: Float32Array; isHighlighted: boolean }) {
    const linePoints = useMemo(() => {
        const vertices: [number, number, number][] = [];

        for (let i = 0; i < points.length; i += 3) {
            vertices.push([points[i], points[i + 1], points[i + 2]]);
        }

        return vertices;
    }, [points]);

    return (
        <Line
            points={linePoints}
            color={isHighlighted ? VIEWPORT_THEME.selectionEdgeColor : VIEWPORT_THEME.edgeColor}
            lineWidth={isHighlighted ? VIEWPORT_THEME.selectionEdgeWidth : VIEWPORT_THEME.edgeWidth}
        />
    );
}

function PickRayCapture({ onPickRay }: { onPickRay?: ViewerViewportProps['onPickRay'] }) {
    const { camera, gl } = useThree();

    useEffect(() => {
        if (!onPickRay) {
            return;
        }

        const raycaster = new Raycaster();
        const pointer = new Vector2();

        const handleClick = (event: MouseEvent) => {
            if (event.button !== 0) {
                return;
            }

            const rect = gl.domElement.getBoundingClientRect();
            pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            raycaster.setFromCamera(pointer, camera);
            onPickRay(
                {
                    x: raycaster.ray.origin.x,
                    y: raycaster.ray.origin.y,
                    z: raycaster.ray.origin.z,
                },
                {
                    x: raycaster.ray.direction.x,
                    y: raycaster.ray.direction.y,
                    z: raycaster.ray.direction.z,
                },
            );
        };

        gl.domElement.addEventListener('click', handleClick);
        return () => gl.domElement.removeEventListener('click', handleClick);
    }, [camera, gl.domElement, onPickRay]);

    return null;
}

export function ViewerViewport({ sceneData, highlightedFaceId = null, highlightedEdgeId = null, onPickRay }: ViewerViewportProps) {
    const hasInteractionEdgeHighlight = highlightedEdgeId !== null;

    return (
        <div className="viewport-canvas-frame">
            <Canvas orthographic camera={{ position: [6, 6, 6], zoom: 90, near: 0.1, far: 1000 }} gl={{ alpha: true }}>
                <ambientLight intensity={VIEWPORT_THEME.ambientIntensity} />
                <directionalLight position={[-5, 9, 6]} intensity={VIEWPORT_THEME.directionalIntensity} />
                <DraftingGrid />
                <AxisGuide />
                {sceneData?.faces.map((face) => (
                    <FaceMesh
                        key={`face-${face.faceId}`}
                        positions={face.positions}
                        normals={face.normals}
                        indices={face.indices}
                        isHighlighted={highlightedFaceId === face.faceId}
                    />
                ))}
                {hasInteractionEdgeHighlight
                    ? sceneData?.edges
                        .filter((edge) => edge.edgeId === highlightedEdgeId)
                        .map((edge) => (
                            <EdgeLine
                                key={`edge-${edge.edgeId}`}
                                points={edge.points}
                                isHighlighted
                            />
                        ))
                    : null}
                <PickRayCapture onPickRay={onPickRay} />
                <OrbitControls makeDefault enablePan enableZoom />
            </Canvas>
        </div>
    );
}
