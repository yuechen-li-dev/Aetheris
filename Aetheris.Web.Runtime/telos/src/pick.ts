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
  dynamicLines: readonly TelosLine[] = [];
  raycaster: Raycaster;
  constructor(camera: TelosCamera) {
    this.camera = camera;
    this.proxies = [];
    this.lines = [];
    this.raycaster = new Raycaster();
  }
  setScene(scene: TelosScene) {
    // Per-face draw ranges require separate geometries, but not copies of an
    // entire body's CPU buffers for every face or repeated occurrence.
    const buffers = new Map<TelosGeometry, { position: BufferAttribute; index: BufferAttribute }>();
    for (const mesh of this.proxies)
      buffers.set(mesh.userData.definition, {
        position: mesh.geometry.getAttribute("position") as BufferAttribute,
        index: mesh.geometry.index!,
      });
    const previous = new Map(
      this.proxies.map((mesh) => [
        JSON.stringify([
          mesh.userData.item.identity.occurrenceId,
          mesh.userData.item.identity.overlayId,
          mesh.userData.definition.id,
          mesh.userData.item.triangleRange?.startTriangle,
        ]),
        mesh,
      ]),
    );
    this.proxies = [];
    this.lines = scene.lines ?? [];
    for (const item of [
      ...(scene.meshes ?? []),
      ...(scene.fields ?? []).filter((field) => field.proxy),
    ]) {
      const definition = "definition" in item ? item.definition : item.proxy!;
      const key = JSON.stringify([
        item.identity.occurrenceId,
        item.identity.overlayId,
        definition.id,
        "triangleRange" in item ? item.triangleRange?.startTriangle : undefined,
      ]);
      let mesh = previous.get(key);
      if (
        mesh &&
        (mesh.userData.definition.positions !== definition.positions ||
          mesh.userData.definition.indices !== definition.indices)
      )
        mesh = undefined;
      if (!mesh) {
        const geometry = new BufferGeometry();
        let shared = buffers.get(definition);
        if (!shared) {
          shared = { position: new BufferAttribute(Float32Array.from(definition.positions), 3),
            index: new BufferAttribute(Uint32Array.from(definition.indices), 1) };
          buffers.set(definition, shared);
        }
        geometry.setAttribute("position", shared.position);
        geometry.setIndex(shared.index);
        mesh = new Mesh(geometry, new MeshBasicMaterial({ side: DoubleSide }));
      } else previous.delete(key);
      mesh.matrixAutoUpdate = false;
      const range = "triangleRange" in item ? item.triangleRange : undefined;
      mesh.geometry.setDrawRange(range ? range.startTriangle * 3 : 0,
        range ? range.triangleCount * 3 : Infinity);
      mesh.matrix.fromArray(item.transform ?? new Matrix4().elements);
      mesh.updateMatrixWorld(true);
      mesh.userData = { item, definition };
      this.proxies.push(mesh);
    }
    for (const mesh of previous.values()) {
      mesh.geometry.dispose();
      if (!Array.isArray(mesh.material)) mesh.material.dispose();
    }
  }
  pick(pixel: Pixel, mode = "face"): TelosHit | null {
    const ray = this.camera.worldRay(pixel);
    this.raycaster.ray.set(ray.origin, ray.direction);
    const intersections = this.raycaster.intersectObjects(
      this.proxies.filter((mesh) => mesh.userData.item.visible !== false),
      false,
    );
    // Explicit authoring surfaces take priority over model faces. Equal priority uses distance.
    const nearestModel = intersections.find(
      (hit) => !hit.object.userData.item.identity.overlayId,
    );
    const overlay = intersections.find(
      (hit) =>
        hit.object.userData.item.identity.overlayId &&
        (hit.object.userData.item.depthMode === "always-on-top" ||
          !nearestModel ||
          hit.distance <= nearestModel.distance),
    );
    const closest = overlay ?? intersections[0];
    const faceHit = closest ? this.hit(closest) : null;
    if (overlay?.object.userData.item.depthMode === "always-on-top")
      return faceHit;
    let hit: TelosHit | null = null,
      distance = Infinity,
      priority = -1;
    for (const line of [...this.dynamicLines, ...this.lines]) {
      if (
        line.visible === false ||
        (mode !== "edge" && !line.identity.overlayId)
      )
        continue;
      const transform = new Matrix4().fromArray(
        line.transform ?? new Matrix4().elements,
      );
      const points = line.points;
      const linePriority = this.dynamicLines.includes(line)
        ? 2
        : line.identity.overlayId
          ? 1
          : 0;
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
          (linePriority > priority ||
            (linePriority === priority && d < distance)) &&
          (line.depthMode === "always-on-top" ||
            !closest ||
            d <= closest.distance + tolerance)
        ) {
          distance = d;
          priority = linePriority;
          hit = {
            ...line.identity,
            worldPosition: onSegment.toArray(),
            worldNormal: null,
            distance: d,
          };
        }
      }
    }
    return hit ?? (mode === "edge" ? null : faceHit);
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
