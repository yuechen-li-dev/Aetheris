import assert from 'node:assert/strict';
import test from 'node:test';
import { ModelSession } from '../src/index.js';
import { normalizeMesh, reviveBinary, transferables } from '../src/runtime.js';

function binary(positions, indices) {
  const bytes = new Uint8Array(positions.length * 8 + indices.length * 4);
  new Float64Array(bytes.buffer, 0, positions.length).set(positions);
  new Int32Array(bytes.buffer, positions.length * 8, indices.length).set(indices);
  return bytes;
}

function definition(id, positionsAt, indicesAt) {
  return { id, identity: id, ranges: [], positions: { $binary: 'f64', byteOffset: positionsAt, length: 3 },
    normals: { $binary: 'f64', byteOffset: positionsAt, length: 3 }, indices: { $binary: 'i32', byteOffset: indicesAt, length: 3 } };
}

test('binary placeholders become exact typed-array views of one buffer', () => {
  const bytes = binary([0.1, -2.5, 1e-300], [0, 1, 2]);
  const result = reviveBinary({ model: { mesh: { definitions: [definition('a', 0, 24)], occurrences: [] } } }, bytes);
  const [revived] = normalizeMesh(result).model.mesh.definitions;
  assert.deepEqual([...revived.positions], [0.1, -2.5, 1e-300]);
  assert.ok(revived.indices instanceof Uint32Array);
  assert.deepEqual([...revived.indices], [0, 1, 2]);
  assert.equal(revived.positions.buffer, bytes.buffer);
  assert.equal(revived.indices.buffer, bytes.buffer, 'indices are reinterpreted, not copied');
});

test('transferables lists each shared buffer once and skips retained definitions', () => {
  const bytes = binary([1, 2, 3], [0, 1, 2]);
  const result = normalizeMesh(reviveBinary({ model: { mesh: { definitions: [definition('a', 0, 24), definition('b', 0, 24),
    { id: 'c', identity: 'c', retained: true }] } } }, bytes));
  assert.deepEqual(transferables(result), [bytes.buffer]);
});

test('a rebuild substitutes retained definitions with the ones the session holds', () => {
  const held = { id: 'kept', identity: 'kept', positions: new Float64Array([1, 2, 3]), normals: new Float64Array(3), indices: new Uint32Array([0, 1, 2]), ranges: [] };
  const fresh = { id: 'new', identity: 'new', positions: new Float64Array(3), normals: new Float64Array(3), indices: new Uint32Array([0, 1, 2]), ranges: [] };
  const session = new ModelSession({}, { revision: 1, mesh: { definitions: [held], occurrences: [] } });
  session.apply({ revision: 2, mesh: { definitions: [{ id: 'kept', identity: 'kept', retained: true }, fresh], occurrences: [] } });
  assert.equal(session.revision, 2);
  assert.equal(session.mesh.definitions[0], held);
  assert.equal(session.mesh.definitions[1], fresh);
});

test('a retained definition the session does not hold is an error, not a silent hole', () => {
  const session = new ModelSession({}, { revision: 1, mesh: { definitions: [], occurrences: [] } });
  assert.throws(() => session.apply({ revision: 2, mesh: { definitions: [{ id: 'missing', identity: 'missing', retained: true }], occurrences: [] } }),
    error => error.code === 'retained-definition-missing');
});


test('Motion sampling uses the cached runtime graph without replacing shared meshes', async () => {
  const requests = [];
  const mesh = { definitions: [], occurrences: [] };
  const transport = { async request(request) {
    requests.push(request);
    return { revision: 3, motion: request.motion, timeSeconds: request.timeSeconds, state: { Shaft: 450 }, transforms: [] };
  } };
  const session = new ModelSession(transport, { id: 'model-1', revision: 3, mesh, motions: [] });
  const pose = await session.sampleMotion('Cycle', 1.25);
  assert.equal(pose.state.Shaft, 450);
  assert.equal(session.mesh, mesh);
  assert.equal(requests.length, 1);
  assert.equal(requests[0].operation, 'sampleMotion');
  transport.request = async () => ({ revision: 2 });
  await assert.rejects(session.sampleMotion('Cycle', 1), error => error.code === 'motion-revision-mismatch');
});
