import { Line, OrbitControls, Text } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Box3, BufferAttribute, BufferGeometry, Color, DoubleSide, Euler, Group, MeshStandardMaterial, Object3D, OrthographicCamera, Quaternion, Raycaster, Vector2, Vector3 } from 'three';
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

interface GridLayerEnvelope {
    layer: string;
    spacing: number;
    weight: number;
    xStart: number;
    xEnd: number;
    zStart: number;
    zEnd: number;
    xLineCount: number;
    zLineCount: number;
    firstVerticalLine: [[number, number, number], [number, number, number]] | null;
    lastVerticalLine: [[number, number, number], [number, number, number]] | null;
    firstHorizontalLine: [[number, number, number], [number, number, number]] | null;
    lastHorizontalLine: [[number, number, number], [number, number, number]] | null;
    skippedByWeight: boolean;
}

interface GridAuditSnapshot {
    computedBounds: {
        minX: number;
        maxX: number;
        minZ: number;
        maxZ: number;
        centerX: number;
        centerZ: number;
        spanX: number;
        spanZ: number;
    };
    baseSpan: number;
    margin: number;
    worldSpan: number;
    gridSelection: {
        primarySpacing: number;
        secondarySpacing: number;
        primaryWeight: number;
        secondaryWeight: number;
    };
    fallbackUsed: boolean;
    fallbackReason: string | null;
    fallbackOriginalBounds: {
        minX: number;
        maxX: number;
        minZ: number;
        maxZ: number;
    } | null;
    minimumSpanClampApplied: boolean;
    layerEnvelopes: GridLayerEnvelope[];
    transformSpace: {
        gridGroupLocalSpace: string;
        parentTransformAssumption: string;
    };
    renderMode: GridDebugMode;
    sketchFadeImplementation: {
        axis: 'cross-width-only';
        detail: string;
    };
    generatedLineCounts: {
        minor: number;
        major: number;
        total: number;
    };
}

type GridDebugMode = 'sketch-grid' | 'plain-grid' | 'sketch-grid-exaggerated';


interface GridSceneObjectAudit {
    uuid: string;
    name: string;
    kind: string;
    type: string;
    childCount: number;
    visible: boolean;
    worldPosition: { x: number; y: number; z: number };
    worldScale: { x: number; y: number; z: number };
    worldEulerDeg: { x: number; y: number; z: number };
    worldBounds: { min: { x: number; y: number; z: number }; max: { x: number; y: number; z: number } } | null;
}

function toRoundedVector(vector: Vector3): { x: number; y: number; z: number } {
    return {
        x: Number(vector.x.toFixed(6)),
        y: Number(vector.y.toFixed(6)),
        z: Number(vector.z.toFixed(6)),
    };
}

function auditGridSceneObject(object: Object3D): GridSceneObjectAudit {
    object.updateWorldMatrix(true, false);
    const worldPosition = new Vector3();
    const worldScale = new Vector3();
    const worldQuaternion = object.getWorldQuaternion(new Quaternion());
    const worldEuler = new Euler().setFromQuaternion(worldQuaternion, 'XYZ');
    object.getWorldPosition(worldPosition);
    object.getWorldScale(worldScale);

    const bounds = new Box3().setFromObject(object);
    const hasFiniteBounds = Number.isFinite(bounds.min.x)
        && Number.isFinite(bounds.min.y)
        && Number.isFinite(bounds.min.z)
        && Number.isFinite(bounds.max.x)
        && Number.isFinite(bounds.max.y)
        && Number.isFinite(bounds.max.z);

    return {
        uuid: object.uuid,
        name: object.name || '(unnamed)',
        kind: String(object.userData.gridNodeKind ?? 'unclassified'),
        type: object.type,
        childCount: object.children.length,
        visible: object.visible,
        worldPosition: toRoundedVector(worldPosition),
        worldScale: toRoundedVector(worldScale),
        worldEulerDeg: {
            x: Number((worldEuler.x * 180 / Math.PI).toFixed(3)),
            y: Number((worldEuler.y * 180 / Math.PI).toFixed(3)),
            z: Number((worldEuler.z * 180 / Math.PI).toFixed(3)),
        },
        worldBounds: hasFiniteBounds
            ? {
                min: toRoundedVector(bounds.min),
                max: toRoundedVector(bounds.max),
            }
            : null,
    };
}

function parseGridDebugFlag(): boolean {
    if (typeof window === 'undefined') {
        return false;
    }

    const debugParam = new URLSearchParams(window.location.search).get('gridDebug');
    return debugParam === '1' || debugParam === 'true';
}

function parseGridDebugMode(): GridDebugMode {
    if (typeof window === 'undefined') {
        return 'sketch-grid';
    }

    const modeParam = new URLSearchParams(window.location.search).get('gridDebugMode');
    if (modeParam === 'plain-grid') {
        return 'plain-grid';
    }

    if (modeParam === 'sketch-grid-exaggerated') {
        return 'sketch-grid-exaggerated';
    }

    return 'sketch-grid';
}


function parseGridForceNoCullingFlag(): boolean {
    if (typeof window === 'undefined') {
        return false;
    }

    const value = new URLSearchParams(window.location.search).get('gridForceNoCulling');
    return value === '1' || value === 'true';
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
    const { camera, scene } = useThree();
    const gridDebugEnabled = useMemo(() => parseGridDebugFlag(), []);
    const gridDebugMode = useMemo(() => parseGridDebugMode(), []);
    const gridForceNoCulling = useMemo(() => parseGridForceNoCullingFlag(), []);
    const debugSnapshotLoggedRef = useRef(false);
    const gridSceneAuditLoggedRef = useRef(false);
    const gridGroupRef = useRef<Group>(null);
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
        layerEnvelopes,
        auditSnapshot,
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

        let fallbackUsed = false;
        let fallbackReason: string | null = null;
        let fallbackOriginalBounds: GridAuditSnapshot['fallbackOriginalBounds'] = null;

        if (hitPoints.length === 4) {
            minX = Math.min(...hitPoints.map((point) => point.x));
            maxX = Math.max(...hitPoints.map((point) => point.x));
            minZ = Math.min(...hitPoints.map((point) => point.z));
            maxZ = Math.max(...hitPoints.map((point) => point.z));
        } else {
            fallbackUsed = true;
            fallbackReason = `Expected 4 corner-plane hits but received ${hitPoints.length}.`;
            if (hitPoints.length > 0) {
                fallbackOriginalBounds = {
                    minX: Math.min(...hitPoints.map((point) => point.x)),
                    maxX: Math.max(...hitPoints.map((point) => point.x)),
                    minZ: Math.min(...hitPoints.map((point) => point.z)),
                    maxZ: Math.max(...hitPoints.map((point) => point.z)),
                };
            }
            const fallbackExtent = Math.max(10 / Math.max(orthographicCamera.zoom ?? 1, 0.0001), 1);
            minX = camera.position.x - fallbackExtent;
            maxX = camera.position.x + fallbackExtent;
            minZ = camera.position.z - fallbackExtent;
            maxZ = camera.position.z + fallbackExtent;
        }

        const unclampedBaseSpan = Math.max(maxX - minX, maxZ - minZ);
        const baseSpan = Math.max(unclampedBaseSpan, 1);
        const minimumSpanClampApplied = baseSpan !== unclampedBaseSpan;
        const margin = baseSpan * 0.2;
        const expandedMinX = minX - margin;
        const expandedMaxX = maxX + margin;
        const expandedMinZ = minZ - margin;
        const expandedMaxZ = maxZ + margin;
        const worldSpan = Math.max(expandedMaxX - expandedMinX, expandedMaxZ - expandedMinZ);
        const gridSelection = selectLogarithmicGridScales(worldSpan, VIEWPORT_THEME.gridTargetCellCount);

        const minor: ReactNode[] = [];
        const major: ReactNode[] = [];
        const buildGridLineLayers = (isMajor: boolean, alphaWeight: number) => {
            const clampedAlphaWeight = Math.min(Math.max(alphaWeight, 0), 1);
            const plainLayer = [{
                variant: 'plain',
                color: isMajor ? VIEWPORT_THEME.gridMajorCoreColor : VIEWPORT_THEME.gridMinorCoreColor,
                opacity: clampedAlphaWeight,
                lineWidth: isMajor ? VIEWPORT_THEME.gridMajorCoreWidth : VIEWPORT_THEME.gridMinorCoreWidth,
            }] as const;

            if (gridDebugMode === 'plain-grid') {
                return plainLayer;
            }

            const fadeOpacity = gridDebugMode === 'sketch-grid-exaggerated'
                ? 0.78
                : (isMajor ? VIEWPORT_THEME.gridMajorFadeOpacity : VIEWPORT_THEME.gridMinorFadeOpacity) * clampedAlphaWeight;
            const fadeWidth = gridDebugMode === 'sketch-grid-exaggerated'
                ? (isMajor ? VIEWPORT_THEME.gridMajorFadeWidth : VIEWPORT_THEME.gridMinorFadeWidth) * 2.2
                : isMajor ? VIEWPORT_THEME.gridMajorFadeWidth : VIEWPORT_THEME.gridMinorFadeWidth;

            const coreOpacity = gridDebugMode === 'sketch-grid-exaggerated'
                ? 0.32
                : (isMajor ? VIEWPORT_THEME.gridMajorCoreOpacity : VIEWPORT_THEME.gridMinorCoreOpacity) * clampedAlphaWeight;

            return [
                {
                    variant: 'fade',
                    color: isMajor ? VIEWPORT_THEME.gridMajorFadeColor : VIEWPORT_THEME.gridMinorFadeColor,
                    opacity: fadeOpacity,
                    lineWidth: fadeWidth,
                },
                {
                    variant: 'core',
                    color: isMajor ? VIEWPORT_THEME.gridMajorCoreColor : VIEWPORT_THEME.gridMinorCoreColor,
                    opacity: coreOpacity,
                    lineWidth: isMajor ? VIEWPORT_THEME.gridMajorCoreWidth : VIEWPORT_THEME.gridMinorCoreWidth,
                },
            ] as const;
        };
        const envelopes: GridLayerEnvelope[] = [];
        const pushGridLayerLines = (spacing: number, weight: number, layerPrefix: string) => {
            const layerEnvelope: GridLayerEnvelope = {
                layer: layerPrefix,
                spacing,
                weight,
                xStart: 0,
                xEnd: 0,
                zStart: 0,
                zEnd: 0,
                xLineCount: 0,
                zLineCount: 0,
                firstVerticalLine: null,
                lastVerticalLine: null,
                firstHorizontalLine: null,
                lastHorizontalLine: null,
                skippedByWeight: weight <= 0.001,
            };

            if (weight <= 0.001) {
                envelopes.push(layerEnvelope);
                return;
            }

            const xStart = Math.floor(expandedMinX / spacing);
            const xEnd = Math.ceil(expandedMaxX / spacing);
            const zStart = Math.floor(expandedMinZ / spacing);
            const zEnd = Math.ceil(expandedMaxZ / spacing);

            layerEnvelope.xStart = xStart;
            layerEnvelope.xEnd = xEnd;
            layerEnvelope.zStart = zStart;
            layerEnvelope.zEnd = zEnd;
            layerEnvelope.xLineCount = xEnd - xStart + 1;
            layerEnvelope.zLineCount = zEnd - zStart + 1;
            layerEnvelope.firstVerticalLine = [[xStart * spacing, VIEWPORT_THEME.gridYOffset, expandedMinZ], [xStart * spacing, VIEWPORT_THEME.gridYOffset, expandedMaxZ]];
            layerEnvelope.lastVerticalLine = [[xEnd * spacing, VIEWPORT_THEME.gridYOffset, expandedMinZ], [xEnd * spacing, VIEWPORT_THEME.gridYOffset, expandedMaxZ]];
            layerEnvelope.firstHorizontalLine = [[expandedMinX, VIEWPORT_THEME.gridYOffset, zStart * spacing], [expandedMaxX, VIEWPORT_THEME.gridYOffset, zStart * spacing]];
            layerEnvelope.lastHorizontalLine = [[expandedMinX, VIEWPORT_THEME.gridYOffset, zEnd * spacing], [expandedMaxX, VIEWPORT_THEME.gridYOffset, zEnd * spacing]];

            for (let xIndex = xStart; xIndex <= xEnd; xIndex += 1) {
                const x = xIndex * spacing;
                const points: [[number, number, number], [number, number, number]] = [
                    [x, VIEWPORT_THEME.gridYOffset, expandedMinZ],
                    [x, VIEWPORT_THEME.gridYOffset, expandedMaxZ],
                ];
                const isMajor = xIndex % VIEWPORT_THEME.gridMajorStep === 0;
                const target = isMajor ? major : minor;
                const linePrefix = isMajor ? 'major' : 'minor';

                buildGridLineLayers(isMajor, weight).forEach((layer) => {
                    target.push(
                        <Line
                            key={`${layerPrefix}-${linePrefix}-${layer.variant}-x-${xIndex}`}
                            points={points}
                            color={layer.color}
                            transparent
                            opacity={layer.opacity}
                            lineWidth={layer.lineWidth}
                            userData={{
                                gridNodeKind: 'generated-line',
                                gridAxis: 'x',
                                gridLayer: layerPrefix,
                                gridClass: linePrefix,
                                gridVariant: layer.variant,
                                expectedBounds: {
                                    minX: points[0][0],
                                    maxX: points[1][0],
                                    minY: points[0][1],
                                    maxY: points[1][1],
                                    minZ: Math.min(points[0][2], points[1][2]),
                                    maxZ: Math.max(points[0][2], points[1][2]),
                                },
                            }}
                        />,
                    );
                });
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

                buildGridLineLayers(isMajor, weight).forEach((layer) => {
                    target.push(
                        <Line
                            key={`${layerPrefix}-${linePrefix}-${layer.variant}-z-${zIndex}`}
                            points={points}
                            color={layer.color}
                            transparent
                            opacity={layer.opacity}
                            lineWidth={layer.lineWidth}
                            userData={{
                                gridNodeKind: 'generated-line',
                                gridAxis: 'z',
                                gridLayer: layerPrefix,
                                gridClass: linePrefix,
                                gridVariant: layer.variant,
                                expectedBounds: {
                                    minX: Math.min(points[0][0], points[1][0]),
                                    maxX: Math.max(points[0][0], points[1][0]),
                                    minY: points[0][1],
                                    maxY: points[1][1],
                                    minZ: points[0][2],
                                    maxZ: points[1][2],
                                },
                            }}
                        />,
                    );
                });
            }

            envelopes.push(layerEnvelope);
        };

        pushGridLayerLines(gridSelection.primarySpacing, gridSelection.primaryWeight, 'primary');
        pushGridLayerLines(gridSelection.secondarySpacing, gridSelection.secondaryWeight, 'secondary');

        return {
            minorLines: minor,
            majorLines: major,
            cornerDiagnostics: diagnostics,
            layerEnvelopes: envelopes,
            auditSnapshot: {
                computedBounds: {
                    minX: expandedMinX,
                    maxX: expandedMaxX,
                    minZ: expandedMinZ,
                    maxZ: expandedMaxZ,
                    centerX: (expandedMinX + expandedMaxX) * 0.5,
                    centerZ: (expandedMinZ + expandedMaxZ) * 0.5,
                    spanX: expandedMaxX - expandedMinX,
                    spanZ: expandedMaxZ - expandedMinZ,
                },
                baseSpan,
                margin,
                worldSpan,
                gridSelection: {
                    primarySpacing: gridSelection.primarySpacing,
                    secondarySpacing: gridSelection.secondarySpacing,
                    primaryWeight: gridSelection.primaryWeight,
                    secondaryWeight: gridSelection.secondaryWeight,
                },
                fallbackUsed,
                fallbackReason,
                fallbackOriginalBounds,
                minimumSpanClampApplied,
                layerEnvelopes: envelopes,
                transformSpace: {
                    gridGroupLocalSpace: 'All DraftingGrid lines are authored in parent/world axes (x,z on y=gridYOffset).',
                    parentTransformAssumption: 'DraftingGrid is mounted without transform; local coordinates match world-space viewer coordinates.',
                },
                renderMode: gridDebugMode,
                sketchFadeImplementation: {
                    axis: 'cross-width-only',
                    detail: 'Sketch mode renders each infinite-grid segment as two coincident Line layers (wide low-alpha halo + narrow core) with no endpoint, segment-UV, world-distance, or clip-space fade.',
                },
                generatedLineCounts: {
                    minor: minor.length,
                    major: major.length,
                    total: minor.length + major.length,
                },
            },
            bounds: {
                minX: expandedMinX,
                maxX: expandedMaxX,
                minZ: expandedMinZ,
                maxZ: expandedMaxZ,
            },
        };
    }, [cameraSnapshot, camera, gridDebugMode]);



    useEffect(() => {
        gridSceneAuditLoggedRef.current = false;
    }, [cameraSnapshot, gridDebugMode]);


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
        // eslint-disable-next-line no-console
        console.log('[grid-debug] generation-audit', {
            bounds: auditSnapshot.computedBounds,
            baseSpan: auditSnapshot.baseSpan,
            margin: auditSnapshot.margin,
            worldSpan: auditSnapshot.worldSpan,
            selectedSpacing: auditSnapshot.gridSelection,
            fallbackUsed: auditSnapshot.fallbackUsed,
            fallbackReason: auditSnapshot.fallbackReason,
            fallbackOriginalBounds: auditSnapshot.fallbackOriginalBounds,
            minimumSpanClampApplied: auditSnapshot.minimumSpanClampApplied,
            transformSpace: auditSnapshot.transformSpace,
            renderMode: auditSnapshot.renderMode,
            gridForceNoCulling,
            sketchFadeImplementation: auditSnapshot.sketchFadeImplementation,
            layerEnvelopes: auditSnapshot.layerEnvelopes,
        });
        if (auditSnapshot.fallbackUsed) {
            // eslint-disable-next-line no-console
            console.warn('[grid-debug] fallback override engaged', {
                reason: auditSnapshot.fallbackReason,
                before: auditSnapshot.fallbackOriginalBounds,
                after: auditSnapshot.computedBounds,
            });
        }
    }, [auditSnapshot, cornerDiagnostics, gridDebugEnabled, gridForceNoCulling]);

    const generationEnvelopeMarkers = useMemo(() => {
        if (!gridDebugEnabled) {
            return null;
        }

        return layerEnvelopes
            .filter((layer) => !layer.skippedByWeight)
            .flatMap((layer) => {
                const color = layer.layer === 'primary' ? '#14b8a6' : '#f97316';
                const lines = [
                    { key: 'first-vertical', points: layer.firstVerticalLine },
                    { key: 'last-vertical', points: layer.lastVerticalLine },
                    { key: 'first-horizontal', points: layer.firstHorizontalLine },
                    { key: 'last-horizontal', points: layer.lastHorizontalLine },
                ] as const;

                return lines
                    .map((line) => {
                        if (line.points === null) {
                            return null;
                        }

                        return (
                            <Line
                                key={`grid-envelope-${layer.layer}-${line.key}`}
                                points={[
                                    [line.points[0][0], line.points[0][1] + 0.02, line.points[0][2]],
                                    [line.points[1][0], line.points[1][1] + 0.02, line.points[1][2]],
                                ]}
                                color={color}
                                lineWidth={4}
                                userData={{ gridNodeKind: 'generation-envelope', gridLayer: layer.layer, gridEdge: line.key }}
                            />
                        );
                    });
            });
    }, [gridDebugEnabled, layerEnvelopes]);

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
                            userData={{ gridNodeKind: 'corner-marker', markerCorner: diagnostic.label, markerAxis: 'x' }}
                        />
                        <Line
                            points={[[hit.x, VIEWPORT_THEME.gridYOffset, hit.z - markerSize], [hit.x, VIEWPORT_THEME.gridYOffset, hit.z + markerSize]]}
                            color={diagnostic.color}
                            lineWidth={3}
                            userData={{ gridNodeKind: 'corner-marker', markerCorner: diagnostic.label, markerAxis: 'z' }}
                        />
                        <Text
                            position={[hit.x, VIEWPORT_THEME.gridYOffset + 0.03, hit.z]}
                            color={diagnostic.color}
                            fontSize={0.15}
                            anchorX="center"
                            anchorY="bottom"
                            userData={{ gridNodeKind: 'corner-label', markerCorner: diagnostic.label }}
                        >
                            {diagnostic.label}
                        </Text>
                    </group>
                );
            });
    }, [cornerDiagnostics, gridDebugEnabled]);





    useEffect(() => {
        if (!gridDebugEnabled || !gridGroupRef.current) {
            return;
        }

        const gridNodes: Object3D[] = [];
        gridGroupRef.current.traverse((object) => {
            if (object.userData.gridNodeKind) {
                gridNodes.push(object);
            }
        });

        const noCullingApplied = gridForceNoCulling;
        let cullingDisabledCount = 0;
        let geometryObjectCount = 0;
        let missingBoundingBoxBefore = 0;
        let missingBoundingSphereBefore = 0;
        let missingBoundingBoxAfter = 0;
        let missingBoundingSphereAfter = 0;
        let nonFiniteBoundingBoxAfter = 0;
        let nonFiniteBoundingSphereAfter = 0;

        gridNodes.forEach((node) => {
            if (noCullingApplied && 'frustumCulled' in node) {
                (node as Object3D & { frustumCulled?: boolean }).frustumCulled = false;
                cullingDisabledCount += 1;
            }

            const candidate = node as Object3D & { geometry?: {
                boundingBox?: Box3 | null;
                boundingSphere?: { center: Vector3; radius: number } | null;
                computeBoundingBox?: () => void;
                computeBoundingSphere?: () => void;
            } };

            if (!candidate.geometry) {
                return;
            }

            geometryObjectCount += 1;

            if (!candidate.geometry.boundingBox) {
                missingBoundingBoxBefore += 1;
            }
            if (!candidate.geometry.boundingSphere) {
                missingBoundingSphereBefore += 1;
            }

            candidate.geometry.computeBoundingBox?.();
            candidate.geometry.computeBoundingSphere?.();

            const boundingBox = candidate.geometry.boundingBox ?? null;
            const boundingSphere = candidate.geometry.boundingSphere ?? null;

            if (!boundingBox) {
                missingBoundingBoxAfter += 1;
            } else {
                const min = boundingBox.min;
                const max = boundingBox.max;
                const finite = Number.isFinite(min.x)
                    && Number.isFinite(min.y)
                    && Number.isFinite(min.z)
                    && Number.isFinite(max.x)
                    && Number.isFinite(max.y)
                    && Number.isFinite(max.z);
                if (!finite) {
                    nonFiniteBoundingBoxAfter += 1;
                }
            }

            if (!boundingSphere) {
                missingBoundingSphereAfter += 1;
            } else {
                const finite = Number.isFinite(boundingSphere.center.x)
                    && Number.isFinite(boundingSphere.center.y)
                    && Number.isFinite(boundingSphere.center.z)
                    && Number.isFinite(boundingSphere.radius);
                if (!finite) {
                    nonFiniteBoundingSphereAfter += 1;
                }
            }
        });

        // eslint-disable-next-line no-console
        console.log('[grid-debug] culling-bounds-audit', {
            gridForceNoCulling,
            noCullingApplied,
            taggedGridNodeCount: gridNodes.length,
            cullingDisabledCount,
            geometryObjectCount,
            missingBoundingBoxBefore,
            missingBoundingSphereBefore,
            missingBoundingBoxAfter,
            missingBoundingSphereAfter,
            nonFiniteBoundingBoxAfter,
            nonFiniteBoundingSphereAfter,
            boundsRecomputeApplied: true,
            note: 'Set ?gridDebug=1&gridForceNoCulling=1 to force all tagged grid nodes frustumCulled=false for diagnosis.',
        });
    }, [auditSnapshot.generatedLineCounts.total, gridDebugEnabled, gridForceNoCulling, gridDebugMode]);

    useEffect(() => {
        if (!gridDebugEnabled || gridSceneAuditLoggedRef.current) {
            return;
        }

        if (!gridGroupRef.current) {
            return;
        }

        gridSceneAuditLoggedRef.current = true;
        scene.updateMatrixWorld(true);

        const gridObjects: Object3D[] = [];
        scene.traverse((object) => {
            if (object.userData.gridNodeKind) {
                gridObjects.push(object);
            }
        });

        const rootObjects = gridObjects.filter((object) => object.userData.gridNodeKind === 'grid-root');
        const renderedGeneratedLineCount = gridObjects.filter((object) => object.userData.gridNodeKind === 'generated-line').length;
        const expectedGeneratedLineCount = auditSnapshot.generatedLineCounts.total;
        const generationObjectMatchesRenderedObject = rootObjects.length === 1
            && rootObjects[0].uuid === gridGroupRef.current.uuid
            && renderedGeneratedLineCount === expectedGeneratedLineCount;

        // eslint-disable-next-line no-console
        console.log('[grid-debug] scene-graph-audit', {
            generationRootUuid: gridGroupRef.current.uuid,
            renderedRootUuids: rootObjects.map((object) => object.uuid),
            renderedRootCount: rootObjects.length,
            expectedGeneratedLineCount,
            renderedGeneratedLineCount,
            generationObjectMatchesRenderedObject,
            staleOrDuplicateGridRootDetected: rootObjects.length !== 1,
            renderedGridObjects: gridObjects.map((object) => auditGridSceneObject(object)),
        });
    }, [auditSnapshot.generatedLineCounts.total, gridDebugEnabled, scene]);

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
                userData={{ gridNodeKind: 'debug-bounds' }}
            />
        );
    }, [bounds.maxX, bounds.maxZ, bounds.minX, bounds.minZ, gridDebugEnabled]);

    return (
        <group
            ref={gridGroupRef}
            name="DraftingGridRoot"
            userData={{ gridNodeKind: 'grid-root', renderMode: gridDebugMode, gridForceNoCulling }}
        >
            <group name="DraftingGridMinorLines" userData={{ gridNodeKind: 'generated-line-container', gridClass: 'minor' }}>
                {minorLines}
            </group>
            <group name="DraftingGridMajorLines" userData={{ gridNodeKind: 'generated-line-container', gridClass: 'major' }}>
                {majorLines}
            </group>
            <group name="DraftingGridCornerMarkers" userData={{ gridNodeKind: 'corner-marker-container' }}>
                {debugMarkers}
            </group>
            {debugBoundsOverlay}
            <group name="DraftingGridGenerationEnvelope" userData={{ gridNodeKind: 'generation-envelope-container' }}>
                {generationEnvelopeMarkers}
            </group>
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
