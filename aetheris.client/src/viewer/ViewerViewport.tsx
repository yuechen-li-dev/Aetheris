import { Line, OrbitControls, Text } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { BufferAttribute, BufferGeometry, DoubleSide, MeshStandardMaterial, OrthographicCamera, Plane, Raycaster, Vector2, Vector3 } from 'three';
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
    gridCoverageMarginScale: 1.12,
    gridMinimumCoverageSize: 16,
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

function DraftingGrid() {
    const { camera, size } = useThree();
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

    const { minorLines, majorLines } = useMemo(() => {
        const orthographicCamera = camera as OrthographicCamera;
        const zoom = Math.max(orthographicCamera.zoom ?? 1, 0.0001);
        const worldHeight = size.height / zoom;
        const worldWidth = size.width / zoom;
        const worldSpan = Math.max(worldWidth, worldHeight);
        const gridSelection = selectLogarithmicGridScales(worldSpan, VIEWPORT_THEME.gridTargetCellCount);
        const gridPlane = new Plane(new Vector3(0, 1, 0), -VIEWPORT_THEME.gridYOffset);
        const corners: readonly [number, number][] = [
            [-1, -1],
            [1, -1],
            [1, 1],
            [-1, 1],
        ];
        const raycaster = new Raycaster();
        const hit = new Vector3();
        const visiblePointsOnPlane: Vector3[] = [];

        for (const [x, y] of corners) {
            raycaster.setFromCamera(new Vector2(x, y), orthographicCamera);
            if (raycaster.ray.intersectPlane(gridPlane, hit)) {
                visiblePointsOnPlane.push(hit.clone());
            }
        }

        const centerX = visiblePointsOnPlane.length > 0
            ? visiblePointsOnPlane.reduce((sum, point) => sum + point.x, 0) / visiblePointsOnPlane.length
            : camera.position.x;
        const centerZ = visiblePointsOnPlane.length > 0
            ? visiblePointsOnPlane.reduce((sum, point) => sum + point.z, 0) / visiblePointsOnPlane.length
            : camera.position.z;

        const fallbackExtent = Math.max(worldSpan * VIEWPORT_THEME.gridExtentScale, VIEWPORT_THEME.gridMinimumCoverageSize);
        const minX = visiblePointsOnPlane.length > 0 ? Math.min(...visiblePointsOnPlane.map((point) => point.x)) : centerX - fallbackExtent;
        const maxX = visiblePointsOnPlane.length > 0 ? Math.max(...visiblePointsOnPlane.map((point) => point.x)) : centerX + fallbackExtent;
        const minZ = visiblePointsOnPlane.length > 0 ? Math.min(...visiblePointsOnPlane.map((point) => point.z)) : centerZ - fallbackExtent;
        const maxZ = visiblePointsOnPlane.length > 0 ? Math.max(...visiblePointsOnPlane.map((point) => point.z)) : centerZ + fallbackExtent;
        const extentX = Math.max((maxX - minX) * 0.5 * VIEWPORT_THEME.gridCoverageMarginScale, fallbackExtent);
        const extentZ = Math.max((maxZ - minZ) * 0.5 * VIEWPORT_THEME.gridCoverageMarginScale, fallbackExtent);

        const minor: ReactNode[] = [];
        const major: ReactNode[] = [];
        const pushGridLayerLines = (spacing: number, weight: number, layerPrefix: string) => {
            if (weight <= 0.001) {
                return;
            }

            const xStart = Math.floor((centerX - extentX) / spacing);
            const xEnd = Math.ceil((centerX + extentX) / spacing);
            const zStart = Math.floor((centerZ - extentZ) / spacing);
            const zEnd = Math.ceil((centerZ + extentZ) / spacing);

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
                    [centerX - extentX, VIEWPORT_THEME.gridYOffset, z],
                    [centerX + extentX, VIEWPORT_THEME.gridYOffset, z],
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

        return { minorLines: minor, majorLines: major };
    }, [cameraSnapshot, camera, size.height, size.width]);

    return (
        <group>
            {minorLines}
            {majorLines}
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
