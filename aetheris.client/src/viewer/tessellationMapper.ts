import type { TessellationResponseDto } from '../api/aetherisApi';

export interface RenderFacePatch {
    faceId: number;
    positions: Float32Array;
    normals: Float32Array;
    indices: Uint32Array;
}

export interface RenderEdgePolyline {
    edgeId: number;
    points: Float32Array;
}

export interface RenderSceneData {
    faces: RenderFacePatch[];
    edges: RenderEdgePolyline[];
}

export function mapTessellationToRenderData(tessellation: TessellationResponseDto): RenderSceneData {
    return {
        faces: tessellation.facePatches.map((patch) => ({
            faceId: patch.faceId,
            positions: new Float32Array(patch.positions.flatMap((position) => [position.x, position.y, position.z])),
            normals: new Float32Array(patch.normals.flatMap((normal) => [normal.x, normal.y, normal.z])),
            indices: new Uint32Array(patch.triangleIndices),
        })),
        edges: tessellation.edgePolylines
            .filter((polyline) => polyline.points.length >= 2)
            .map((polyline) => ({
                edgeId: polyline.edgeId,
                points: new Float32Array(polyline.points.flatMap((point) => [point.x, point.y, point.z])),
            })),
    };
}
