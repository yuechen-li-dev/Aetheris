import {
  Matrix4,
  Vector3,
  Quaternion,
  WebGPUCoordinateSystem,
  PerspectiveCamera,
  OrthographicCamera,
} from "three";
import type { NumericArray, Pixel } from "./contracts.js";

/** Column-major matrices; right-handed world; WebGPU clip depth [0,1]. Pixels are CSS pixels. */
export class TelosCamera {
  position: Vector3;
  target: Vector3;
  up: Vector3;
  mode: "perspective" | "orthographic";
  fov: number;
  span: number;
  near: number;
  far: number;
  width: number;
  height: number;
  view: Matrix4;
  inverseView: Matrix4;
  projection: Matrix4;
  inverseProjection: Matrix4;
  viewProjection: Matrix4;
  inverseViewProjection: Matrix4;
  constructor() {
    this.position = new Vector3(4, 3, 5);
    this.target = new Vector3();
    this.up = new Vector3(0, 1, 0);
    this.mode = "perspective";
    this.fov = 35;
    this.span = 5;
    this.near = 0.01;
    this.far = 10000;
    this.width = 1;
    this.height = 1;
    this.view = new Matrix4();
    this.inverseView = new Matrix4();
    this.projection = new Matrix4();
    this.inverseProjection = new Matrix4();
    this.viewProjection = new Matrix4();
    this.inverseViewProjection = new Matrix4();
    this.update();
  }
  resize(width: number, height: number) {
    this.width = Math.max(1, width);
    this.height = Math.max(1, height);
    this.update();
  }
  update() {
    if (!(this.near > 0 && this.far > this.near && this.span > 0))
      throw new Error("telos-camera-clipping-invalid");
    const rotation = new Matrix4().lookAt(this.position, this.target, this.up);
    this.inverseView.compose(
      this.position,
      new Quaternion().setFromRotationMatrix(rotation),
      new Vector3(1, 1, 1),
    );
    this.view.copy(this.inverseView).invert();
    const aspect = this.width / this.height;
    if (this.mode === "orthographic") {
      const h = this.span / 2;
      this.projection.makeOrthographic(
        -h * aspect,
        h * aspect,
        h,
        -h,
        this.near,
        this.far,
        WebGPUCoordinateSystem,
      );
    } else {
      const h = this.near * Math.tan((this.fov * Math.PI) / 360);
      this.projection.makePerspective(
        -h * aspect,
        h * aspect,
        h,
        -h,
        this.near,
        this.far,
        WebGPUCoordinateSystem,
      );
    }
    this.inverseProjection.copy(this.projection).invert();
    this.viewProjection.multiplyMatrices(this.projection, this.view);
    this.inverseViewProjection.copy(this.viewProjection).invert();
  }
  project(point: NumericArray) {
    const p = new Vector3(point[0], point[1], point[2]).applyMatrix4(
      this.viewProjection,
    );
    return [((p.x + 1) * this.width) / 2, ((1 - p.y) * this.height) / 2, p.z];
  }
  unproject(pixel: Pixel, depth: number) {
    return new Vector3(
      (pixel[0] / this.width) * 2 - 1,
      1 - (pixel[1] / this.height) * 2,
      depth,
    ).applyMatrix4(this.inverseViewProjection);
  }
  worldRay(pixel: Pixel) {
    const origin = this.unproject(pixel, 0),
      end = this.unproject(pixel, 1);
    return { origin, direction: end.clone().sub(origin).normalize() };
  }
  fit(minimum: NumericArray, maximum: NumericArray) {
    const min = new Vector3(minimum[0], minimum[1], minimum[2]),
      max = new Vector3(maximum[0], maximum[1], maximum[2]);
    if (
      ![...Array.from(minimum), ...Array.from(maximum)].every(Number.isFinite)
    )
      return;
    this.target.copy(min).add(max).multiplyScalar(0.5);
    const radius = Math.max(max.distanceTo(min) / 2, 1e-5);
    const distance = (radius / Math.sin((this.fov * Math.PI) / 360)) * 1.2;
    this.position
      .copy(this.target)
      .add(new Vector3(1, 0.8, 1).normalize().multiplyScalar(distance));
    this.span = radius * 2.5;
    this.near = Math.max(radius * 1e-4, 1e-8);
    this.far = distance + radius * 20;
    this.update();
  }
  /** Output-only compatibility state. Changes to the adapter never change Telos. */
  toThree() {
    const camera =
      this.mode === "orthographic"
        ? new OrthographicCamera()
        : new PerspectiveCamera();
    camera.matrixAutoUpdate = false;
    camera.matrixWorld.copy(this.inverseView);
    camera.matrixWorldInverse.copy(this.view);
    camera.projectionMatrix.copy(this.projection);
    camera.projectionMatrixInverse.copy(this.inverseProjection);
    camera.position.copy(this.position);
    return camera;
  }
}
