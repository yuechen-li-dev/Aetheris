import type { Diagnostic, EditableProperty, ModelSession, ModelTreeNode, RebuildResult } from '@aetheris/cad';

export const root: ModelTreeNode = { id: 'model:bracket', kind: 'Model', name: 'WebBracket', children: ['box:plate', 'hole:center'], visible: true };
export const plate: ModelTreeNode = { id: 'box:plate', kind: 'Box', name: 'Plate', parentId: root.id, children: [], visible: true };
export const hole: ModelTreeNode = { id: 'hole:center', kind: 'Hole', name: 'CenterMount', parentId: root.id, children: [], visible: true };
export const width: EditableProperty = { id: 'prop:width', ownerEntityId: plate.id, name: 'Width', type: 'Length', unit: 'mm', value: 50, writable: true, source: { source: 'bracket.firmament', line: 3, column: 24, start: 40, length: 4 } };

export function fakeModel(overrides: Partial<ModelSession> = {}): ModelSession {
  const nodes = [root, plate, hole];
  const properties = [width];
  const model = {
    id: 'session:1', name: 'WebBracket', revision: 1, source: '', sourceName: 'bracket.firmament',
    tree: { rootId: root.id, nodes }, properties,
    mesh: { schema: 'aetheris/display-mesh/1', name: 'WebBracket', units: 'mm', definitions: [{ id: 'def:plate', identity: 'plate', positions: new Float64Array(), normals: new Float64Array(), indices: new Uint32Array(), ranges: [{ startTriangle: 0, triangleCount: 12, faceId: 'face:top', semanticEntityId: plate.id }] }], occurrences: [{ id: 'occ:plate', path: '/Plate', definitionId: 'def:plate', semanticEntityId: plate.id, transform: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] }] },
    diagnostics: [] as readonly Diagnostic[], changes: { changedEntityIds: [], addedEntityIds: [], removedEntityIds: [], meshChanged: false, diagnosticsChanged: false },
    entity(id: string) { return nodes.find(item => item.id === id); }, property(id: string) { return properties.find(item => item.id === id); },
    setProperty: async () => ok(), setSource: async () => ok(), rebuild: async () => ok(),
    resolveSelection: () => ({ semanticEntityId: plate.id, faceId: 'face:top', occurrenceId: 'occ:plate', definitionId: 'def:plate' }),
    selectionForEntity: (id: string) => id === plate.id ? { occurrenceIds: ['occ:plate'], ranges: [{ definitionId: 'def:plate', startTriangle: 0, triangleCount: 12, faceId: 'face:top', semanticEntityId: plate.id }] } : { occurrenceIds: [], ranges: [] },
    exportSTEP: async () => new Uint8Array([1, 2, 3]), exportSTEPBlob: async () => new Blob(), dispose: async () => {}
  } as unknown as ModelSession;
  return Object.assign(model, overrides);
}

export function ok(): RebuildResult { return { success: true, revision: 2, diagnostics: [], retainedPreviousGeometry: false }; }
