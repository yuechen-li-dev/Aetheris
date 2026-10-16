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

test('public language outline sends source, revision and project context through runtime transport', async () => {
  const requests = [];
  const cad = new Aetheris({ request: async request => {
    requests.push(request);
    return { version: 'firmament-outline/1', document: request.sourceName, revision: request.sourceRevision,
      nodes: [], edges: [], externals: [], diagnostics: [] };
  } });
  const outline = await cad.language.outline('Model M { Units: mm }',
    { sourceName: 'root.firmament', sourceRevision: 'r9', projectRoot: 'root.firmament', projectDocuments: { 'part.firmament': 'Struct P {}' } });
  assert.equal(outline.version, 'firmament-outline/1');
  assert.equal(outline.revision, 'r9');
  assert.deepEqual(requests, [{ operation: 'languageOutline', source: 'Model M { Units: mm }', sourceName: 'root.firmament',
    sourceRevision: 'r9', projectDocuments: { 'part.firmament': 'Struct P {}' }, projectRoot: 'root.firmament' }]);
});

test('public language edit sends operations with the source revision through runtime transport', async () => {
  const requests = [];
  const cad = new Aetheris({ request: async request => {
    requests.push(request);
    return { document: request.sourceName, revision: request.sourceRevision, text: 'Model M { Units: cm }', changed: true,
      edit: { start: 17, length: 2, replacement: 'cm' }, nodes: ['M'], warnings: [] };
  } });
  const operations = [{ kind: 'setField', node: 'M', field: 'Units', value: 'cm' }];
  const edit = await cad.language.edit('Model M { Units: mm }', operations, { sourceName: 'm.firmament', sourceRevision: 'r3' });
  assert.equal(edit.text, 'Model M { Units: cm }');
  assert.deepEqual(edit.nodes, ['M']);
  assert.deepEqual(requests, [{ operation: 'languageEdit', source: 'Model M { Units: mm }', operations, sourceName: 'm.firmament', sourceRevision: 'r3' }]);
});

test('public language palette asks the runtime for the catalog', async () => {
  const requests = [];
  const cad = new Aetheris({ request: async request => { requests.push(request); return { version: 'firmament-palette/1', items: [] }; } });
  assert.equal((await cad.language.palette()).version, 'firmament-palette/1');
  assert.deepEqual(requests, [{ operation: 'languagePalette' }]);
});
