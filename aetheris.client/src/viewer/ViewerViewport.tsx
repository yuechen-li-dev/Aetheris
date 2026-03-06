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
    coverageMode: 'corner-intersection' | 'grazing-fallback';
    cameraPlaneAlignment: number;
    grazingThresholds: {
        enterFallback: number;
        exitFallback: number;
    };
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
}

const GRAZING_ALIGNMENT_ENTER_THRESHOLD = 0.075;
const GRAZING_ALIGNMENT_EXIT_THRESHOLD = 0.11;
const GRAZING_FALLBACK_SPAN_MULTIPLIER = 3.25;

function parseGridDebugFlag(): boolean {
    if (typeof window === 'undefined') {
        return false;
    }

    const debugParam = new URLSearchParams(window.location.search).get('gridDebug');
    return debugParam === '1' || debugParam === 'true';
}


function parseGridDepthTestFlag(): boolean {
    if (typeof window === 'undefined') {
        return true;
    }

    const param = new URLSearchParams(window.location.search).get('gridDepthTest');
    if (param === null) {
        return true;
    }

    return !(param === '0' || param === 'false');
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
    const gridDepthTestEnabled = useMemo(() => parseGridDepthTestFlag(), []);
    const debugSnapshotLoggedRef = useRef<string | null>(null);
    const [coverageMode, setCoverageMode] = useState<'corner-intersection' | 'grazing-fallback'>('corner-intersection');
    const [cameraSnapshot, setCameraSnapshot] = useState(() => {
        const orthographicCamera = camera as OrthographicCamera;
        const forward = orthographicCamera.getWorldDirection(new Vector3()).normalize();
        return {
            x: camera.position.x,
            y: camera.position.y,
            z: camera.position.z,
            zoom: orthographicCamera.zoom ?? 1,
            forwardY: forward.y,
        };
    });

    const gridLineRenderProps = useMemo(() => ({ depthTest: gridDepthTestEnabled }), [gridDepthTestEnabled]);

    useFrame(() => {
        const orthographicCamera = camera as OrthographicCamera;
        const forward = orthographicCamera.getWorldDirection(new Vector3()).normalize();
        const alignment = Math.abs(forward.y);

        setCoverageMode((previousMode) => {
            if (previousMode === 'corner-intersection' && alignment <= GRAZING_ALIGNMENT_ENTER_THRESHOLD) {
                return 'grazing-fallback';
            }

            if (previousMode === 'grazing-fallback' && alignment >= GRAZING_ALIGNMENT_EXIT_THRESHOLD) {
                return 'corner-intersection';
            }

            return previousMode;
        });

        setCameraSnapshot((previousSnapshot) => {
            const nextSnapshot = {
                x: camera.position.x,
                y: camera.position.y,
                z: camera.position.z,
                zoom: orthographicCamera.zoom ?? 1,
                forwardY: forward.y,
            };
            const changed = Math.abs(nextSnapshot.x - previousSnapshot.x) > 0.01
                || Math.abs(nextSnapshot.y - previousSnapshot.y) > 0.01
                || Math.abs(nextSnapshot.z - previousSnapshot.z) > 0.01
                || Math.abs(nextSnapshot.zoom - previousSnapshot.zoom) > 0.01
                || Math.abs(nextSnapshot.forwardY - previousSnapshot.forwardY) > 0.0025;

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

        const cameraPlaneAlignment = Math.abs(cameraForward.y);
        const unstableCornerIntersection = hitPoints.length !== 4;
        const activeCoverageMode: 'corner-intersection' | 'grazing-fallback' = unstableCornerIntersection
            ? 'grazing-fallback'
            : coverageMode;

        const fallbackUsed = activeCoverageMode === 'grazing-fallback';
        let fallbackReason: string | null = null;
        let fallbackOriginalBounds: GridAuditSnapshot['fallbackOriginalBounds'] = null;

        if (hitPoints.length > 0) {
            fallbackOriginalBounds = {
                minX: Math.min(...hitPoints.map((point) => point.x)),
                maxX: Math.max(...hitPoints.map((point) => point.x)),
                minZ: Math.min(...hitPoints.map((point) => point.z)),
                maxZ: Math.max(...hitPoints.map((point) => point.z)),
            };
        }

        if (!fallbackUsed && hitPoints.length === 4) {
            minX = Math.min(...hitPoints.map((point) => point.x));
            maxX = Math.max(...hitPoints.map((point) => point.x));
            minZ = Math.min(...hitPoints.map((point) => point.z));
            maxZ = Math.max(...hitPoints.map((point) => point.z));
        } else {
            const cameraHalfWidth = (orthographicCamera.right - orthographicCamera.left) / (2 * Math.max(orthographicCamera.zoom ?? 1, 0.0001));
            const cameraHalfHeight = (orthographicCamera.top - orthographicCamera.bottom) / (2 * Math.max(orthographicCamera.zoom ?? 1, 0.0001));
            const fallbackHalfSpan = Math.max(cameraHalfWidth, cameraHalfHeight, 1) * GRAZING_FALLBACK_SPAN_MULTIPLIER;
            const fallbackCenterX = camera.position.x;
            const fallbackCenterZ = camera.position.z;

            minX = fallbackCenterX - fallbackHalfSpan;
            maxX = fallbackCenterX + fallbackHalfSpan;
            minZ = fallbackCenterZ - fallbackHalfSpan;
            maxZ = fallbackCenterZ + fallbackHalfSpan;

            if (cameraPlaneAlignment <= GRAZING_ALIGNMENT_ENTER_THRESHOLD) {
                fallbackReason = `cameraForward·planeNormal=${cameraForward.y.toFixed(4)} (abs=${cameraPlaneAlignment.toFixed(4)}) <= enter threshold ${GRAZING_ALIGNMENT_ENTER_THRESHOLD}.`;
            } else if (unstableCornerIntersection) {
                fallbackReason = `Expected 4 corner-plane hits but received ${hitPoints.length}.`;
            } else {
                fallbackReason = `Maintaining grazing-angle fallback until abs(cameraForward·planeNormal) reaches ${GRAZING_ALIGNMENT_EXIT_THRESHOLD}.`;
            }
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
                const alphaWeight = Math.min(Math.max(weight, 0), 1);

                target.push(
                    <Line
                        key={`${layerPrefix}-${linePrefix}-fade-x-${xIndex}`}
                        points={points}
                        color={isMajor ? VIEWPORT_THEME.gridMajorFadeColor : VIEWPORT_THEME.gridMinorFadeColor}
                        transparent
                        opacity={(isMajor ? VIEWPORT_THEME.gridMajorFadeOpacity : VIEWPORT_THEME.gridMinorFadeOpacity) * alphaWeight}
                        lineWidth={isMajor ? VIEWPORT_THEME.gridMajorFadeWidth : VIEWPORT_THEME.gridMinorFadeWidth}
                        {...gridLineRenderProps}
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
                        {...gridLineRenderProps}
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
                        {...gridLineRenderProps}
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
                        {...gridLineRenderProps}
                    />,
                );
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
                coverageMode: activeCoverageMode,
                cameraPlaneAlignment,
                grazingThresholds: {
                    enterFallback: GRAZING_ALIGNMENT_ENTER_THRESHOLD,
                    exitFallback: GRAZING_ALIGNMENT_EXIT_THRESHOLD,
                },
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
            },
            bounds: {
                minX: expandedMinX,
                maxX: expandedMaxX,
                minZ: expandedMinZ,
                maxZ: expandedMaxZ,
            },
        };
    }, [cameraSnapshot, camera, coverageMode, gridLineRenderProps]);

    useEffect(() => {
        if (!gridDebugEnabled) {
            return;
        }

        const debugSignature = `${auditSnapshot.coverageMode}:${auditSnapshot.cameraPlaneAlignment.toFixed(4)}:${auditSnapshot.computedBounds.minX.toFixed(2)}:${auditSnapshot.computedBounds.maxX.toFixed(2)}:${auditSnapshot.computedBounds.minZ.toFixed(2)}:${auditSnapshot.computedBounds.maxZ.toFixed(2)}`;
        if (debugSnapshotLoggedRef.current === debugSignature) {
            return;
        }
        debugSnapshotLoggedRef.current = debugSignature;
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
            depthTestEnabled: gridDepthTestEnabled,
            coverageMode: auditSnapshot.coverageMode,
            cameraPlaneAlignment: auditSnapshot.cameraPlaneAlignment,
            grazingThresholds: auditSnapshot.grazingThresholds,
            fallbackUsed: auditSnapshot.fallbackUsed,
            fallbackReason: auditSnapshot.fallbackReason,
            fallbackOriginalBounds: auditSnapshot.fallbackOriginalBounds,
            minimumSpanClampApplied: auditSnapshot.minimumSpanClampApplied,
            transformSpace: auditSnapshot.transformSpace,
            layerEnvelopes: auditSnapshot.layerEnvelopes,
        });
        // eslint-disable-next-line no-console
        console.info('[grid-debug] coverage-mode', {
            mode: auditSnapshot.coverageMode,
            alignmentAbsDot: auditSnapshot.cameraPlaneAlignment,
            thresholds: auditSnapshot.grazingThresholds,
        });

        if (auditSnapshot.fallbackUsed) {
            // eslint-disable-next-line no-console
            console.warn('[grid-debug] fallback override engaged', {
                reason: auditSnapshot.fallbackReason,
                before: auditSnapshot.fallbackOriginalBounds,
                after: auditSnapshot.computedBounds,
                mode: auditSnapshot.coverageMode,
            });
        }
    }, [auditSnapshot, cornerDiagnostics, gridDebugEnabled, gridDepthTestEnabled]);

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
                                {...gridLineRenderProps}
                            />
                        );
                    });
            });
    }, [gridDebugEnabled, layerEnvelopes, gridLineRenderProps]);

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
                            {...gridLineRenderProps}
                        />
                        <Line
                            points={[[hit.x, VIEWPORT_THEME.gridYOffset, hit.z - markerSize], [hit.x, VIEWPORT_THEME.gridYOffset, hit.z + markerSize]]}
                            color={diagnostic.color}
                            lineWidth={3}
                            {...gridLineRenderProps}
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
    }, [cornerDiagnostics, gridDebugEnabled, gridLineRenderProps]);

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
                {...gridLineRenderProps}
            />
        );
    }, [bounds.maxX, bounds.maxZ, bounds.minX, bounds.minZ, gridDebugEnabled, gridLineRenderProps]);

    return (
        <group>
            {minorLines}
            {majorLines}
            {debugMarkers}
            {debugBoundsOverlay}
            {generationEnvelopeMarkers}
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
