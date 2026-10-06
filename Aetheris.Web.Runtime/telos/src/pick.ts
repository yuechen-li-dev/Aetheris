import {
  BufferGeometry,
  BufferAttribute,
  Mesh,
  MeshBasicMaterial,
  DoubleSide,
  Matrix4,
  Raycaster,
  Vector3,
} from "three";
import type { Intersection } from "three";
import type { TelosCamera } from "./camera.js";
import type {
  TelosScene,
  TelosLine,
  Pixel,
  TelosHit,
  TelosMesh,
  TelosField,
  TelosGeometry,
} from "./contracts.js";

/** CPU engineering proxy; field objects may supply the same mesh proxy without drawing it. */
export class TelosPick {
  camera: TelosCamera;
  proxies: Mesh[];
  lines: readonly TelosLine[];
  raycaster: Raycaster;
  constructor(camera: TelosCamera) {
    this.camera = camera;
    this.proxies = [];
    this.lines = [];
    this.raycaster = new Raycaster();
  }
  setScene(scene: TelosScene) {
    this.dispose();
    this.lines = scene.lines ?? [];
    for (const item of [
      ...(scene.meshes ?? []),
      ...(scene.fields ?? []).filter((field) => field.proxy),
    ]) {
      const definition = "definition" in item ? item.definition : item.proxy!;
      const geometry = new BufferGeometry();
      geometry.setAttribute(
        "position",
        new BufferAttribute(Float32Array.from(definition.positions), 3),
      );
      geometry.setIndex(
        new BufferAttribute(Uint32Array.from(definition.indices), 1),
      );
      const mesh = new Mesh(
        geometry,
        new MeshBasicMaterial({ side: DoubleSide }),
      );
      mesh.matrixAutoUpdate = false;
      mesh.matrix.fromArray(item.transform ?? new Matrix4().elements);
      mesh.updateMatrixWorld(true);
      mesh.userData = { item, definition };
      this.proxies.push(mesh);
    }
  }
  pick(pixel: Pixel, mode = "face"): TelosHit | null {
    const ray = this.camera.worldRay(pixel);
    this.raycaster.ray.set(ray.origin, ray.direction);
    const closest = this.raycaster.intersectObjects(this.proxies, false)[0];
    const faceHit = closest ? this.hit(closest) : null;
    if (mode !== "edge") return faceHit;
    let hit: TelosHit | null = null,
      distance = Infinity;
    for (const line of this.lines) {
      const transform = new Matrix4().fromArray(
        line.transform ?? new Matrix4().elements,
      );
      const points = line.points;
      for (let i = 3; i < points.length; i += 3) {
        const a = new Vector3(
          points[i - 3],
          points[i - 2],
          points[i - 1],
        ).applyMatrix4(transform);
        const b = new Vector3(
          points[i],
          points[i + 1],
          points[i + 2],
        ).applyMatrix4(transform);
        const pa = this.camera.project(a.toArray()),
          pb = this.camera.project(b.toArray());
        if (pa[2] < 0 || pb[2] < 0 || pa[2] > 1 || pb[2] > 1) continue;
        const dx = pb[0] - pa[0],
          dy = pb[1] - pa[1],
          length = dx * dx + dy * dy;
        const t = length
          ? Math.max(
              0,
              Math.min(
                1,
                ((pixel[0] - pa[0]) * dx + (pixel[1] - pa[1]) * dy) / length,
              ),
            )
          : 0;
        const pixels = Math.hypot(
          pixel[0] - pa[0] - t * dx,
          pixel[1] - pa[1] - t * dy,
        );
        const onRay = new Vector3(),
          onSegment = new Vector3();
        this.raycaster.ray.distanceSqToSegment(a, b, onRay, onSegment);
        const d = onRay.distanceTo(ray.origin);
        const tolerance =
          Math.max(this.camera.span / this.camera.height, d * 0.001) * 3;
        if (
          pixels <= Math.max(4, (line.widthPixels ?? 1) / 2 + 2) &&
          d < distance &&
          (!closest || d <= closest.distance + tolerance)
        ) {
          distance = d;
          hit = {
            ...line.identity,
            worldPosition: onSegment.toArray(),
            worldNormal: null,
            distance: d,
          };
        }
      }
    }
    return hit;
  }
  hit(hit: Intersection): TelosHit {
    const { item, definition } = hit.object.userData as {
      item: TelosMesh | TelosField;
      definition: TelosGeometry;
    };
    const triangleIndex = hit.faceIndex ?? 0;
    const range = definition.ranges?.find(
      (range) =>
        triangleIndex >= range.startTriangle &&
        triangleIndex < range.startTriangle + range.triangleCount,
    );
    return {
      ...item.identity,
      faceId: range?.faceId ?? item.identity.faceId,
      triangleIndex,
      worldPosition: hit.point.toArray(),
      worldNormal: hit
        .face!.normal.clone()
        .transformDirection(
          new Matrix4().copy(hit.object.matrixWorld).invert().transpose(),
        )
        .toArray(),
      distance: hit.distance,
    };
  }
  dispose() {
    for (const mesh of this.proxies) {
      mesh.geometry.dispose();
      if (!Array.isArray(mesh.material)) mesh.material.dispose();
    }
    this.proxies = [];
  }
}
