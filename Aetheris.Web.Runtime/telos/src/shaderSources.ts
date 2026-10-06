export interface TelosShaderSources {
  mesh: string;
  line: string;
  temporal?: string;
}

async function loadShader(url: URL): Promise<string> {
  try {
    const response = await fetch(url);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return await response.text();
  } catch (cause) {
    throw new Error(`telos-shader-load: ${url.pathname}`, { cause });
  }
}

/** Static asset URLs work in native ESM and are rewritten by browser bundlers. */
export async function loadTelosShaders(): Promise<TelosShaderSources> {
  const [mesh, line] = await Promise.all([
    loadShader(new URL("./shaders/mesh.wgsl", import.meta.url)),
    loadShader(new URL("./shaders/line.wgsl", import.meta.url)),
  ]);
  // Optional temporal assets never gate the existing viewport path.
  const temporal = await Promise.all([
    loadShader(new URL("./shaders/temporal-policy.wgsl", import.meta.url)),
    loadShader(new URL("./shaders/temporal-resolve.wgsl", import.meta.url)),
  ]).then(parts => parts.join("\n")).catch(() => undefined);
  return { mesh, line, temporal };
}
