export function normalizeMesh(value) {
  const mesh = value?.model?.mesh ?? value?.mesh;
  if (!mesh?.definitions) return value;
  for (const definition of mesh.definitions) {
    // A retained definition is resolved by ModelSession from the one it already holds.
    if (definition.retained) continue;
    if (!(definition.positions instanceof Float64Array)) definition.positions = Float64Array.from(definition.positions);
    if (!(definition.normals instanceof Float64Array)) definition.normals = Float64Array.from(definition.normals);
    // Bridge indices arrive as Int32Array views; indices are non-negative, so reinterpret in place.
    if (definition.indices instanceof Int32Array) definition.indices = new Uint32Array(definition.indices.buffer, definition.indices.byteOffset, definition.indices.length);
    else if (!(definition.indices instanceof Uint32Array)) definition.indices = Uint32Array.from(definition.indices);
  }
  return value;
}

/** Distinct ArrayBuffers behind the mesh arrays (bridge arrays share one buffer). */
export function transferables(value) {
  const result = new Set();
  const mesh = value?.model?.mesh ?? value?.mesh;
  for (const definition of mesh?.definitions ?? []) {
    if (definition.retained) continue;
    for (const array of [definition.positions, definition.normals, definition.indices]) if (array?.buffer) result.add(array.buffer);
  }
  return [...result];
}

/** BUILD-PERF-M3: replace {"$binary","byteOffset","length"} placeholders by typed-array views. */
export function reviveBinary(value, bytes) {
  if (Array.isArray(value)) {
    for (let i = 0; i < value.length; i++) { const item = value[i]; if (item !== null && typeof item === 'object') value[i] = reviveBinary(item, bytes); }
    return value;
  }
  const type = value.$binary;
  if (type !== undefined) {
    const start = bytes.byteOffset + value.byteOffset;
    if (type === 'f64') return new Float64Array(bytes.buffer, start, value.length);
    if (type === 'i32') return new Int32Array(bytes.buffer, start, value.length);
    throw new Error(`Unknown binary array type '${type}'.`);
  }
  for (const key in value) { const item = value[key]; if (item !== null && typeof item === 'object') value[key] = reviveBinary(item, bytes); }
  return value;
}

export async function createRuntime(runtimeBase = new URL('./runtime/', import.meta.url)) {
  const dotnetUrl = new URL('_framework/dotnet.js', runtimeBase);
  const importStart = performance.now();
  const { dotnet } = await import(/* @vite-ignore */ dotnetUrl.href);
  const moduleImportMilliseconds = performance.now() - importStart;
  const createStart = performance.now();
  const runtime = await dotnet.withApplicationArguments().create();
  const runtimeCreateMilliseconds = performance.now() - createStart;
  const exportsStart = performance.now();
  const exports = await runtime.getAssemblyExports('Aetheris.Web.Runtime.dll');
  const assemblyExportsMilliseconds = performance.now() - exportsStart;
  const invoke = exports.Aetheris.Web.Runtime.Program.Invoke;
  // A runtime with TakeBinary speaks bridge 1: binary mesh arrays and retained definitions.
  const takeBinary = exports.Aetheris.Web.Runtime.Program.TakeBinary;
  const bridge = typeof takeBinary === 'function' ? 1 : undefined;
  const requestRuntime = request => {
    const start = performance.now();
    const requestJson = JSON.stringify(bridge ? { ...request, bridge } : request);
    const requestMilliseconds = performance.now() - start;
    const invokeStart = performance.now();
    const responseJson = invoke(requestJson);
    const invokeMilliseconds = performance.now() - invokeStart;
    const parseStart = performance.now();
    const envelope = JSON.parse(responseJson);
    const responseParseMilliseconds = performance.now() - parseStart;
    const binaryStart = performance.now();
    let binaryBytes = 0;
    if (bridge) {
      let bytes = takeBinary();
      if (bytes?.byteLength) {
        // Float64Array views need 8-byte alignment of the underlying buffer offset.
        if (!(bytes instanceof Uint8Array) || bytes.byteOffset % 8 !== 0) bytes = Uint8Array.from(bytes);
        binaryBytes = bytes.byteLength;
        if (envelope.ok) reviveBinary(envelope.result, bytes);
      }
    }
    const binaryMilliseconds = performance.now() - binaryStart;
    if (!envelope.ok) {
      const error = new Error(envelope.error.message);
      error.name = 'AetherisError'; error.code = envelope.error.code; error.details = envelope.error.details;
      throw error;
    }
    const normalizeStart = performance.now();
    const result = normalizeMesh(envelope.result);
    const meshNormalizeMilliseconds = performance.now() - normalizeStart;
    if (request.performance && result?.model?.timings) result.model.timings.bridge = {
      requestMilliseconds, invokeMilliseconds, responseParseMilliseconds, binaryMilliseconds,
      meshNormalizeMilliseconds, responseBytes: responseJson.length, binaryBytes, bridge: bridge ?? 0
    };
    return result;
  };
  requestRuntime.initTiming = { moduleImportMilliseconds, runtimeCreateMilliseconds, assemblyExportsMilliseconds };
  return requestRuntime;
}
