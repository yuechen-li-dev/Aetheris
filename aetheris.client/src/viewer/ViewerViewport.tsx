import { Line, OrbitControls } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import { useMemo } from 'react';
import { BufferAttribute, BufferGeometry, DoubleSide, MeshStandardMaterial } from 'three';
import type { RenderSceneData } from './tessellationMapper';

interface ViewerViewportProps {
    sceneData: RenderSceneData | null;
}

function FaceMesh({ positions, normals, indices }: { positions: Float32Array; normals: Float32Array; indices: Uint32Array }) {
    const geometry = useMemo(() => {
        const meshGeometry = new BufferGeometry();
        meshGeometry.setAttribute('position', new BufferAttribute(positions, 3));
        meshGeometry.setAttribute('normal', new BufferAttribute(normals, 3));
        meshGeometry.setIndex(new BufferAttribute(indices, 1));
        meshGeometry.computeBoundingSphere();
        return meshGeometry;
    }, [indices, normals, positions]);

    const material = useMemo(() => new MeshStandardMaterial({ color: '#8db8ff', metalness: 0.1, roughness: 0.75, side: DoubleSide }), []);

    return <mesh geometry={geometry} material={material} />;
}

function EdgeLine({ points }: { points: Float32Array }) {
    const linePoints = useMemo(() => {
        const vertices: [number, number, number][] = [];

        for (let i = 0; i < points.length; i += 3) {
            vertices.push([points[i], points[i + 1], points[i + 2]]);
        }

        return vertices;
    }, [points]);

    return <Line points={linePoints} color="#111827" lineWidth={1} />;
}

export function ViewerViewport({ sceneData }: ViewerViewportProps) {
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
                    />
                ))}
                {sceneData?.edges.map((edge) => (
                    <EdgeLine
                        key={`edge-${edge.edgeId}`}
                        points={edge.points}
                    />
                ))}
                <OrbitControls makeDefault enablePan enableZoom />
            </Canvas>
        </div>
    );
}
