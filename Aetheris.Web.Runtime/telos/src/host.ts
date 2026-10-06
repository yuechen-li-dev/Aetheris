import { Matrix4, Vector3, Box3 } from "three";
import { TelosCamera } from "./camera.js";
import { TelosDevice, TelosFrame } from "./device.js";
import { TelosPick } from "./pick.js";
import { attachNavigation } from "./navigation.js";
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
  gridPlane: "xy" | "xz" = "xz";
  readonly geometry = new Map<string, GeometryBuffers>();
  private meshes: Draw<TelosMesh>[] = [];
  private lines: Draw<TelosLine>[] = [];
  private fields: Draw<TelosField>[] = [];
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
  };
  static async create(
    canvas: HTMLCanvasElement,
    diagnostic?: (message: string) => void,
  ) {
    const owner = await TelosDevice.create(canvas, diagnostic);
    return new TelosHost(owner);
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
    this.releaseDraws();
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
        if (repeated) throw new Error('telos-definition-identity-collision: ' + definition.id);
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
      const draw = this.draw(
        item,
        telosPipeline(this.owner, item.overlay ? "overlay-mesh" : "mesh"),
        224,
        geometry.count,
      );
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
      const draw = this.draw(
        item,
        telosPipeline(this.owner, item.overlay ? "overlay-line" : "line"),
        160,
        segments.length / 6,
      );
      draw.vertices = resources.buffer(
        new Float32Array(segments),
        GPUBufferUsage.VERTEX,
      );
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
      const draw = this.draw(
        item,
        telosPipeline(this.owner, "mesh", item.artifact),
        32,
        6,
      );
      draw.vertices = resources.buffer(
        new Float32Array(108),
        GPUBufferUsage.VERTEX,
      );
      this.fields.push(draw);
    }
    this.metrics.fieldPipelineMs = performance.now() - fieldStart;
    this.picker.setScene(scene);
    this.invalidate();
  }
  fit() {
    const bounds = new Box3();
    for (const item of this.scene.meshes) {
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
    data.set(this.camera.viewProjection.elements);
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
    data.set([material.roughness, material.metallic, 0, 0], 52);
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
        0,
      ],
      36,
    );
    this.owner.device.queue.writeBuffer(draw.uniform, 0, data);
  }
  private updateField(draw: Draw<TelosField>) {
    const inverse = matrix(draw.item.transform).invert(),
      localVP = this.camera.viewProjection
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
        const origin = this.camera.unproject(pixel, 0).applyMatrix4(inverse),
          far = this.camera.unproject(pixel, 1).applyMatrix4(inverse);
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
    this.updateGrid();
    this.frame.begin(this.background);
    this.metrics.draws = 0;
    if (this.gridDraw) {
      const grid = this.frame.pass("ReferenceGrid");
      this.updateLine(this.gridDraw);
      grid.setPipeline(this.gridDraw.pipeline);
      grid.setBindGroup(0, this.gridDraw.binding);
      grid.setVertexBuffer(0, this.gridDraw.vertices!);
      grid.draw(6, this.gridDraw.count);
      grid.end();
    }
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
    for (const draw of this.lines.filter((draw) => draw.item.overlay)) {
      this.updateLine(draw);
      pass.setPipeline(draw.pipeline);
      pass.setBindGroup(0, draw.binding);
      pass.setVertexBuffer(0, draw.vertices!);
      pass.draw(6, draw.count);
    }
    pass.end();
    this.frame.end();
    this.metrics.frames++;
    this.metrics.frameMs = performance.now() - started;
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
    this.releaseDraws();
    this.geometry.clear();
    this.frame.dispose();
    this.owner.dispose();
  }
}
