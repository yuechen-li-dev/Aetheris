import test from 'node:test';
import assert from 'node:assert/strict';
import { pmiGroup, pmiLines } from '../dist/pmi.js';
import { disposeConceptWireframe } from '../dist/concepts.js';

test('both display adapters retain compiler coordinates and keep PMI outside engineering picks', () => {
  const annotations = [{ id: 'pmi:1', lines: [[[1,2,3], [4,5,6], [7,8,9]]] }];
  const lines = pmiLines(annotations);
  assert.deepEqual(lines[0].points, [1,2,3,4,5,6,7,8,9]);
  assert.equal(lines[0].pickable, false);
  assert.equal(lines[0].includeInFit, true);
  assert.equal(lines[0].depthMode, 'always-on-top');
  const group = pmiGroup(annotations);
  const geometry = (group.children[0] as import('three').LineSegments).geometry;
  assert.deepEqual([...geometry.getAttribute('position').array], [1,2,3,4,5,6,4,5,6,7,8,9]);
  assert.equal((group.children[0] as import('three').LineSegments).material instanceof Object, true);
  disposeConceptWireframe(group);
  assert.equal(group.children.length, 0);
  assert.deepEqual(annotations[0].lines[0][0], [1,2,3]);
});
