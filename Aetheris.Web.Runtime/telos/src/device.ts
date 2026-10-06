import { loadTelosShaders, type TelosShaderSources } from "./shaderSources.js";
const contextOwners = new WeakMap<GPUCanvasContext, TelosDevice>();

export class TelosResources {
  device: GPUDevice;
  buffers: Set<GPUBuffer>;
  textures: Set<GPUTexture>;
  modules: Map<string, { module: GPUShaderModule; code: string }>;
  pipelines: Map<string, GPURenderPipeline>;
  constructor(device: GPUDevice) {
    this.device = device;
    this.buffers = new Set();
    this.textures = new Set();
    this.modules = new Map();
    this.pipelines = new Map();
  }
  buffer(
    data: Float32Array<ArrayBuffer> | Uint32Array<ArrayBuffer>,
    usage: GPUBufferUsageFlags,
  ) {
    const buffer = this.device.createBuffer({
      size: Math.max(4, Math.ceil(data.byteLength / 4) * 4),
      usage: usage | GPUBufferUsage.COPY_DST,
    });
    this.device.queue.writeBuffer(buffer, 0, data);
    this.buffers.add(buffer);
    return buffer;
  }
  texture(descriptor: GPUTextureDescriptor) {
    const texture = this.device.createTexture(descriptor);
    this.textures.add(texture);
    return texture;
  }
  release(resource: GPUBuffer | GPUTexture) {
    resource.destroy();
    if ("mapAsync" in resource) this.buffers.delete(resource);
    else this.textures.delete(resource);
  }
  module(id: string, code: string) {
    const previous = this.modules.get(id);
    if (previous && previous.code !== code)
      throw new Error("telos-shader-identity-collision: " + id);
    if (previous) return previous.module;
    const module = this.device.createShaderModule({ label: id, code });
    this.modules.set(id, { module, code });
    return module;
  }
  pipeline(key: string, create: () => GPURenderPipeline): GPURenderPipeline {
    if (!this.pipelines.has(key)) this.pipelines.set(key, create());
    return this.pipelines.get(key)!;
  }
  /** Modules/pipelines have no destroy API. Drop owned references once a semantic
   * program leaves the model; per-draw buffers are released by the host. */
  releaseProgram(id: string) {
    this.modules.delete(id);
    for (const key of this.pipelines.keys())
      if (JSON.parse(key)[0] === id) this.pipelines.delete(key);
  }
  dispose() {
    for (const resource of [...this.buffers, ...this.textures])
      resource.destroy();
    this.buffers.clear();
    this.textures.clear();
    this.modules.clear();
    this.pipelines.clear();
  }
}

export class TelosDevice {
  sampleCount: 1 | 4 = 4;
  shaders: TelosShaderSources;
  adapter: GPUAdapter;
  device: GPUDevice;
  context: GPUCanvasContext;
  canvas: HTMLCanvasElement;
  diagnostic: (message: string) => void;
  format: GPUTextureFormat;
  resources: TelosResources;
  capabilities: { features: string[]; limits: GPUSupportedLimits };
  stopped: boolean;
  onLost?: () => void;
  onError: (event: GPUUncapturedErrorEvent) => void;
  static async create(
    canvas: HTMLCanvasElement,
    diagnostic: (message: string) => void = () => {},
    signal?: AbortSignal,
  ) {
    if (!navigator.gpu)
      throw new Error(
        "telos-webgpu-unavailable: use a WebGPU-capable browser.",
      );
    const adapter = await navigator.gpu.requestAdapter();
    if (!adapter) throw new Error("telos-adapter-unavailable");
    const shaders = await loadTelosShaders();
    // Timing is optional; neither viewport nor temporal startup depends on it.
    let device: GPUDevice;
    if (adapter.features.has("timestamp-query")) {
      device = await adapter
        .requestDevice({ requiredFeatures: ["timestamp-query"] })
        .catch(() => adapter.requestDevice());
    } else {
      device = await adapter.requestDevice();
    }
    if (signal?.aborted) {
      device.destroy();
      throw new DOMException("Viewport initialization cancelled", "AbortError");
    }
    const context = canvas.getContext("webgpu");
    if (!context) {
      device.destroy();
      throw new Error("telos-context-unavailable");
    }
    return new TelosDevice(
      adapter,
      device,
      context,
      canvas,
      diagnostic,
      shaders,
    );
  }
  constructor(
    adapter: GPUAdapter,
    device: GPUDevice,
    context: GPUCanvasContext,
    canvas: HTMLCanvasElement,
    diagnostic: (message: string) => void,
    shaders: TelosShaderSources,
  ) {
    this.shaders = shaders;
    this.adapter = adapter;
    this.device = device;
    this.context = context;
    this.canvas = canvas;
    this.diagnostic = diagnostic;
    this.format = navigator.gpu.getPreferredCanvasFormat();
    this.resources = new TelosResources(device);
    this.capabilities = {
      features: [...device.features],
      limits: device.limits,
    };
    this.stopped = false;
    context.configure({
      device,
      format: this.format,
      alphaMode: "opaque",
      usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.COPY_SRC,
    });
    contextOwners.set(context, this);
    device.lost.then((info) => {
      const expected = this.stopped;
      this.stopped = true;
      if (!expected) diagnostic("telos-device-lost: " + info.message);
      this.onLost?.();
    });
    this.onError = (event) => {
      diagnostic("telos-gpu-validation: " + event.error.message);
      this.stopped = true;
      this.onLost?.();
    };
    device.addEventListener("uncapturederror", this.onError);
  }
  dispose() {
    this.stopped = true;
    this.resources.dispose();
    if (contextOwners.get(this.context) === this) {
      this.context.unconfigure();
      contextOwners.delete(this.context);
    }
    this.device.removeEventListener("uncapturederror", this.onError);
    this.device.destroy();
  }
}

/** One depth owner. Spatial rendering uses four samples; temporal experiments use one. */
export class TelosFrame {
  owner: TelosDevice;
  depth: GPUTexture | null;
  color: GPUTexture | null = null;
  private samples = 0;
  depthView!: GPUTextureView;
  colorView!: GPUTextureView;
  target!: GPUTexture;
  encoder!: GPUCommandEncoder;
  width: number;
  height: number;
  generation: number;
  dpr = 1;
  constructor(owner: TelosDevice) {
    this.owner = owner;
    this.depth = null;
    this.width = 0;
    this.height = 0;
    this.generation = 0;
  }
  resize(width: number, height: number, dpr = 1) {
    this.dpr = Math.max(0.25, dpr);
    const limit = this.owner.device.limits.maxTextureDimension2D;
    const w = Math.min(limit, Math.max(1, Math.round(width * this.dpr))),
      h = Math.min(limit, Math.max(1, Math.round(height * this.dpr)));
    if (
      this.width === w &&
      this.height === h &&
      this.samples === this.owner.sampleCount
    )
      return;
    if (this.depth) this.owner.resources.release(this.depth);
    if (this.color) this.owner.resources.release(this.color);
    this.samples = this.owner.sampleCount;
    this.owner.canvas.width = this.width = w;
    this.owner.canvas.height = this.height = h;
    this.depth = this.owner.resources.texture({
      label: "Telos authoritative depth",
      size: [w, h],
      format: "depth32float",
      sampleCount: this.samples,
      usage:
        GPUTextureUsage.RENDER_ATTACHMENT |
        GPUTextureUsage.TEXTURE_BINDING |
        (this.samples === 1 ? GPUTextureUsage.COPY_SRC : 0),
    });
    this.color =
      this.samples === 4
        ? this.owner.resources.texture({
            label: "Telos spatial color",
            size: [w, h],
            format: this.owner.format,
            sampleCount: 4,
            usage: GPUTextureUsage.RENDER_ATTACHMENT,
          })
        : null;
    this.depthView = this.depth.createView();
    this.generation++;
  }
  begin(background: GPUColor, source?: GPUTexture) {
    this.target = this.owner.context.getCurrentTexture();
    this.colorView = (this.color ?? source ?? this.target).createView();
    this.encoder = this.owner.device.createCommandEncoder();
    const pass = this.pass("Background", true, background);
    pass.end();
  }
  pass(label: string, clear = false, background: GPUColor = [0, 0, 0, 1]) {
    return this.encoder.beginRenderPass({
      label,
      colorAttachments: [
        {
          view: this.colorView,
          clearValue: background,
          loadOp: clear ? "clear" : "load",
          storeOp: "store",
        },
      ],
      depthStencilAttachment: {
        view: this.depthView,
        depthClearValue: 1,
        depthLoadOp: clear ? "clear" : "load",
        depthStoreOp: "store",
      },
    });
  }
  end() {
    if (this.color) {
      // Resolve once, after topology and overlays; every pass shares the same depth.
      const pass = this.encoder.beginRenderPass({
        colorAttachments: [
          {
            view: this.colorView,
            resolveTarget: this.target.createView(),
            loadOp: "load",
            storeOp: "discard",
          },
        ],
      });
      pass.end();
    }
    this.owner.device.queue.submit([this.encoder.finish()]);
  }
  dispose() {
    if (this.depth) this.owner.resources.release(this.depth);
    if (this.color) this.owner.resources.release(this.color);
    this.color = null;
    this.depth = null;
  }
}
