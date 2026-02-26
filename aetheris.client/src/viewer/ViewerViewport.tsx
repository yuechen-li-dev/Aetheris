import { Line, OrbitControls } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import { useThree } from '@react-three/fiber';
import { useEffect, useMemo } from 'react';
import { BufferAttribute, BufferGeometry, DoubleSide, MeshStandardMaterial, Raycaster, Vector2 } from 'three';
import type { RenderSceneData } from './tessellationMapper';

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
        () => new MeshStandardMaterial({ color: isHighlighted ? '#f59e0b' : '#8db8ff', metalness: 0.1, roughness: 0.75, side: DoubleSide }),
        [isHighlighted],
    );

    return <mesh geometry={geometry} material={material} />;
}

function EdgeLine({ points, isHighlighted }: { points: Float32Array; isHighlighted: boolean }) {
    const linePoints = useMemo(() => {
        const vertices: [number, number, number][] = [];

        for (let i = 0; i < points.length; i += 3) {
            vertices.push([points[i], points[i + 1], points[i + 2]]);
        }

        return vertices;
    }, [points]);

    return <Line points={linePoints} color={isHighlighted ? '#dc2626' : '#111827'} lineWidth={isHighlighted ? 3 : 1} />;
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
    return (
        <div className="viewport-shell">
            <Canvas camera={{ position: [4, 4, 4], fov: 50 }}>
                <color attach="background" args={['#f8fafc']} />
                <ambientLight intensity={0.5} />
                <directionalLight position={[4, 8, 3]} intensity={1.2} />
                <gridHelper args={[20, 20, '#cbd5e1', '#e2e8f0']} />
                <axesHelper args={[2]} />
                {sceneData?.faces.map((face) => (
                    <FaceMesh
                        key={`face-${face.faceId}`}
                        positions={face.positions}
                        normals={face.normals}
                        indices={face.indices}
                        isHighlighted={highlightedFaceId === face.faceId}
                    />
                ))}
                {sceneData?.edges.map((edge) => (
                    <EdgeLine
                        key={`edge-${edge.edgeId}`}
                        points={edge.points}
                        isHighlighted={highlightedEdgeId === edge.edgeId}
                    />
                ))}
                <PickRayCapture onPickRay={onPickRay} />
                <OrbitControls makeDefault enablePan enableZoom />
            </Canvas>
        </div>
    );
}
