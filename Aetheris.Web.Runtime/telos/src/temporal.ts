import { Matrix4 } from "three";
import type { TelosDevice, TelosFrame } from "./device.js";
import type { TelosCamera } from "./camera.js";

export type TelosAAMode = "None" | "SpatialOnly" | "TAA" | "TAAUtility";
export type TelosAADebug =
  | "color"
  | "policy"
  | "confidence"
  | "motion"
  | "depth";
const debugNames: TelosAADebug[] = [
  "color",
  "policy",
  "confidence",
  "motion",
  "depth",
];
const halton = (index: number, base: number) => {
  let value = 0;
  let fraction = 1;
  while (index > 0) {
    fraction /= base;
    value += fraction * (index % base);
    index = Math.floor(index / base);
  }
  return value - 0.5;
};
/** Eight deterministic physical-pixel samples. Picking never consumes these matrices. */
export function temporalJitter(index: number): [number, number] {
  return [halton((index % 8) + 1, 2), halton((index % 8) + 1, 3)];
}
export function temporalProjection(
  camera: TelosCamera,
  width: number,
  height: number,
  index: number,
  output = new Matrix4(),
) {
  const [x, y] = temporalJitter(index);
  return output
    .makeTranslation((2 * x) / width, (-2 * y) / height, 0)
    .multiply(camera.viewProjection);
}
export function temporalMemoryBytes(width: number, height: number) {
  return width * height * 12;
}

/** Optional shared resolve owner. One source color, one history color and one previous depth. */
export class TelosTemporal {
  private source?: GPUTexture;
  private history?: GPUTexture;
  private previousDepth?: GPUTexture;
  private binding?: GPUBindGroup;
  private generation = -1;
  private valid = false;
  private samples = 0;
  private sequence = 0;
  private logical = new Matrix4();
  private projectionKey = "";
  private readonly previous = new Matrix4();
  private readonly inversePrevious = new Matrix4();
  private readonly data = new Float32Array(56);
  private readonly uniform: GPUBuffer;
  private readonly sampler: GPUSampler;
  private readonly query?: GPUQuerySet;
  private readonly queryResolve?: GPUBuffer;
  private readonly queryRead?: GPUBuffer;
  private debug: TelosAADebug = "color";
  private mode: TelosAAMode = "SpatialOnly";
  readonly renderProjection = new Matrix4();
  readonly inverseRenderProjection = new Matrix4();
  lastResetReason = "initial";
  resolveEncodeMs = 0;
  reprojectionCpuMs = 0;
  static async create(owner: TelosDevice): Promise<TelosTemporal | undefined> {
    if (!owner.shaders.temporal) return;
    owner.device.pushErrorScope("validation");
    let scopeOpen = true;
    try {
      const module = owner.device.createShaderModule({
        label: "Telos temporal resolve",
        code: owner.shaders.temporal,
      });
      const pipeline = await owner.device.createRenderPipelineAsync({
        label: "Telos light temporal resolve",
        layout: "auto",
        vertex: { module, entryPoint: "resolveVertex" },
        fragment: {
          module,
          entryPoint: "resolveFragment",
          targets: [{ format: owner.format }],
        },
        primitive: { topology: "triangle-list" },
      });
      const error = await owner.device.popErrorScope();
      scopeOpen = false;
      if (error) throw new Error(error.message);
      return new TelosTemporal(owner, pipeline);
    } catch (error) {
      // Pipeline failure must preserve viewport startup and the existing spatial path.
      if (scopeOpen) await owner.device.popErrorScope().catch(() => undefined);
      owner.diagnostic("telos-temporal-unavailable: " + String(error));
      return;
    }
  }
  private constructor(
    private readonly owner: TelosDevice,
    private readonly pipeline: GPURenderPipeline,
  ) {
    this.uniform = owner.resources.buffer(this.data, GPUBufferUsage.UNIFORM);
    this.sampler = owner.device.createSampler({
      magFilter: "linear",
      minFilter: "linear",
    });
    if (owner.device.features.has("timestamp-query")) {
      this.query = owner.device.createQuerySet({ type: "timestamp", count: 2 });
      this.queryResolve = owner.resources.buffer(
        new Uint32Array(4),
        GPUBufferUsage.QUERY_RESOLVE | GPUBufferUsage.COPY_SRC,
      );
      this.queryRead = owner.device.createBuffer({
        size: 16,
        usage: GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ,
      });
      owner.resources.buffers.add(this.queryRead);
    }
  }
  reset(reason: string) {
    this.valid = false;
    this.samples = 0;
    this.sequence = 0;
    this.lastResetReason = reason;
  }
  get needsFrame() {
    return this.samples < 8;
  }
  get historyValid() {
    return this.valid;
  }
  prepare(
    frame: TelosFrame,
    camera: TelosCamera,
    mode: TelosAAMode,
    debug: TelosAADebug,
  ) {
    const start = performance.now();
    if (mode !== this.mode) this.reset("mode");
    this.mode = mode;
    this.debug = debug;
    if (frame.generation !== this.generation) {
      this.releaseTextures();
      const size = [frame.width, frame.height];
      this.source = this.owner.resources.texture({
        label: "Telos temporal source",
        size,
        format: this.owner.format,
        usage:
          GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.TEXTURE_BINDING,
      });
      this.history = this.owner.resources.texture({
        label: "Telos temporal history",
        size,
        format: this.owner.format,
        usage: GPUTextureUsage.COPY_DST | GPUTextureUsage.TEXTURE_BINDING,
      });
      this.previousDepth = this.owner.resources.texture({
        label: "Telos previous depth",
        size,
        format: "depth32float",
        usage: GPUTextureUsage.COPY_DST | GPUTextureUsage.TEXTURE_BINDING,
      });
      this.binding = this.owner.device.createBindGroup({
        layout: this.pipeline.getBindGroupLayout(0),
        entries: [
          { binding: 0, resource: this.source.createView() },
          { binding: 1, resource: frame.depthView },
          { binding: 2, resource: this.history.createView() },
          { binding: 3, resource: this.previousDepth.createView() },
          { binding: 4, resource: this.sampler },
          { binding: 5, resource: { buffer: this.uniform } },
        ],
      });
      this.generation = frame.generation;
      this.reset("viewport/DPR");
    }
    const key = [
      camera.mode,
      camera.fov,
      camera.span,
      camera.near,
      camera.far,
      frame.dpr,
    ].join(":");
    if (key !== this.projectionKey) this.reset("projection/DPR");
    this.projectionKey = key;
    if (!this.logical.equals(camera.viewProjection)) this.samples = 0;
    this.logical.copy(camera.viewProjection);
    temporalProjection(
      camera,
      frame.width,
      frame.height,
      this.sequence,
      this.renderProjection,
    );
    this.inverseRenderProjection.copy(this.renderProjection).invert();
    this.reprojectionCpuMs = performance.now() - start;
    return this.source!;
  }
  resolve(frame: TelosFrame) {
    const start = performance.now();
    this.data.set(this.inverseRenderProjection.elements, 0);
    this.data.set(this.previous.elements, 16);
    this.data.set(this.inversePrevious.elements, 32);
    this.data.set(
      [
        frame.width,
        frame.height,
        this.valid ? 1 : 0,
        this.mode === "TAA" ? 1 : 2,
      ],
      48,
    );
    this.data.set([debugNames.indexOf(this.debug), 0, 0, 0], 52);
    this.owner.device.queue.writeBuffer(this.uniform, 0, this.data);
    frame.colorView = frame.target.createView();
    const pass = frame.encoder.beginRenderPass({
      label: "TemporalResolve",
      timestampWrites: this.query
        ? {
            querySet: this.query,
            beginningOfPassWriteIndex: 0,
            endOfPassWriteIndex: 1,
          }
        : undefined,
      colorAttachments: [
        {
          view: frame.colorView,
          loadOp: "clear",
          clearValue: [0, 0, 0, 1],
          storeOp: "store",
        },
      ],
    });
    pass.setPipeline(this.pipeline);
    pass.setBindGroup(0, this.binding!);
    pass.draw(3);
    pass.end();
    if (this.query) {
      frame.encoder.resolveQuerySet(this.query, 0, 2, this.queryResolve!, 0);
      frame.encoder.copyBufferToBuffer(
        this.queryResolve!,
        0,
        this.queryRead!,
        0,
        16,
      );
    }
    // Capture surface history before crisp grid, topology, authoring and DOM overlays.
    // Debug pixels must never contaminate color history.
    if (this.debug === "color") {
      frame.encoder.copyTextureToTexture(
        { texture: frame.target },
        { texture: this.history! },
        [frame.width, frame.height],
      );
      frame.encoder.copyTextureToTexture(
        { texture: frame.depth!, aspect: "depth-only" },
        { texture: this.previousDepth!, aspect: "depth-only" },
        [frame.width, frame.height],
      );
      this.valid = true;
      this.previous.copy(this.renderProjection);
      this.inversePrevious.copy(this.inverseRenderProjection);
    }
    this.samples++;
    this.sequence = (this.sequence + 1) % 8;
    this.resolveEncodeMs = performance.now() - start;
  }
  /** Explicit qualification readback. Await it before submitting the next frame. */
  async readGpuMilliseconds(): Promise<number | null> {
    if (!this.queryRead) return null;
    await this.queryRead.mapAsync(GPUMapMode.READ);
    const stamps = new BigUint64Array(this.queryRead.getMappedRange());
    const milliseconds = Number(stamps[1] - stamps[0]) / 1e6;
    this.queryRead.unmap();
    return milliseconds;
  }
  private releaseTextures() {
    for (const texture of [this.source, this.history, this.previousDepth])
      if (texture) this.owner.resources.release(texture);
    this.source = this.history = this.previousDepth = undefined;
    this.binding = undefined;
  }
  dispose() {
    this.releaseTextures();
    this.owner.resources.release(this.uniform);
    this.query?.destroy();
    if (this.queryResolve) this.owner.resources.release(this.queryResolve);
    if (this.queryRead) this.owner.resources.release(this.queryRead);
  }
}
