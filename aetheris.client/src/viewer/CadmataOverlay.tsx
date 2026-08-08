/* eslint-disable react-refresh/only-export-components -- layer contracts and the overlay intentionally share one small module. */
import { Line } from '@react-three/drei';
import { useMemo } from 'react';
import { DoubleSide, Vector3 } from 'three';
import type { CadmataEntity, CadmataLayer, CadmataVisualizationArtifact } from './conceptVisualization';

export const CADMATA_PALETTE = { concept: '#6ea6b8', profile: '#4db6ac', compose: '#b48748', selection: '#ffbf47', ancestor: '#77a7d9', diagnostic: '#e05d5d' } as const;
export type CadmataLayerVisibility = Record<CadmataLayer, boolean>;
export const DEFAULT_CADMATA_LAYERS: CadmataLayerVisibility = { material: true, brepEdges: true, conceptPoints: true, conceptAxes: true, conceptRegions: true, profileGuides: true, profileLoops: true, composeRegions: true, selections: true, diagnostics: true };

function colorFor(entity: CadmataEntity, selected: boolean) { if (selected) return CADMATA_PALETTE.selection; if (entity.layer === 'profileGuides' || entity.layer === 'profileLoops') return CADMATA_PALETTE.profile; if (entity.layer === 'composeRegions') return CADMATA_PALETTE.compose; if (entity.layer === 'diagnostics') return CADMATA_PALETTE.diagnostic; return CADMATA_PALETTE.concept; }
function OverlayEntity({ entity, selected, onSelect }: { entity: CadmataEntity; selected: boolean; onSelect: (id: string) => void }) {
  const geometry = entity.geometry; const color = colorFor(entity, selected);
  const points = useMemo(() => geometry?.type === 'polyline' ? geometry.points.map((p) => [p.x, p.y, p.z] as [number, number, number]) : [], [geometry]);
  if (!geometry) return null;
  if (geometry.type === 'point') return <mesh position={[geometry.point.x, geometry.point.y, geometry.point.z]} onClick={(event) => { event.stopPropagation(); onSelect(entity.stableId); }}><sphereGeometry args={[1.8, 12, 8]} /><meshBasicMaterial color={color} depthTest={false} /></mesh>;
  if (geometry.type === 'circle') {
    const count = 48; const points = Array.from({ length: count + 1 }, (_, i) => { const a = (i / count) * Math.PI * 2; return [geometry.center.x + Math.cos(a) * geometry.radius, geometry.center.y + Math.sin(a) * geometry.radius, geometry.center.z] as [number, number, number]; });
    return <Line points={points} color={color} lineWidth={selected ? 3.5 : 1.5} onClick={(event) => { event.stopPropagation(); onSelect(entity.stableId); }} />;
  }
  if (geometry.type === 'plane') return <mesh position={[geometry.origin.x, geometry.origin.y, geometry.origin.z]} onClick={(event) => { event.stopPropagation(); onSelect(entity.stableId); }}><planeGeometry args={[new Vector3(geometry.u.x, geometry.u.y, geometry.u.z).length() * 2, new Vector3(geometry.v.x, geometry.v.y, geometry.v.z).length() * 2]} /><meshBasicMaterial color={color} transparent opacity={0.12} side={DoubleSide} depthWrite={false} /></mesh>;
  return <Line points={geometry.closed ? [...points, points[0]] : points} color={color} lineWidth={selected ? 4 : entity.layer === 'profileLoops' ? 2.5 : 1.25} onClick={(event) => { event.stopPropagation(); onSelect(entity.stableId); }} />;
}
export function CadmataOverlay({ artifact, layers, selectedIds, onSelect }: { artifact: CadmataVisualizationArtifact | null; layers: CadmataLayerVisibility; selectedIds: Set<string>; onSelect: (id: string) => void }) {
  if (!artifact) return null;
  return <group name="CadmataCompilerOverlays">{artifact.entities.filter((entity) => layers[entity.layer]).map((entity) => <OverlayEntity key={entity.stableId} entity={entity} selected={selectedIds.has(entity.stableId)} onSelect={onSelect} />)}</group>;
}
