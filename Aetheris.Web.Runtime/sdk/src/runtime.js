export function normalizeMesh(value) {
  const mesh = value?.model?.mesh ?? value?.mesh;
  if (!mesh?.definitions) return value;
  for (const definition of mesh.definitions) {
    if (!(definition.positions instanceof Float64Array)) definition.positions = Float64Array.from(definition.positions);
    if (!(definition.normals instanceof Float64Array)) definition.normals = Float64Array.from(definition.normals);
    if (!(definition.indices instanceof Uint32Array)) definition.indices = Uint32Array.from(definition.indices);
  }
  return value;
}

export function transferables(value) {
  const result = [];
  const mesh = value?.model?.mesh ?? value?.mesh;
  for (const definition of mesh?.definitions ?? []) result.push(definition.positions.buffer, definition.normals.buffer, definition.indices.buffer);
  return result;
}

export async function createRuntime(runtimeBase = new URL('./runtime/', import.meta.url)) {
  const dotnetUrl = new URL('_framework/dotnet.js', runtimeBase);
  const importStart = performance.now();
  const { dotnet } = await import(dotnetUrl.href);
  const moduleImportMilliseconds = performance.now() - importStart;
  const createStart = performance.now();
  const runtime = await dotnet.withApplicationArguments().create();
  const runtimeCreateMilliseconds = performance.now() - createStart;
  const exportsStart = performance.now();
  const exports = await runtime.getAssemblyExports('Aetheris.Web.Runtime.dll');
  const assemblyExportsMilliseconds = performance.now() - exportsStart;
  const invoke = exports.Aetheris.Web.Runtime.Program.Invoke;
  const requestRuntime = request => {
    const start = performance.now();
    const requestJson = JSON.stringify(request);
    const requestMilliseconds = performance.now() - start;
    const invokeStart = performance.now();
    const responseJson = invoke(requestJson);
    const invokeMilliseconds = performance.now() - invokeStart;
    const parseStart = performance.now();
    const envelope = JSON.parse(responseJson);
    const responseParseMilliseconds = performance.now() - parseStart;
    if (!envelope.ok) {
      const error = new Error(envelope.error.message);
      error.name = 'AetherisError'; error.code = envelope.error.code; error.details = envelope.error.details;
      throw error;
    }
    const normalizeStart = performance.now();
    const result = normalizeMesh(envelope.result);
    const meshNormalizeMilliseconds = performance.now() - normalizeStart;
    if (request.performance && result?.model?.timings) result.model.timings.bridge = {
      requestMilliseconds, invokeMilliseconds, responseParseMilliseconds,
      meshNormalizeMilliseconds, responseBytes: responseJson.length
    };
    return result;
  };
  requestRuntime.initTiming = { moduleImportMilliseconds, runtimeCreateMilliseconds, assemblyExportsMilliseconds };
  return requestRuntime;
}
