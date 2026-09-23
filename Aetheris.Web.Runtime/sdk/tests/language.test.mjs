import assert from 'node:assert/strict';
import test from 'node:test';
import { Aetheris } from '../src/index.js';

test('public language completion sends source revision and offset through runtime transport', async () => {
  const requests = [];
  const cad = new Aetheris({ request: async request => {
    requests.push(request);
    return { document: request.sourceName, revision: request.sourceRevision, context: 'Helix',
      replaceStart: request.offset - 3, replaceLength: 3, fields: [], missingRequiredFields: [] };
  } });
  const result = await cad.language.complete('Helix H { Rad', 13,
    { sourceName: 'spring.firmament', sourceRevision: 'draft-3' });
  assert.equal(result.revision, 'draft-3');
  assert.deepEqual(requests, [{ operation: 'languageComplete', source: 'Helix H { Rad', offset: 13,
    sourceName: 'spring.firmament', sourceRevision: 'draft-3' }]);
});

test('public semantic schema uses the versioned runtime contract', async () => {
  const requests = [];
  const cad = new Aetheris({ request: async request => {
    requests.push(request);
    return { version: 'firmament-semantic-schema/1', constructs: [] };
  } });
  const schema = await cad.language.schema();
  assert.equal(schema.version, 'firmament-semantic-schema/1');
  assert.deepEqual(requests, [{ operation: 'languageSchema' }]);
});
