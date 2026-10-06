import { Matrix4, Vector3, Box3 } from "three";
import { TelosCamera } from "./camera.js";
import { TelosDevice, TelosFrame } from "./device.js";
import { TelosPick } from "./pick.js";
import { attachNavigation } from "./navigation.js";
import { TelosTemporal, type TelosAAMode, type TelosAADebug } from "./temporal.js";
import { telosPipeline } from "./pipelines.js";
import type {
  TelosScene,
  TelosMesh,
  TelosLine,
  TelosField,
  TelosGeometry,
  NumericArray,
} from "./contracts.js";

type GeometryBuffers = {
  definition: TelosGeometry;
  positions: GPUBuffer;
  normals: GPUBuffer;
  indices: GPUBuffer;
  count: number;
};
type Draw<T> = {
  item: T;
  uniform: GPUBuffer;
  binding: GPUBindGroup;
  pipeline: GPURenderPipeline;
  vertices?: GPUBuffer;
  geometry?: GeometryBuffers;
  count: number;
};
const meshKey = (item: TelosMesh) =>
  JSON.stringify([
    item.identity.occurrenceId,
    item.identity.overlayId,
    item.definition.id,
  ]);
const emptyScene = (): TelosScene => ({ meshes: [], lines: [], fields: [] });
const matrix = (values?: NumericArray) =>
  values ? new Matrix4().fromArray(values) : new Matrix4();

export class TelosHost {
  readonly camera = new TelosCamera();
  readonly picker = new TelosPick(this.camera);
  readonly frame: TelosFrame;
  scene = emptyScene();
  background: GPUColor = [0.08, 0.1, 0.09, 1];
  grid = false;
  temporal?: TelosTemporal;
  aaMode: TelosAAMode = "SpatialOnly";
  aaDebug: TelosAADebug = "color";
  setAA(mode: TelosAAMode, debug: TelosAADebug = "color") {
    if ((mode === "TAA" || mode === "TAAUtility") && !this.temporal) {
      this.owner.diagnostic("telos-temporal-unavailable: retaining spatial AA");
      mode = "SpatialOnly";
    }
    if (mode !== this.aaMode) this.temporal?.reset("mode");
    this.aaMode = mode;
    this.aaDebug = debug;
    this.invalidate();
  }
  private get temporalActive() {
    return this.temporal && (this.aaMode === "TAA" || this.aaMode === "TAAUtility");
  }
  private get renderProjection() {
    return this.temporalActive ? this.temporal!.renderProjection : this.camera.viewProjection;
  }
  gridPlane: "xy" | "xz" = "xz";
  readonly geometry = new Map<string, GeometryBuffers>();
  private meshes: Draw<TelosMesh>[] = [];
  private lines: Draw<TelosLine>[] = [];
  private fields: Draw<TelosField>[] = [];
  private dynamicLines = new Map<string, Draw<TelosLine>>();
  private frameListeners = new Set<() => void>();
  /** Called with current camera matrices before drawing. No separate projection loop. */
  beforeFrame(listener: () => void) {
    this.frameListeners.add(listener);
    this.invalidate();
    return () => {
      this.frameListeners.delete(listener);
    };
  }
  /** Retained world-space lines, intended for projected leaders and interactive previews.
   * A stable ID and segment count retain both vertex and uniform buffers. */
  setDynamicLines(items: readonly TelosLine[]) {
    const started = performance.now();
    const used = new Set<string>();
    for (const item of items) {
      if (used.has(item.id))
        throw new Error("telos-dynamic-line-identity-collision: " + item.id);
      used.add(item.id);
      const count = Math.max(0, item.points.length / 3 - 1);
      if (!Number.isInteger(count))
        throw new Error("telos-line-points-invalid");
      const pipeline = telosPipeline(
        this.owner,
        "overlay-line",
        undefined,
        item.depthMode,
      );
      let draw = this.dynamicLines.get(item.id);
      if (draw && (draw.count !== count || draw.pipeline !== pipeline)) {
        this.owner.resources.release(draw.uniform);
        this.owner.resources.release(draw.vertices!);
        this.dynamicLines.delete(item.id);
        draw = undefined;
      }
      if (!count) continue;
      if (!draw) {
        draw = this.draw(item, pipeline, 160, count);
        draw.vertices = this.owner.resources.buffer(
          new Float32Array(count * 6),
          GPUBufferUsage.VERTEX,
        );
        this.dynamicLines.set(item.id, draw);
      }
      draw.item = item;
      const points = new Float32Array(count * 6);
      for (let i = 0; i < count; i++)
        for (let j = 0; j < 6; j++) points[i * 6 + j] = item.points[i * 3 + j];
      this.owner.device.queue.writeBuffer(draw.vertices!, 0, points);
    }
    for (const [id, draw] of this.dynamicLines)
      if (!used.has(id)) {
        this.owner.resources.release(draw.uniform);
        this.owner.resources.release(draw.vertices!);
        this.dynamicLines.delete(id);
      }
    this.picker.dynamicLines = items;
    this.metrics.dynamicLineUpdateMs = performance.now() - started;
  }
  private gridDraw?: Draw<TelosLine>;
  private gridKey = "";
  private raf = 0;
  private disposed = false;
  private detach?: () => void;
  private observer?: ResizeObserver;
  metrics = {
    meshUploadMs: 0,
    lineUploadMs: 0,
    fieldPipelineMs: 0,
    frameMs: 0,
    frames: 0,
    draws: 0,
    overlayUpdateMs: 0,
    dynamicLineUpdateMs: 0,
  };
  static async create(
    canvas: HTMLCanvasElement,
    diagnostic?: (message: string) => void,
  ) {
    const owner = await TelosDevice.create(canvas, diagnostic);
    const host = new TelosHost(owner);
    host.temporal = await TelosTemporal.create(owner);
    return host;
  }
  constructor(readonly owner: TelosDevice) {
    this.frame = new TelosFrame(owner);
    this.frame.resize(1, 1);
    owner.onLost = () => {
      cancelAnimationFrame(this.raf);
      this.raf = 0;
    };
  }
  /** A product supplies DOM inputs; all GPU sizing and scheduling stay here. */
  attach(container: HTMLElement) {
    this.detach?.();
    this.observer?.disconnect();
    this.detach = attachNavigation(this.owner.canvas, this.camera, () =>
      this.invalidate(),
    );
    this.observer = new ResizeObserver(() => this.invalidate());
    this.observer.observe(container);
    this.invalidate();
  }
  resize(width: number, height: number, dpr = 1) {
    this.camera.resize(width, height);
    this.frame.resize(width, height, dpr);
  }
  invalidate() {
    if (this.disposed || this.owner.stopped || this.raf) return;
    this.raf = requestAnimationFrame(() => {
      this.raf = 0;
      try {
        const canvas = this.owner.canvas;
        this.resize(
          canvas.clientWidth || this.camera.width,
          canvas.clientHeight || this.camera.height,
          globalThis.devicePixelRatio || 1,
        );
        this.render();
      } catch (error) {
        this.owner.diagnostic(String(error));
        this.owner.stopped = true;
      }
    });
  }
  private draw<T>(
    item: T,
    pipeline: GPURenderPipeline,
    bytes: number,
    count: number,
  ): Draw<T> {
    const uniform = this.owner.resources.buffer(
      new Float32Array(bytes / 4),
      GPUBufferUsage.UNIFORM,
    );
    const binding = this.owner.device.createBindGroup({
      layout: pipeline.getBindGroupLayout(0),
      entries: [{ binding: 0, resource: { buffer: uniform } }],
    });
    return { item, pipeline, uniform, binding, count };
  }
  setScene(scene: TelosScene) {
    // No prior per-occurrence transforms are retained yet. Reset instead of assuming camera-only motion.
    this.temporal?.reset("scene/appearance/occurrence");
    const previous = [...this.meshes, ...this.lines, ...this.fields];
    const retained = new Set<
      Draw<TelosMesh> | Draw<TelosLine> | Draw<TelosField>
    >();
    const oldMeshes = new Map(
      this.meshes.map((draw) => [meshKey(draw.item), draw]),
    );
    const oldLines = new Map(this.lines.map((draw) => [draw.item.id, draw]));
    const oldFields = new Map(
      this.fields.map((draw) => [
        draw.item.identity.occurrenceId + ":" + draw.item.artifact.shaderId,
        draw,
      ]),
    );
    this.meshes = [];
    this.lines = [];
    this.fields = [];
    this.scene = scene;
    const resources = this.owner.resources,
      used = new Set<string>();
    const start = performance.now();
    for (const item of scene.meshes) {
      const definition = item.definition;
      const repeated = used.has(definition.id);
      used.add(definition.id);
      let geometry = this.geometry.get(definition.id);
      if (
        geometry &&
        (geometry.definition.positions !== definition.positions ||
          geometry.definition.indices !== definition.indices ||
          geometry.definition.normals !== definition.normals)
      ) {
        if (repeated)
          throw new Error(
            "telos-definition-identity-collision: " + definition.id,
          );
        this.releaseGeometry(geometry);
        this.geometry.delete(definition.id);
        geometry = undefined;
      }
      if (!geometry) {
        geometry = {
          definition,
          positions: resources.buffer(
            Float32Array.from(definition.positions),
            GPUBufferUsage.VERTEX,
          ),
          normals: resources.buffer(
            Float32Array.from(definition.normals),
            GPUBufferUsage.VERTEX,
          ),
          indices: resources.buffer(
            Uint32Array.from(definition.indices),
            GPUBufferUsage.INDEX,
          ),
          count: definition.indices.length,
        };
        this.geometry.set(definition.id, geometry);
      }
      const pipeline = telosPipeline(
        this.owner,
        item.overlay ? "overlay-mesh" : "mesh",
        undefined,
        item.depthMode,
      );
      const old = oldMeshes.get(meshKey(item));
      const draw =
        old?.pipeline === pipeline
          ? old
          : this.draw(item, pipeline, 224, geometry.count);
      oldMeshes.delete(meshKey(item));
      draw.item = item;
      draw.count = geometry.count;
      retained.add(draw);
      draw.geometry = geometry;
      this.meshes.push(draw);
    }
    for (const [id, geometry] of this.geometry)
      if (!used.has(id)) {
        this.releaseGeometry(geometry);
        this.geometry.delete(id);
      }
    this.metrics.meshUploadMs = performance.now() - start;
    const lineStart = performance.now();
    for (const item of scene.lines) {
      if (item.points.length < 6) continue;
      const segments: number[] = [];
      for (let i = 3; i < item.points.length; i += 3)
        for (let j = i - 3; j < i + 3; j++) segments.push(item.points[j]);
      const pipeline = telosPipeline(
        this.owner,
        item.overlay ? "overlay-line" : "line",
        undefined,
        item.depthMode,
      );
      const old = oldLines.get(item.id);
      oldLines.delete(item.id);
      const reusable =
        old?.pipeline === pipeline && old.count === segments.length / 6;
      const draw = reusable
        ? old
        : this.draw(item, pipeline, 160, segments.length / 6);
      if (!reusable)
        draw.vertices = resources.buffer(
          new Float32Array(segments),
          GPUBufferUsage.VERTEX,
        );
      else if (old.item.points !== item.points)
        this.owner.device.queue.writeBuffer(
          draw.vertices!,
          0,
          new Float32Array(segments),
        );
      draw.item = item;
      retained.add(draw);
      this.lines.push(draw);
    }
    this.metrics.lineUploadMs = performance.now() - lineStart;
    const fieldStart = performance.now();
    for (const item of scene.fields) {
      if (
        item.artifact.bindings.length !== 1 ||
        item.artifact.bindings[0].group !== 0 ||
        item.artifact.bindings[0].binding !== 0 ||
        item.artifact.bindings[0].byteSize !== 32 ||
        !item.artifact.capabilities.includes("telos-field-rays/1")
      )
        throw new Error("telos-field-abi-unsupported");
      const model = matrix(item.transform),
        e = model.elements;
      const axes = [
        new Vector3(e[0], e[1], e[2]),
        new Vector3(e[4], e[5], e[6]),
        new Vector3(e[8], e[9], e[10]),
      ];
      if (
        axes.some((axis) => Math.abs(axis.length() - 1) > 1e-6) ||
        Math.abs(axes[0].dot(axes[1])) > 1e-6 ||
        Math.abs(axes[0].dot(axes[2])) > 1e-6 ||
        Math.abs(axes[1].dot(axes[2])) > 1e-6
      )
        throw new Error("telos-field-transform-not-rigid");
      const pipeline = telosPipeline(this.owner, "mesh", item.artifact);
      const old = oldFields.get(
        item.identity.occurrenceId + ":" + item.artifact.shaderId,
      );
      const draw =
        old?.pipeline === pipeline ? old : this.draw(item, pipeline, 32, 6);
      if (draw !== old)
        draw.vertices = resources.buffer(
          new Float32Array(108),
          GPUBufferUsage.VERTEX,
        );
      draw.item = item;
      retained.add(draw);
      this.fields.push(draw);
    }
    this.metrics.fieldPipelineMs = performance.now() - fieldStart;
    for (const draw of previous)
      if (!retained.has(draw)) {
        resources.release(draw.uniform);
        if (draw.vertices) resources.release(draw.vertices);
      }
    this.picker.setScene(scene);
    this.invalidate();
  }
  fit() {
    const bounds = new Box3();
    const modelMeshes = this.scene.meshes.filter((item) => !item.overlay);
    for (const item of modelMeshes.length ? modelMeshes : this.scene.meshes) {
      const transform = matrix(item.transform),
        p = item.definition.positions;
      for (let i = 0; i < p.length; i += 3)
        bounds.expandByPoint(
          new Vector3(p[i], p[i + 1], p[i + 2]).applyMatrix4(transform),
        );
    }
    for (const field of this.scene.fields)
      bounds.union(
        new Box3(
          new Vector3(...field.bounds.minimum),
          new Vector3(...field.bounds.maximum),
        ).applyMatrix4(matrix(field.transform)),
      );
    if (!bounds.isEmpty())
      this.camera.fit(bounds.min.toArray(), bounds.max.toArray());
    this.invalidate();
  }
  private updateMesh(draw: Draw<TelosMesh>) {
    const model = matrix(draw.item.transform);
    const data = new Float32Array(56);
    data.set(draw.item.overlay ? this.camera.viewProjection.elements : this.renderProjection.elements);
    data.set(model.elements, 16);
    data.set(model.clone().invert().transpose().elements, 32);
    const material = draw.item.material;
    data.set(
      draw.item.selected
        ? [0.8, 0.55, 0.12, 1]
        : draw.item.hovered
          ? [0.25, 0.65, 0.48, 1]
          : [...material.baseColor.slice(0, 3), material.opacity],
      48,
    );
    data.set(
      [material.roughness, material.metallic, draw.item.unlit ? 1 : 0, 0],
      52,
    );
    this.owner.device.queue.writeBuffer(draw.uniform, 0, data);
  }
  private updateLine(draw: Draw<TelosLine>) {
    const data = new Float32Array(40);
    data.set(this.camera.viewProjection.elements);
    data.set(matrix(draw.item.transform).elements, 16);
    data.set(
      draw.item.selected
        ? [1, 0.75, 0.2, 1]
        : (draw.item.color ?? [0.15, 0.18, 0.16, 1]),
      32,
    );
    data.set(
      [
        this.camera.width,
        this.camera.height,
        draw.item.selected
          ? Math.max(3, draw.item.widthPixels ?? 1)
          : (draw.item.widthPixels ?? 1),
        draw.item.depthMode === "depth-tested" ? 0 : 0.000002,
      ],
      36,
    );
    this.owner.device.queue.writeBuffer(draw.uniform, 0, data);
  }
  private updateField(draw: Draw<TelosField>) {
    const inverse = matrix(draw.item.transform).invert(),
      localVP = this.renderProjection
        .clone()
        .multiply(matrix(draw.item.transform));
    const m = localVP.elements,
      min = draw.item.bounds.minimum,
      max = draw.item.bounds.maximum;
    const epsilon = Math.max(
      1e-6,
      new Vector3(...min).distanceTo(new Vector3(...max)) * 1e-5,
    );
    const corners: [number, number][] = [
      [0, this.camera.height],
      [this.camera.width, this.camera.height],
      [0, 0],
      [0, 0],
      [this.camera.width, this.camera.height],
      [this.camera.width, 0],
    ];
    const vertices = new Float32Array(
      corners.flatMap((pixel) => {
        const unproject = (depth: number) => new Vector3(
          (pixel[0] / this.camera.width) * 2 - 1,
          1 - (pixel[1] / this.camera.height) * 2,
          depth,
        ).applyMatrix4(this.temporalActive ? this.temporal!.inverseRenderProjection : this.camera.inverseViewProjection);
        const origin = unproject(0).applyMatrix4(inverse),
          far = unproject(1).applyMatrix4(inverse);
        return [
          (pixel[0] / this.camera.width) * 2 - 1,
          1 - (pixel[1] / this.camera.height) * 2,
          0,
          ...origin.toArray(),
          ...far.sub(origin).toArray(),
          m[2],
          m[6],
          m[10],
          m[14],
          m[3],
          m[7],
          m[11],
          m[15],
          epsilon,
        ];
      }),
    );
    this.owner.device.queue.writeBuffer(draw.vertices!, 0, vertices);
    const data = new Float32Array(8);
    data.set(
      draw.item.selected
        ? [0.8, 0.55, 0.12, 1]
        : [
            ...draw.item.material.baseColor.slice(0, 3),
            draw.item.material.opacity,
          ],
      0,
    );
    data[4] = draw.item.material.roughness;
    this.owner.device.queue.writeBuffer(draw.uniform, 0, data);
  }
  private updateGrid() {
    const key = JSON.stringify([
      this.grid,
      this.gridPlane,
      this.camera.viewProjection.elements,
      this.camera.span,
      this.camera.width,
      this.camera.height,
    ]);
    if (key === this.gridKey) return;
    this.gridKey = key;
    if (this.gridDraw) {
      this.owner.resources.release(this.gridDraw.uniform);
      this.owner.resources.release(this.gridDraw.vertices!);
      this.gridDraw = undefined;
    }
    if (!this.grid) return;
    const axis = this.gridPlane === "xy" ? "z" : "y",
      points: Vector3[] = [];
    for (const pixel of [
      [0, 0],
      [this.camera.width, 0],
      [0, this.camera.height],
      [this.camera.width, this.camera.height],
    ] as [number, number][]) {
      const ray = this.camera.worldRay(pixel),
        t = -ray.origin[axis] / ray.direction[axis];
      if (Number.isFinite(t) && t > 0 && t < this.camera.far)
        points.push(ray.origin.clone().addScaledVector(ray.direction, t));
    }
    const span = Math.max(
      this.camera.span,
      this.camera.position.distanceTo(this.camera.target) * 0.5,
      1e-8,
    );
    const extent = Math.min(
      span * 20,
      Math.max(span, ...points.map((p) => p.distanceTo(this.camera.target))),
    );
    const step = Math.pow(10, Math.floor(Math.log10(extent / 12)));
    const centerX = Math.round(this.camera.target.x / step) * step;
    const centerY =
      Math.round(
        (this.gridPlane === "xy"
          ? this.camera.target.y
          : this.camera.target.z) / step,
      ) * step;
    const count = Math.min(40, Math.ceil(extent / step));
    const segments: number[] = [];
    const point = (x: number, y: number) =>
      this.gridPlane === "xy" ? [x, y, 0] : [x, 0, y];
    for (let i = -count; i <= count; i++) {
      segments.push(
        ...point(centerX + i * step, centerY - count * step),
        ...point(centerX + i * step, centerY + count * step),
      );
      segments.push(
        ...point(centerX - count * step, centerY + i * step),
        ...point(centerX + count * step, centerY + i * step),
      );
    }
    this.gridDraw = this.draw(
      {
        id: "reference-grid",
        points: segments,
        identity: { occurrenceId: "reference-grid" },
        widthPixels: 0.7,
        color: [0.45, 0.48, 0.46, 0.18],
      },
      telosPipeline(this.owner, "line"),
      160,
      segments.length / 6,
    );
    this.gridDraw.vertices = this.owner.resources.buffer(
      new Float32Array(segments),
      GPUBufferUsage.VERTEX,
    );
  }
  render() {
    if (this.owner.stopped || this.disposed) return;
    cancelAnimationFrame(this.raf);
    this.raf = 0;
    const started = performance.now();
    this.camera.update();
    const overlayStart = performance.now();
    for (const listener of this.frameListeners) listener();
    this.metrics.overlayUpdateMs = performance.now() - overlayStart;
    this.updateGrid();
    const temporal = this.temporalActive ? this.temporal : undefined;
    const source = temporal?.prepare(this.frame, this.camera, this.aaMode, this.aaDebug);
    this.frame.begin(this.background, source);
    this.metrics.draws = 0;
    const drawGrid = () => {
      if (!this.gridDraw) return;
      const grid = this.frame.pass("ReferenceGrid");
      this.updateLine(this.gridDraw);
      grid.setPipeline(this.gridDraw.pipeline);
      grid.setBindGroup(0, this.gridDraw.binding);
      grid.setVertexBuffer(0, this.gridDraw.vertices!);
      grid.draw(6, this.gridDraw.count);
      grid.end();
    };
    const drawMesh = (pass: GPURenderPassEncoder, draw: Draw<TelosMesh>) => {
      if (draw.item.visible === false) return;
      this.updateMesh(draw);
      pass.setPipeline(draw.pipeline);
      pass.setBindGroup(0, draw.binding);
      pass.setVertexBuffer(0, draw.geometry!.positions);
      pass.setVertexBuffer(1, draw.geometry!.normals);
      pass.setIndexBuffer(draw.geometry!.indices, "uint32");
      pass.drawIndexed(draw.count);
      this.metrics.draws++;
    };
    let pass = this.frame.pass("MeshOpaque");
    for (const draw of this.meshes.filter((draw) => !draw.item.overlay))
      drawMesh(pass, draw);
    pass.end();
    pass = this.frame.pass("Field");
    for (const draw of this.fields) {
      this.updateField(draw);
      pass.setPipeline(draw.pipeline);
      pass.setBindGroup(0, draw.binding);
      pass.setVertexBuffer(0, draw.vertices!);
      pass.draw(6);
      this.metrics.draws++;
    }
    pass.end();
    temporal?.resolve(this.frame);
    drawGrid();
    pass = this.frame.pass("Topology");
    for (const draw of this.lines.filter((draw) => !draw.item.overlay)) {
      if (draw.item.visible === false) continue;
      this.updateLine(draw);
      pass.setPipeline(draw.pipeline);
      pass.setBindGroup(0, draw.binding);
      pass.setVertexBuffer(0, draw.vertices!);
      pass.draw(6, draw.count);
      this.metrics.draws++;
    }
    pass.end();
    pass = this.frame.pass("Overlay");
    for (const draw of this.meshes.filter((draw) => draw.item.overlay))
      drawMesh(pass, draw);
    for (const draw of [
      ...this.lines.filter((draw) => draw.item.overlay),
      ...this.dynamicLines.values(),
    ]) {
      if (draw.item.visible === false) continue;
      this.updateLine(draw);
      pass.setPipeline(draw.pipeline);
      pass.setBindGroup(0, draw.binding);
      pass.setVertexBuffer(0, draw.vertices!);
      pass.draw(6, draw.count);
      this.metrics.draws++;
    }
    pass.end();
    this.frame.end();
    this.metrics.frames++;
    this.metrics.frameMs = performance.now() - started;
    if (temporal?.needsFrame) this.invalidate();
  }
  /** Qualification readback uses the authoritative presented texture. */
  async readPixels(): Promise<Uint8Array> {
    const { width, height, target } = this.frame;
    const bytesPerRow = Math.ceil((width * 4) / 256) * 256;
    const buffer = this.owner.device.createBuffer({
      size: bytesPerRow * height,
      usage: GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ,
    });
    const encoder = this.owner.device.createCommandEncoder();
    encoder.copyTextureToBuffer({ texture: target }, { buffer, bytesPerRow }, [
      width,
      height,
    ]);
    this.owner.device.queue.submit([encoder.finish()]);
    await buffer.mapAsync(GPUMapMode.READ);
    const input = new Uint8Array(buffer.getMappedRange()),
      output = new Uint8Array(width * height * 4);
    for (let y = 0; y < height; y++)
      output.set(
        input.subarray(y * bytesPerRow, y * bytesPerRow + width * 4),
        y * width * 4,
      );
    buffer.unmap();
    buffer.destroy();
    return output;
  }
  private releaseGeometry(geometry: GeometryBuffers) {
    for (const buffer of [
      geometry.positions,
      geometry.normals,
      geometry.indices,
    ])
      this.owner.resources.release(buffer);
  }
  private releaseDraws() {
    for (const draw of [...this.meshes, ...this.lines, ...this.fields]) {
      this.owner.resources.release(draw.uniform);
      if (draw.vertices) this.owner.resources.release(draw.vertices);
    }
    this.meshes = [];
    this.lines = [];
    this.fields = [];
  }
  dispose() {
    if (this.disposed) return;
    this.disposed = true;
    cancelAnimationFrame(this.raf);
    this.raf = 0;
    this.observer?.disconnect();
    this.detach?.();
    this.picker.dispose();
    this.setDynamicLines([]);
    this.frameListeners.clear();
    this.releaseDraws();
    this.geometry.clear();
    this.temporal?.dispose();
    this.frame.dispose();
    this.owner.dispose();
  }
}
