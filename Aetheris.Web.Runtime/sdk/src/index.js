import { createRuntime } from './runtime.js';

const defaultRuntimeBase = typeof __AETHERIS_RUNTIME_BASE__ !== 'undefined'
  ? new URL(__AETHERIS_RUNTIME_BASE__, globalThis.location?.href)
  : new URL('./runtime/', import.meta.url);

export class AetherisError extends Error {
  constructor(code, message, details) { super(message); this.name = 'AetherisError'; this.code = code; this.details = details; }
}

class DirectTransport {
  constructor(runtimeBase) { this.runtimeBase = runtimeBase; this.queue = Promise.resolve(); }
  async request(request) {
    this.invoke ??= await createRuntime(this.runtimeBase);
    const operation = this.queue.then(() => this.invoke(request));
    this.queue = operation.catch(() => undefined);
    return operation;
  }
  dispose() { return this.request({ operation: 'disposeRuntime' }); }
}

class WorkerTransport {
  constructor(runtimeBase) {
    this.runtimeBase = runtimeBase;
    this.worker = new Worker(new URL('./worker.js', import.meta.url), { type: 'module', name: 'Aetheris CAD' });
    this.pending = new Map(); this.nextId = 0;
    this.worker.onmessage = ({ data }) => {
      const pending = this.pending.get(data.id); if (!pending) return;
      this.pending.delete(data.id);
      data.error ? pending.reject(new AetherisError(data.error.code, data.error.message, data.error.details)) : pending.resolve(data.result);
    };
    this.worker.onerror = event => {
      const error = new AetherisError('worker-error', event.message || 'The Aetheris worker failed to start.');
      for (const pending of this.pending.values()) pending.reject(error);
      this.pending.clear();
    };
  }
  request(request) {
    return new Promise((resolve, reject) => {
      const id = ++this.nextId; this.pending.set(id, { resolve, reject });
      this.worker.postMessage({ id, request, runtimeBase: this.runtimeBase?.href });
    });
  }
  async dispose() { try { await this.request({ operation: 'disposeRuntime' }); } finally { this.worker.terminate(); } }
}

export class Aetheris {
  static async create(options = {}) {
    if (options.worker === true) throw new AetherisError('worker-unavailable', 'Worker-backed execution is not qualified in Web SDK X1. Use the default in-page runtime.');
    const runtimeBase = options.wasmUrl ? new URL(options.wasmUrl, globalThis.location?.href) : defaultRuntimeBase;
    const transport = new DirectTransport(runtimeBase);
    const cad = new Aetheris(transport, options.diagnostics);
    cad.runtimeInfo = await cad.info();
    return cad;
  }
  constructor(transport, diagnostics) {
    this.transport = transport; this.diagnostics = diagnostics;
    this.language = {
      schema: () => this.transport.request({ operation: 'languageSchema' }),
      complete: (source, offset, options = {}) => this.transport.request({ operation: 'languageComplete', source, offset,
        sourceName: options.sourceName, sourceRevision: options.sourceRevision })
    };
  }
  info() { return this.transport.request({ operation: 'info' }); }
  async capabilities() { return (await this.info()).capabilities; }
  async compile(source, options = {}) {
    throwIfAborted(options.signal);
    const result = await this.transport.request({ operation: 'compile', source, sourceName: options.sourceName });
    this.diagnostics?.(result.diagnostics);
    return { model: result.success ? new ModelSession(this.transport, result.model) : null, diagnostics: result.diagnostics };
  }
  dispose() { return this.transport.dispose(); }
}

export class ModelSession {
  constructor(transport, snapshot) { this.transport = transport; this.apply(snapshot); this.queue = Promise.resolve(); }
  apply(snapshot) {
    Object.assign(this, snapshot);
    this._rangesBySemantic = new Map();
    this._rangesBySource = new Map();
    this._rangesByEntity = new Map();
    this._rangesByDefinition = new Map();
    this._geometrySourceMap = [];
    const add = (index, key, value) => { if (key != null) { const items = index.get(key) ?? []; items.push(value); index.set(key, items); } };
    for (const definition of this.mesh.definitions) {
      for (const range of definition.ranges ?? []) {
        const item = { definitionId: definition.id, ...range };
        add(this._rangesBySemantic, range.semanticTopologyId, item);
        add(this._rangesBySource, range.originFeature, item);
        add(this._rangesByEntity, range.semanticEntityId, item);
        add(this._rangesByDefinition, definition.id, item);
        this._geometrySourceMap.push({ definitionId: definition.id, topologyId: range.faceId, topologyKind: 'Face', semanticKey: range.semanticTopologyId ?? null,
          outputRole: range.outputRole ?? null, originFeature: range.originFeature ?? null, source: range.source ?? null,
          selector: range.selector ?? null, qualification: range.sourceAddressability ?? 'RuntimeOnly', buildRevision: range.buildRevision ?? this.revision });
      }
      for (const edge of definition.edges ?? []) this._geometrySourceMap.push({ definitionId: definition.id, topologyId: edge.edgeId, topologyKind: 'Edge',
        semanticKey: edge.semanticTopologyId ?? null, outputRole: edge.outputRole ?? null, originFeature: edge.originFeature ?? null,
        source: edge.source ?? null, selector: edge.selector ?? null, qualification: edge.sourceAddressability ?? 'RuntimeOnly', buildRevision: edge.buildRevision ?? this.revision });
    }
  }
  entity(id) { return this.tree.nodes.find(node => node.id === id); }
  property(id) { return this.properties.find(property => property.id === id); }
  async setProperty(propertyId, value, options = {}) {
    return this.serial(async () => { throwIfAborted(options.signal); await this.transport.request({ operation: 'setProperty', sessionId: this.id, propertyId, value }); return this.rebuild(options); });
  }
  async setSource(source, options = {}) {
    return this.serial(async () => { throwIfAborted(options.signal); await this.transport.request({ operation: 'setSource', sessionId: this.id, source, sourceName: options.sourceName }); return this.rebuild(options); });
  }
  async describeConstruct(semanticId, options = {}) {
    throwIfAborted(options.signal);
    return this.transport.request({ operation: 'describeConstruct', sessionId: this.id, constructSemanticId: semanticId });
  }
  async rewriteField(source, projection, fieldId, value, options = {}) {
    throwIfAborted(options.signal);
    return this.transport.request({ operation: 'rewriteField', sessionId: this.id, source,
      sourceRevision: projection.sourceRevision, buildRevision: projection.buildRevision,
      constructSemanticId: projection.semanticId, fieldId, fieldValue: value });
  }
  async rebuild(options = {}) {
    throwIfAborted(options.signal);
    const result = await this.transport.request({ operation: 'rebuild', sessionId: this.id });
    if (result.success) this.apply(result.model);
    return result;
  }
  resolveSelection(definitionId, triangleIndex, occurrenceId) {
    const definition = this.mesh.definitions.find(item => item.id === definitionId);
    const occurrence = occurrenceId ? this.mesh.occurrences.find(item => item.id === occurrenceId) : this.mesh.occurrences.find(item => item.definitionId === definitionId);
    const range = definition?.ranges?.find(item => triangleIndex >= item.startTriangle && triangleIndex < item.startTriangle + item.triangleCount);
    return range && occurrence ? { semanticEntityId: range.semanticEntityId ?? occurrence.semanticEntityId, faceId: range.faceId, occurrenceId: occurrence.id, definitionId,
      semanticTopologyId: range.semanticTopologyId ?? null, topologyKind: range.topologyKind ?? 'Face', outputRole: range.outputRole ?? null, originFeature: range.originFeature ?? null,
      selector: range.selector ?? null, sourceAddressability: range.sourceAddressability ?? 'RuntimeOnly',
      selectorReason: range.selectorReason ?? null, source: range.source ?? null, buildRevision: range.buildRevision ?? this.revision } : null;
  }
  describeSelection(definitionId, triangleIndex, occurrenceId) {
    const selection = this.resolveSelection(definitionId, triangleIndex, occurrenceId);
    if (!selection) return null;
    return { ...selection, sourceAddressable: selection.source != null && ['AuthoredStable', 'DerivedStable'].includes(selection.sourceAddressability),
      selectorReason: selection.selectorReason ?? (selection.selector ? null : 'No compiler-owned source selector mapping is available for this display face.') };
  }
  describeEdgeSelection(definitionId, edgeId, occurrenceId) {
    const definition = this.mesh.definitions.find(item => item.id === definitionId);
    const occurrence = occurrenceId ? this.mesh.occurrences.find(item => item.id === occurrenceId) : this.mesh.occurrences.find(item => item.definitionId === definitionId);
    const edge = definition?.edges?.find(item => item.edgeId === edgeId);
    if (!edge || !occurrence) return null;
    return { semanticEntityId: edge.semanticEntityId ?? occurrence.semanticEntityId, faceId: edge.edgeId, occurrenceId: occurrence.id, definitionId,
      semanticTopologyId: edge.semanticTopologyId ?? null, topologyKind: 'Edge', outputRole: edge.outputRole ?? null, originFeature: edge.originFeature ?? null,
      selector: edge.selector ?? null, sourceAddressability: edge.sourceAddressability ?? 'RuntimeOnly',
      selectorReason: edge.selectorReason ?? 'This edge has no qualified Firmament source selector.',
      source: edge.source ?? null, buildRevision: edge.buildRevision ?? this.revision,
      sourceAddressable: edge.source != null && ['AuthoredStable', 'DerivedStable'].includes(edge.sourceAddressability) };
  }
  selectionForSemanticId(semanticTopologyId) {
    return this._rangesBySemantic.get(semanticTopologyId) ?? [];
  }
  selectionForSourceSymbol(symbol) {
    return this._rangesBySource.get(symbol) ?? [];
  }
  selectionForEntity(entityId) {
    const occurrences = this.mesh.occurrences.filter(item => item.semanticEntityId === entityId || item.definitionId === entityId);
    const occurrenceIds = occurrences.map(item => item.id);
    const definitionIds = new Set(occurrences.map(item => item.definitionId).filter(Boolean));
    const ranges = [...(this._rangesByEntity.get(entityId) ?? [])];
    for (const definitionId of definitionIds) for (const range of this._rangesByDefinition.get(definitionId) ?? []) if (!ranges.includes(range)) ranges.push(range);
    return { occurrenceIds, ranges };
  }
  geometrySourceMap() { return this._geometrySourceMap; }
  selectorCandidates(kind = 'Face') {
    const admitted = new Set(['AuthoredStable', 'DerivedStable', 'ImportedStable']);
    const seen = new Set();
    return this._geometrySourceMap.filter(item => {
      if (item.topologyKind !== kind || !item.selector || !item.source || !admitted.has(item.qualification)) return false;
      const key = `${item.selector}\u0000${item.semanticKey ?? ''}`;
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    }).map(item => ({ selector: item.selector, kind: item.topologyKind, semanticKey: item.semanticKey,
      outputRole: item.outputRole, source: item.source, qualification: item.qualification, buildRevision: item.buildRevision }))
      .sort((a, b) => a.selector < b.selector ? -1 : a.selector > b.selector ? 1 : 0);
  }
  async exportSTEP(options = {}) {
    throwIfAborted(options.signal);
    const result = await this.transport.request({ operation: 'exportStep', sessionId: this.id });
    const binary = atob(result.base64); const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return bytes;
  }
  async exportSTEPBlob(options = {}) { return new Blob([await this.exportSTEP(options)], { type: 'model/step' }); }
  async dispose() { await this.transport.request({ operation: 'disposeSession', sessionId: this.id }); }
  serial(operation) { const result = this.queue.then(operation); this.queue = result.catch(() => undefined); return result; }
}

function throwIfAborted(signal) {
  if (signal?.aborted) throw new DOMException('The Aetheris operation was aborted before execution.', 'AbortError');
}
