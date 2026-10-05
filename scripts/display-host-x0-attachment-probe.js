import * as THREE from 'three/webgpu';

// Diagnostic only. No SDK export or product migration is claimed by this probe.
export async function probeExternalAttachments(probeOwnedDepth) {
  const result = { revision: THREE.REVISION, probeOwnedDepth, errors: [], scopes: [] };
  if (!navigator.gpu) throw new Error('DISPLAY_WEBGPU_UNAVAILABLE');
  const adapter = await navigator.gpu.requestAdapter();
  if (!adapter) throw new Error('DISPLAY_WEBGPU_NO_ADAPTER');
  const device = await adapter.requestDevice();
  device.addEventListener('uncapturederror', event => result.errors.push(event.error.message));
  const renderer = new THREE.WebGPURenderer({ device, antialias: false });
  const targets = [], textures = [];
  const geometry = new THREE.SphereGeometry(1, 24, 16);
  const material = new THREE.MeshStandardMaterial({ color: 0x90bba0, roughness: 0.6, metalness: 0.1 });
  try {
    await renderer.init();
    const scene = new THREE.Scene(); scene.background = new THREE.Color(0x18221c);
    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 100); camera.position.z = 5;
    scene.add(new THREE.Mesh(geometry, material), new THREE.HemisphereLight(0xffffff, 0x303030, 3));
    const control = new THREE.RenderTarget(256, 256, { depthTexture: new THREE.DepthTexture(256, 256, THREE.FloatType) });
    targets.push(control);
    const run = async target => {
      device.pushErrorScope('validation');
      let exception = null;
      renderer.setRenderTarget(target);
      try { renderer.render(scene, camera); }
      catch (error) { exception = String(error); }
      finally { renderer.setRenderTarget(null); }
      await device.queue.onSubmittedWorkDone();
      const validation = (await device.popErrorScope())?.message ?? null;
      return { exception, validation };
    };
    result.control = await run(control);
    const external = (format, threeFormat, type) => {
      const gpu = device.createTexture({ size: [256, 256], format,
        usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_SRC });
      textures.push(gpu);
      const texture = new THREE.ExternalTexture(gpu);
      texture.image = { width: 256, height: 256, depth: 1 };
      texture.format = threeFormat; texture.type = type;
      return texture;
    };
    const color = external('rgba8unorm', THREE.RGBAFormat, THREE.UnsignedByteType);
    const depth = probeOwnedDepth ? new THREE.DepthTexture(256, 256, THREE.FloatType)
      : external('depth32float', THREE.DepthFormat, THREE.FloatType);
    const target = new THREE.RenderTarget(256, 256, { depthTexture: depth });
    target.texture = color; targets.push(target);
    result.external = await run(target);
    result.status = result.external.exception || result.external.validation || result.errors.length ? 'BLOCKED' : 'PASS';
    return result;
  } finally {
    for (const target of targets) target.dispose();
    // r183 forwards external disposal; r186 leaves ownership to the application.
    for (const texture of textures) texture.destroy();
    geometry.dispose(); material.dispose(); renderer.dispose(); device.destroy();
  }
}
