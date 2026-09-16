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
  constructor(transport, diagnostics) { this.transport = transport; this.diagnostics = diagnostics; }
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
  apply(snapshot) { Object.assign(this, snapshot); }
  entity(id) { return this.tree.nodes.find(node => node.id === id); }
  property(id) { return this.properties.find(property => property.id === id); }
  async setProperty(propertyId, value, options = {}) {
    return this.serial(async () => { throwIfAborted(options.signal); await this.transport.request({ operation: 'setProperty', sessionId: this.id, propertyId, value }); return this.rebuild(options); });
  }
  async setSource(source, options = {}) {
    return this.serial(async () => { throwIfAborted(options.signal); await this.transport.request({ operation: 'setSource', sessionId: this.id, source, sourceName: options.sourceName }); return this.rebuild(options); });
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
    return range && occurrence ? { semanticEntityId: occurrence.semanticEntityId ?? range.semanticEntityId, faceId: range.faceId, occurrenceId: occurrence.id, definitionId } : null;
  }
  selectionForEntity(entityId) {
    const occurrences = this.mesh.occurrences.filter(item => item.semanticEntityId === entityId || item.definitionId === entityId);
    const occurrenceIds = occurrences.map(item => item.id);
    const definitionIds = new Set(occurrences.map(item => item.definitionId).filter(Boolean));
    const ranges = this.mesh.definitions.flatMap(definition => (definition.ranges ?? []).filter(range => range.semanticEntityId === entityId || definitionIds.has(definition.id)).map(range => ({ definitionId: definition.id, ...range })));
    return { occurrenceIds, ranges };
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
