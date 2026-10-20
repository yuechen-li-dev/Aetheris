import assert from 'node:assert/strict';
import test from 'node:test';
import { ModelSession } from '../src/index.js';

const snapshot = { id: 'session', revision: 7, mesh: { definitions: [], occurrences: [] } };
const request = { mode: 'planes', first: { occurrenceId: 'part', faceId: 2, rayOrigin: [0,0,30], rayDirection: [0,0,-1] } };

test('measure uses the serialized session queue and current build revision', async () => {
  const calls = [];
  const transport = { request: async value => { calls.push(value); return { revision: 7, measurement: { distanceMm: 6 } }; } };
  const model = new ModelSession(transport, snapshot);
  assert.equal((await model.measure(request)).distanceMm, 6);
  assert.deepEqual(calls, [{ operation: 'measure', sessionId: 'session', buildRevision: 7, measurement: request }]);
});
test('measure refuses a result from another build and preserves backend errors', async () => {
  const model = new ModelSession({ request: async () => ({ revision: 6, measurement: { distanceMm: 6 } }) }, snapshot);
  await assert.rejects(model.measure(request), error => error.code === 'measurement-stale-revision');
  model.transport.request = async () => { throw new Error('unsupported trim'); };
  await assert.rejects(model.measure(request), /unsupported trim/);
});
