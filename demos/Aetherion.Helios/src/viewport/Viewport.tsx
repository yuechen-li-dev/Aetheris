import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { ModelSession } from '@aetheris/cad';
import type { DisplayMode, ThemeName, ViewMode } from '../app/types';

interface Props {
  model: ModelSession | null;
  selectedEntityId: string | null;
  theme: ThemeName;
  displayMode: DisplayMode;
  viewMode: ViewMode;
  viewCommand: string;
  onSelect(entityId: string | null, faceId: string | null): void;
}

type MeshMeta = { definitionId: string; occurrenceId: string; edge?: boolean };

export function Viewport(props: Props) {
  const host = useRef<HTMLDivElement>(null);
  const state = useRef<ReturnType<typeof createScene> | null>(null);

  useEffect(() => {
    if (!host.current) return;
    const sceneState = createScene(host.current, props.onSelect);
    state.current = sceneState;
    return () => { sceneState.dispose(); state.current = null; };
  }, []);

  useEffect(() => { if (state.current) loadModel(state.current, props.model); }, [props.model, props.model?.revision]);
  useEffect(() => { if (state.current) selectEntity(state.current, props.model, props.selectedEntityId); }, [props.model, props.selectedEntityId]);
  useEffect(() => { if (state.current) applyAppearance(state.current, props.theme, props.displayMode); }, [props.theme, props.displayMode]);
  useEffect(() => { if (state.current) applyViewCommand(state.current, props.viewMode, props.viewCommand); }, [props.viewMode, props.viewCommand]);

  return <section className="viewport-shell" aria-label="3D viewport">
    <div className="viewport-canvas" ref={host} data-testid="viewport" />
    <div className="axis-labels" aria-hidden="true"><span className="axis-x">X</span><span className="axis-y">Y</span><span className="axis-z">Z</span></div>
    {!props.model && <div className="viewport-empty">Firmament geometry will appear here.</div>}
  </section>;
}

function createScene(host: HTMLDivElement, onSelect: Props['onSelect']) {
  const scene = new THREE.Scene();
  const renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  host.appendChild(renderer.domElement);
  const perspective = new THREE.PerspectiveCamera(35, 1, 0.1, 10000);
  const orthographic = new THREE.OrthographicCamera(-70, 70, 70, -70, 0.1, 10000);
  perspective.position.set(90, 70, 90);
  orthographic.position.copy(perspective.position);
  let camera: THREE.Camera = perspective;
  const controls = new OrbitControls(perspective, renderer.domElement);
  controls.enableDamping = true;
  controls.dampingFactor = 0.12;
  const ambient = new THREE.HemisphereLight(0xffffff, 0x263128, 2.1);
  const key = new THREE.DirectionalLight(0xffffff, 2.8); key.position.set(60, 100, 80);
  scene.add(ambient, key);
  const grid = new THREE.GridHelper(400, 40, 0x6a715f, 0x414942); grid.rotation.x = Math.PI / 2; scene.add(grid);
  const axes = new THREE.AxesHelper(25); scene.add(axes);
  const group = new THREE.Group(); scene.add(group);
  const raycaster = new THREE.Raycaster();
  const pointer = new THREE.Vector2();
  let hovered: THREE.Mesh | null = null;
  let selectedIds = new Set<string>();
  let dirty = true;
  let raf = 0;
  const invalidate = () => { dirty = true; if (!raf) raf = requestAnimationFrame(render); };
  const render = () => {
    raf = 0;
    const damping = controls.update();
    if (dirty || damping) renderer.render(scene, camera);
    dirty = false;
    if (damping) raf = requestAnimationFrame(render);
  };
  controls.addEventListener('change', invalidate);
  const resize = new ResizeObserver(() => {
    const width = Math.max(host.clientWidth, 1), height = Math.max(host.clientHeight, 1);
    renderer.setSize(width, height, false);
    perspective.aspect = width / height; perspective.updateProjectionMatrix();
    const extent = 70; orthographic.left = -extent * width / height; orthographic.right = extent * width / height; orthographic.top = extent; orthographic.bottom = -extent; orthographic.updateProjectionMatrix();
    invalidate();
  });
  resize.observe(host);
  const hitTest = (event: PointerEvent) => {
    const rect = renderer.domElement.getBoundingClientRect();
    pointer.set(((event.clientX - rect.left) / rect.width) * 2 - 1, -((event.clientY - rect.top) / rect.height) * 2 + 1);
    raycaster.setFromCamera(pointer, camera);
    return raycaster.intersectObjects(group.children, false)[0];
  };
  renderer.domElement.addEventListener('pointermove', event => {
    const next = hitTest(event)?.object as THREE.Mesh | undefined;
    if (hovered !== next) { hovered = next ?? null; updateMaterials(group, selectedIds, hovered); invalidate(); }
  });
  renderer.domElement.addEventListener('click', event => {
    const hit = hitTest(event);
    if (!hit) { onSelect(null, null); return; }
    const meta = hit.object.userData as MeshMeta;
    const resolved = (state.model as ModelSession | null)?.resolveSelection(meta.definitionId, hit.faceIndex ?? 0, meta.occurrenceId);
    onSelect(resolved?.semanticEntityId ?? null, resolved?.faceId ?? null);
  });
  const state = { scene, renderer, perspective, orthographic, get camera() { return camera; }, set camera(value) { camera = value; }, controls, grid, group, model: null as ModelSession | null, selectedIds, hovered, invalidate,
    dispose() { cancelAnimationFrame(raf); resize.disconnect(); controls.dispose(); renderer.dispose(); host.replaceChildren(); } };
  invalidate();
  return state;
}

function loadModel(state: ReturnType<typeof createScene>, model: ModelSession | null) {
  for (const child of [...state.group.children]) { state.group.remove(child); disposeObject(child); }
  state.model = model;
  if (!model) { state.invalidate(); return; }
  const geometries = new Map(model.mesh.definitions.map(definition => {
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(Float32Array.from(definition.positions), 3));
    geometry.setAttribute('normal', new THREE.BufferAttribute(Float32Array.from(definition.normals), 3));
    geometry.setIndex(new THREE.BufferAttribute(definition.indices, 1));
    geometry.computeBoundingSphere();
    return [definition.id, geometry];
  }));
  for (const occurrence of model.mesh.occurrences) {
    const geometry = occurrence.definitionId ? geometries.get(occurrence.definitionId) : undefined;
    if (!geometry || !occurrence.definitionId) continue;
    const mesh = new THREE.Mesh(geometry, material(false, false));
    mesh.matrix.fromArray(occurrence.transform); mesh.matrixAutoUpdate = false;
    mesh.userData = { definitionId: occurrence.definitionId, occurrenceId: occurrence.id } satisfies MeshMeta;
    state.group.add(mesh);
    const edges = new THREE.LineSegments(new THREE.EdgesGeometry(geometry, 25), new THREE.LineBasicMaterial({ color: 0x323a35, transparent: true, opacity: 0.72 }));
    edges.matrix.copy(mesh.matrix); edges.matrixAutoUpdate = false;
    edges.userData = { definitionId: occurrence.definitionId, occurrenceId: occurrence.id, edge: true } satisfies MeshMeta;
    edges.raycast = () => undefined;
    state.group.add(edges);
  }
  fit(state);
}

function selectEntity(state: ReturnType<typeof createScene>, model: ModelSession | null, entityId: string | null) {
  const selection = entityId && model ? model.selectionForEntity(entityId) : { occurrenceIds: [] };
  state.selectedIds = new Set(selection.occurrenceIds);
  updateMaterials(state.group, state.selectedIds, state.hovered);
  state.invalidate();
}

function updateMaterials(group: THREE.Group, selectedIds: Set<string>, hovered: THREE.Mesh | null) {
  for (const object of group.children) if (object instanceof THREE.Mesh) {
    const selected = selectedIds.has((object.userData as MeshMeta).occurrenceId);
    (object.material as THREE.MeshStandardMaterial).color.set(selected ? 0xe7ad45 : object === hovered ? 0x78b9a4 : 0xb8beb3);
    (object.material as THREE.MeshStandardMaterial).emissive.set(selected ? 0x382406 : 0x000000);
  } else if (object instanceof THREE.LineSegments) {
    const selected = selectedIds.has((object.userData as MeshMeta).occurrenceId);
    (object.material as THREE.LineBasicMaterial).color.set(selected ? 0xffca63 : 0x323a35);
  }
}

function applyAppearance(state: ReturnType<typeof createScene>, theme: ThemeName, mode: DisplayMode) {
  state.scene.background = new THREE.Color(theme === 'mars' ? 0x111814 : 0xe9ece8);
  (state.grid.material as THREE.Material).opacity = theme === 'mars' ? 0.28 : 0.4;
  (state.grid.material as THREE.Material).transparent = true;
  for (const object of state.group.children) if (object instanceof THREE.Mesh) {
    const mat = object.material as THREE.MeshStandardMaterial;
    mat.wireframe = mode === 'wireframe';
    mat.flatShading = mode !== 'shaded'; mat.needsUpdate = true;
  } else if (object instanceof THREE.LineSegments) {
    object.visible = mode === 'edges';
    (object.material as THREE.LineBasicMaterial).color.set(theme === 'mars' ? 0x323a35 : 0x69716d);
  }
  state.invalidate();
}

function applyViewCommand(state: ReturnType<typeof createScene>, mode: ViewMode, command: string) {
  command = command.split('-')[0];
  const prior = state.camera;
  state.camera = mode === 'orthographic' ? state.orthographic : state.perspective;
  state.controls.object = state.camera as THREE.PerspectiveCamera;
  if (prior !== state.camera) state.camera.position.copy(prior.position);
  const distance = Math.max(state.camera.position.length(), 100);
  const positions: Record<string, THREE.Vector3> = { front: new THREE.Vector3(0, -distance, 0), top: new THREE.Vector3(0, 0, distance), right: new THREE.Vector3(distance, 0, 0), iso: new THREE.Vector3(distance, distance, distance) };
  if (positions[command]) state.camera.position.copy(positions[command]);
  if (command === 'fit') fit(state);
  else if (command === 'fitselection') fitSelection(state);
  else { state.camera.lookAt(0, 0, 0); state.controls.update(); state.invalidate(); }
}

function fit(state: ReturnType<typeof createScene>, objects: readonly THREE.Object3D[] = [state.group]) {
  const box = new THREE.Box3();
  for (const object of objects) box.expandByObject(object);
  if (box.isEmpty()) { state.invalidate(); return; }
  const center = box.getCenter(new THREE.Vector3());
  const size = box.getSize(new THREE.Vector3()).length();
  state.controls.target.copy(center);
  state.camera.position.copy(center).add(new THREE.Vector3(1, 0.8, 1).normalize().multiplyScalar(Math.max(size * 1.5, 30)));
  state.camera.lookAt(center); state.controls.update(); state.invalidate();
}

function fitSelection(state: ReturnType<typeof createScene>) {
  const selected = state.group.children.filter(object => object instanceof THREE.Mesh && state.selectedIds.has((object.userData as MeshMeta).occurrenceId));
  fit(state, selected.length ? selected : [state.group]);
}

function material(selected: boolean, wireframe: boolean) { return new THREE.MeshStandardMaterial({ color: selected ? 0xe7ad45 : 0xb8beb3, metalness: 0.15, roughness: 0.62, side: THREE.DoubleSide, wireframe }); }
function disposeObject(object: THREE.Object3D) { if (object instanceof THREE.Mesh || object instanceof THREE.LineSegments) { object.geometry.dispose(); (object.material as THREE.Material).dispose(); } }
