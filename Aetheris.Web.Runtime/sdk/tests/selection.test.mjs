import assert from 'node:assert/strict';
import test from 'node:test';
import { ModelSession } from '../src/index.js';

test('display-only face identity never becomes a Firmament selector', () => {
  const session = new ModelSession({}, {
    mesh: {
      definitions: [{ id: 'definition', ranges: [{ startTriangle: 0, triangleCount: 2,
        faceId: 'face:5', semanticEntityId: 'body', sourceAddressability: 'RuntimeOnly',
        selector: null, selectorReason: 'No authored topology mapping' }] }],
      occurrences: [{ id: 'occurrence', definitionId: 'definition', semanticEntityId: 'body' }],
    },
  });
  const selection = session.describeSelection('definition', 1, 'occurrence');
  assert.equal(selection.faceId, 'face:5');
  assert.equal(selection.selector, null);
  assert.equal(selection.sourceAddressability, 'RuntimeOnly');
  assert.equal(selection.selectorReason, 'No authored topology mapping');
  assert.equal(session.describeSelection('definition', 2, 'occurrence'), null);
  assert.deepEqual(session.selectorCandidates(), []);
});

test('construction-owned face identity reaches picking and reverse source lookup', () => {
  const source = { source: 'box.firmament', line: 3, column: 5, start: 32, length: 8 };
  const range = { startTriangle: 0, triangleCount: 2, faceId: 'face:2', semanticEntityId: 'Body',
    semanticTopologyId: 'Body.face(+Z)', topologyKind: 'Face', originFeature: 'Body',
    sourceAddressability: 'DerivedStable', selector: 'face(+Z)', source, buildRevision: 4 };
  const session = new ModelSession({}, { revision: 4, mesh: {
    definitions: [{ id: 'definition', ranges: [range] }],
    occurrences: [{ id: 'occurrence', definitionId: 'definition', semanticEntityId: 'Body' }],
  } });
  const picked = session.describeSelection('definition', 1, 'occurrence');
  assert.equal(picked.semanticTopologyId, 'Body.face(+Z)');
  assert.equal(picked.selector, 'face(+Z)');
  assert.equal(picked.sourceAddressability, 'DerivedStable');
  assert.equal(picked.sourceAddressable, true);
  assert.deepEqual(picked.source, source);
  assert.equal(picked.buildRevision, 4);
  assert.deepEqual(session.selectionForSemanticId('Body.face(+Z)')[0].faceId, 'face:2');
  assert.equal(session.selectionForSourceSymbol('Body').length, 1);
  assert.equal(session.geometrySourceMap()[0].semanticKey, 'Body.face(+Z)');
  assert.equal(session.geometrySourceMap()[0].source.start, 32);
  assert.deepEqual(session.selectorCandidates().map(item => item.selector), ['face(+Z)']);
});

test('BRep edges preserve construction metadata without inventing source selectors', () => {
  const source = { source: 'box.firmament', line: 2, column: 3, start: 18, length: 42 };
  const session = new ModelSession({}, { revision: 2, mesh: {
    definitions: [{ id: 'definition', ranges: [], edges: [
      { edgeId: 'edge:7', points: [[0, 0, 0], [1, 0, 0]], closed: false, semanticEntityId: 'Body', semanticTopologyId: 'Body.edge(TopFront)', sourceAddressability: 'DerivedStable', selector: null, selectorReason: 'No qualified edge selector', source },
      { edgeId: 'edge:8', points: [[0, 0, 0], [0, 1, 0]], closed: false, sourceAddressability: 'RuntimeOnly', selector: null },
    ] }],
    occurrences: [{ id: 'occurrence', definitionId: 'definition', semanticEntityId: 'Body' }],
  } });
  const stable = session.describeEdgeSelection('definition', 'edge:7', 'occurrence');
  assert.equal(stable.semanticTopologyId, 'Body.edge(TopFront)');
  assert.deepEqual(stable.source, source);
  assert.equal(stable.sourceAddressable, true);
  assert.equal(stable.selector, null);
  const runtime = session.describeEdgeSelection('definition', 'edge:8', 'occurrence');
  assert.equal(runtime.semanticTopologyId, null);
  assert.equal(runtime.sourceAddressability, 'RuntimeOnly');
  assert.equal(runtime.selector, null);
  assert.equal(session.geometrySourceMap().filter(item => item.topologyKind === 'Edge').length, 2);
});

test('a feature face keeps its owner inside a body occurrence', () => {
  const source = { source: 'hole.firmament', line: 4, column: 2, start: 65, length: 94 };
  const session = new ModelSession({}, { mesh: {
    definitions: [{ id: 'definition', ranges: [{ startTriangle: 0, triangleCount: 1, faceId: 'face:7', semanticEntityId: 'Body.H',
      semanticTopologyId: 'material:hole:Body.H:wall', sourceAddressability: 'DerivedStable', selector: 'face(H.Wall)', source }] }],
    occurrences: [{ id: 'occurrence', definitionId: 'definition', semanticEntityId: 'Body' }],
  } });
  const picked = session.describeSelection('definition', 0, 'occurrence');
  assert.equal(picked.semanticEntityId, 'Body.H');
  assert.equal(picked.sourceAddressable, true);
  assert.equal(picked.selector, 'face(H.Wall)');
  assert.deepEqual(picked.source, source);
  assert.equal(session.selectionForEntity('Body.H').ranges.length, 1);
  assert.deepEqual(session.selectorCandidates().map(item => item.selector), ['face(H.Wall)']);
});
