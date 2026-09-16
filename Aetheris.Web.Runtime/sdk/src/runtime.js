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
  const { dotnet } = await import(dotnetUrl.href);
  const runtime = await dotnet.withApplicationArguments().create();
  const exports = await runtime.getAssemblyExports('Aetheris.Web.Runtime.dll');
  const invoke = exports.Aetheris.Web.Runtime.Program.Invoke;
  return request => {
    const envelope = JSON.parse(invoke(JSON.stringify(request)));
    if (!envelope.ok) {
      const error = new Error(envelope.error.message);
      error.name = 'AetherisError'; error.code = envelope.error.code; error.details = envelope.error.details;
      throw error;
    }
    return normalizeMesh(envelope.result);
  };
}
