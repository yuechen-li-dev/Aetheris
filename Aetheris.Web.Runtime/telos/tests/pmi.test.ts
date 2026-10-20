import test from 'node:test';
import assert from 'node:assert/strict';
import { pmiGroup, pmiLines, pmiMeshes, pmiBodyMaterial, disposePmiGroup } from '../dist/pmi.js';

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
  disposePmiGroup(group);
  assert.equal(group.children.length, 0);
  assert.deepEqual(annotations[0].lines[0][0], [1,2,3]);
});

test('surface witnesses use compiler geometry, depth testing and release every mesh resource', () => {
  const target = { kind: 'PlanePatch', positions: [0,0,0,1,0,0,0,1,0], normals: [0,0,1,0,0,1,0,0,1], indices: [0,1,2], lines: [[[0,0,0],[1,0,0],[0,1,0],[0,0,0]]] };
  const annotations = [{ id: 'pmi:A', lines: [[[0,0,0],[0,0,1]]], target }];
  const mesh = pmiMeshes(annotations)[0];
  assert.equal(mesh.definition.positions, target.positions);
  assert.equal(mesh.pickable, false);
  assert.equal(mesh.depthMode, 'depth-biased');
  assert.ok(pmiLines(annotations).filter(l => l.id.includes(':target:')).every(l => l.depthMode === 'depth-biased'));
  const group = pmiGroup(annotations);
  let geometries = 0, materials = 0;
  for (const child of group.children as (import('three').Mesh | import('three').LineSegments)[]) {
    child.geometry.addEventListener('dispose', () => geometries++);
    const material = child.material as import('three').Material;
    material.addEventListener('dispose', () => materials++);
  }
  const count = group.children.length;
  disposePmiGroup(group);
  assert.equal(geometries, count); assert.equal(materials, count);
  assert.deepEqual(target.positions, [0,0,0,1,0,0,0,1,0]);
  const material = { opacity: 1 };
  assert.equal(pmiBodyMaterial(material, true).opacity, .78);
  assert.equal(material.opacity, 1);
  assert.equal(pmiBodyMaterial(material, true, true), material);
  assert.equal(pmiBodyMaterial(material, false), material);
});
