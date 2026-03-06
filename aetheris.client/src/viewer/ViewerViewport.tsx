import { Line, OrbitControls, Text } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { BufferAttribute, BufferGeometry, DoubleSide, MeshStandardMaterial, OrthographicCamera, Raycaster, Vector2, Vector3 } from 'three';
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

type GridDebugMode = 'normal' | 'fixed-single-layer' | 'no-stroke-style' | 'giant-procedural';

interface GridDebugConfig {
    enabled: boolean;
    mode: GridDebugMode;
    oneShotLog: boolean;
}

const GRID_DEBUG_DEFAULTS: GridDebugConfig = {
    enabled: false,
    mode: 'normal',
    oneShotLog: true,
};

function getGridDebugConfig(): GridDebugConfig {
    if (typeof window === 'undefined') {
        return GRID_DEBUG_DEFAULTS;
    }

    const params = new URLSearchParams(window.location.search);
    const enabled = params.get('gridDebug') === '1';
    const modeParam = params.get('gridDebugMode');
    const mode = modeParam === 'fixed-single-layer'
        || modeParam === 'no-stroke-style'
        || modeParam === 'giant-procedural'
        ? modeParam
        : 'normal';
    const oneShotLog = params.get('gridDebugLog') !== 'continuous';

    return {
        enabled,
        mode,
        oneShotLog,
    };
}

function DraftingGrid() {
    const { camera, size } = useThree();
    const gridDebug = useMemo(() => getGridDebugConfig(), []);
    const didEmitDebugLog = useRef(false);
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

    const { minorLines, majorLines, debugOverlay, debugSnapshot } = useMemo(() => {
        const orthographicCamera = camera as OrthographicCamera;
        const zoom = Math.max(cameraSnapshot.zoom, 0.0001);
        const worldHeight = size.height / zoom;
        const worldWidth = size.width / zoom;
        const worldSpan = Math.max(worldWidth, worldHeight);
        const gridSelection = selectLogarithmicGridScales(worldSpan, VIEWPORT_THEME.gridTargetCellCount);
        const centerX = cameraSnapshot.x;
        const centerZ = cameraSnapshot.z;
        const extentScale = gridDebug.mode === 'fixed-single-layer' ? 20 : VIEWPORT_THEME.gridExtentScale;
        const extentX = worldWidth * extentScale;
        const extentZ = worldHeight * extentScale;
        const layerLineCounts = {
            primary: { x: 0, z: 0 },
            secondary: { x: 0, z: 0 },
        };

        const minor: ReactNode[] = [];
        const major: ReactNode[] = [];
        const pushGridLayerLines = (spacing: number, weight: number, layerPrefix: 'primary' | 'secondary') => {
            if (weight <= 0.001) {
                return;
            }

            const xStart = Math.floor((centerX - extentX) / spacing);
            const xEnd = Math.ceil((centerX + extentX) / spacing);
            const zStart = Math.floor((centerZ - extentZ) / spacing);
            const zEnd = Math.ceil((centerZ + extentZ) / spacing);

            layerLineCounts[layerPrefix].x = xEnd - xStart + 1;
            layerLineCounts[layerPrefix].z = zEnd - zStart + 1;

            for (let xIndex = xStart; xIndex <= xEnd; xIndex += 1) {
                const x = xIndex * spacing;
                const points: [[number, number, number], [number, number, number]] = [
                    [x, VIEWPORT_THEME.gridYOffset, centerZ - extentZ],
                    [x, VIEWPORT_THEME.gridYOffset, centerZ + extentZ],
                ];
                const isMajor = xIndex % VIEWPORT_THEME.gridMajorStep === 0;
                const target = isMajor ? major : minor;
                const linePrefix = isMajor ? 'major' : 'minor';
                const alphaWeight = Math.min(Math.max(weight, 0), 1);

                if (gridDebug.mode !== 'no-stroke-style') {
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
                }
                target.push(
                    <Line
                        key={`${layerPrefix}-${linePrefix}-core-x-${xIndex}`}
                        points={points}
                        color={gridDebug.mode === 'no-stroke-style'
                            ? (isMajor ? '#ff5f5f' : '#ffd166')
                            : (isMajor ? VIEWPORT_THEME.gridMajorCoreColor : VIEWPORT_THEME.gridMinorCoreColor)}
                        transparent
                        opacity={gridDebug.mode === 'no-stroke-style' ? 0.85 : (isMajor ? VIEWPORT_THEME.gridMajorCoreOpacity : VIEWPORT_THEME.gridMinorCoreOpacity) * alphaWeight}
                        lineWidth={gridDebug.mode === 'no-stroke-style' ? 1.2 : (isMajor ? VIEWPORT_THEME.gridMajorCoreWidth : VIEWPORT_THEME.gridMinorCoreWidth)}
                    />,
                );
            }

            for (let zIndex = zStart; zIndex <= zEnd; zIndex += 1) {
                const z = zIndex * spacing;
                const points: [[number, number, number], [number, number, number]] = [
                    [centerX - extentX, VIEWPORT_THEME.gridYOffset, z],
                    [centerX + extentX, VIEWPORT_THEME.gridYOffset, z],
                ];
                const isMajor = zIndex % VIEWPORT_THEME.gridMajorStep === 0;
                const target = isMajor ? major : minor;
                const linePrefix = isMajor ? 'major' : 'minor';
                const alphaWeight = Math.min(Math.max(weight, 0), 1);

                if (gridDebug.mode !== 'no-stroke-style') {
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
                }
                target.push(
                    <Line
                        key={`${layerPrefix}-${linePrefix}-core-z-${zIndex}`}
                        points={points}
                        color={gridDebug.mode === 'no-stroke-style'
                            ? (isMajor ? '#ff5f5f' : '#ffd166')
                            : (isMajor ? VIEWPORT_THEME.gridMajorCoreColor : VIEWPORT_THEME.gridMinorCoreColor)}
                        transparent
                        opacity={gridDebug.mode === 'no-stroke-style' ? 0.85 : (isMajor ? VIEWPORT_THEME.gridMajorCoreOpacity : VIEWPORT_THEME.gridMinorCoreOpacity) * alphaWeight}
                        lineWidth={gridDebug.mode === 'no-stroke-style' ? 1.2 : (isMajor ? VIEWPORT_THEME.gridMajorCoreWidth : VIEWPORT_THEME.gridMinorCoreWidth)}
                    />,
                );
            }
        };

        const fixedSingleLayer = gridDebug.mode === 'fixed-single-layer';
        pushGridLayerLines(gridSelection.primarySpacing, fixedSingleLayer ? 1 : gridSelection.primaryWeight, 'primary');
        pushGridLayerLines(gridSelection.secondarySpacing, fixedSingleLayer ? 0 : gridSelection.secondaryWeight, 'secondary');

        const frustumCorners: [number, number][] = [
            [-1, -1],
            [1, -1],
            [1, 1],
            [-1, 1],
        ];

        const planeHitPoints = frustumCorners.map(([x, y]) => {
            const nearPoint = new Vector3(x, y, -1).unproject(orthographicCamera);
            const farPoint = new Vector3(x, y, 1).unproject(orthographicCamera);
            const direction = farPoint.clone().sub(nearPoint).normalize();
            const yDelta = VIEWPORT_THEME.gridYOffset - nearPoint.y;
            const denominator = direction.y;
            const intersects = Math.abs(denominator) > 1e-8;
            const t = intersects ? yDelta / denominator : Number.NaN;
            const point = intersects && t >= 0
                ? nearPoint.clone().add(direction.multiplyScalar(t))
                : null;

            return point;
        });

        const boundsCorners: [number, number, number][] = [
            [centerX - extentX, VIEWPORT_THEME.gridYOffset, centerZ - extentZ],
            [centerX + extentX, VIEWPORT_THEME.gridYOffset, centerZ - extentZ],
            [centerX + extentX, VIEWPORT_THEME.gridYOffset, centerZ + extentZ],
            [centerX - extentX, VIEWPORT_THEME.gridYOffset, centerZ + extentZ],
            [centerX - extentX, VIEWPORT_THEME.gridYOffset, centerZ - extentZ],
        ];

        const debugOverlay = gridDebug.enabled
            ? (
                <group>
                    <Line points={boundsCorners} color="#00e6ff" lineWidth={2.2} transparent opacity={0.95} />
                    {planeHitPoints.map((point, index) => (
                        point
                            ? <mesh key={`grid-hit-${index}`} position={[point.x, VIEWPORT_THEME.gridYOffset + 0.001, point.z]}><sphereGeometry args={[0.06, 8, 8]} /><meshBasicMaterial color={['#ff2d95', '#14f1ff', '#ffb703', '#8aff80'][index]} /></mesh>
                            : null
                    ))}
                </group>
            )
            : null;

        const debugSnapshot = {
            viewport: { width: size.width, height: size.height },
            camera: {
                position: { x: camera.position.x, y: camera.position.y, z: camera.position.z },
                quaternion: {
                    x: orthographicCamera.quaternion.x,
                    y: orthographicCamera.quaternion.y,
                    z: orthographicCamera.quaternion.z,
                    w: orthographicCamera.quaternion.w,
                },
                zoom,
                orthographicSize: {
                    worldWidth,
                    worldHeight,
                },
            },
            frustumPlaneHits: planeHitPoints.map((point) => (point ? { x: point.x, y: point.y, z: point.z } : null)),
            computedGridBounds: {
                center: { x: centerX, z: centerZ },
                extents: { x: extentX, z: extentZ },
            },
            selectedScales: {
                primarySpacing: gridSelection.primarySpacing,
                secondarySpacing: gridSelection.secondarySpacing,
                primaryWeight: fixedSingleLayer ? 1 : gridSelection.primaryWeight,
                secondaryWeight: fixedSingleLayer ? 0 : gridSelection.secondaryWeight,
                exponent: gridSelection.exponent,
                blend: fixedSingleLayer ? 0 : gridSelection.blend,
            },
            generatedLineCounts: {
                primary: {
                    x: layerLineCounts.primary.x,
                    z: layerLineCounts.primary.z,
                    total: layerLineCounts.primary.x + layerLineCounts.primary.z,
                },
                secondary: {
                    x: layerLineCounts.secondary.x,
                    z: layerLineCounts.secondary.z,
                    total: layerLineCounts.secondary.x + layerLineCounts.secondary.z,
                },
            },
            mode: gridDebug.mode,
        };

        return { minorLines: minor, majorLines: major, debugOverlay, debugSnapshot };
    }, [cameraSnapshot, camera, gridDebug.enabled, gridDebug.mode, size.height, size.width]);

    useEffect(() => {
        if (!gridDebug.enabled) {
            return;
        }

        if (gridDebug.oneShotLog && didEmitDebugLog.current) {
            return;
        }

        console.info('[grid-debug-snapshot]', debugSnapshot);
        if (gridDebug.oneShotLog) {
            didEmitDebugLog.current = true;
        }
    }, [debugSnapshot, gridDebug.enabled, gridDebug.oneShotLog]);

    if (gridDebug.enabled && gridDebug.mode === 'giant-procedural') {
        return (
            <group>
                <gridHelper args={[100000, 2000, '#ff5f5f', '#ffe29a']} position={[0, VIEWPORT_THEME.gridYOffset, 0]} />
                {debugOverlay}
            </group>
        );
    }

    return (
        <group>
            {minorLines}
            {majorLines}
            {debugOverlay}
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
