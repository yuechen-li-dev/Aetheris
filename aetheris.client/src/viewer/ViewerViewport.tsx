import { Line, OrbitControls, Text } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import { useThree } from '@react-three/fiber';
import { useEffect, useMemo } from 'react';
import { BufferAttribute, BufferGeometry, DoubleSide, MeshStandardMaterial, Raycaster, Vector2 } from 'three';
import type { RenderSceneData } from './tessellationMapper';

const VIEWPORT_THEME = {
    surfaceColor: '#969ba1',
    edgeColor: '#2e2e2e',
    edgeWidth: 1.35,
    gridMinorColor: '#d9dee4',
    gridMajorColor: '#c7cdd3',
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
                <gridHelper args={[20, 20, VIEWPORT_THEME.gridMajorColor, VIEWPORT_THEME.gridMinorColor]} />
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
