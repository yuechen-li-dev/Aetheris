import { createRuntime, transferables } from './runtime.js';

let runtimePromise;
// .NET 10 detects a truthy onmessage as a runtime sidecar Worker and can hang
// during asset loading. A listener leaves that property unset.
self.addEventListener('message', async event => {
  const { version, id, sourceRevision, request, runtimeBase } = event.data;
  try {
    if (version !== 1) throw new Error(`Unsupported Aetheris worker protocol ${version}.`);
    const invoke = await (runtimePromise ??= createRuntime(runtimeBase ? new URL(runtimeBase) : undefined));
    const started = performance.now();
    const result = invoke(request);
    const executionMilliseconds = performance.now() - started;
    if (request.operation === 'exportStep') {
      const binary = atob(result.base64);
      const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
      self.postMessage({ version, id, sourceRevision, result: { bytes }, executionMilliseconds, payloadBytes: bytes.byteLength }, { transfer: [bytes.buffer] });
    } else {
      const buffers = transferables(result);
      self.postMessage({ version, id, sourceRevision, result, executionMilliseconds, payloadBytes: buffers.reduce((size, buffer) => size + buffer.byteLength, 0) }, { transfer: buffers });
    }
  } catch (error) {
    self.postMessage({ version, id, sourceRevision, error: { name: error.name, code: error.code ?? 'internal-error', message: error.message, details: error.details } });
  }
});
