import { Aetheris, type DisplayMesh, type ModelSession, type ModelTree } from "@aetheris/cad";
import bracketSource from "../../../fixtures/Canonical/WebSdk/editable-bracket.firmament?raw";
import "./styles.css";

const status = requiredElement<HTMLParagraphElement>("status");
const exportButton = requiredElement<HTMLButtonElement>("export");
const treeElement = requiredElement<HTMLOListElement>("semantic-tree");
const summaryElement = requiredElement<HTMLDListElement>("mesh-summary");
const canvas = requiredElement<HTMLCanvasElement>("viewport");

let cad: Aetheris | undefined;
let model: ModelSession | undefined;

void start();

async function start(): Promise<void> {
  try {
    cad = await Aetheris.create();
    const result = await cad.compile(bracketSource, { sourceName: "editable-bracket.firmament" });
    if (!result.model) {
      throw new Error(result.diagnostics.map((item) => `${item.code}: ${item.message}`).join("\n"));
    }

    model = result.model;
    renderSemanticTree(model.tree);
    renderMesh(model.mesh);
    renderSummary(model.mesh);
    status.textContent = `Compiled ${model.name} at revision ${model.revision}.`;
    exportButton.disabled = false;
  } catch (error) {
    status.classList.add("error");
    status.textContent = error instanceof Error ? error.message : String(error);
  }
}

exportButton.addEventListener("click", async () => {
  if (!model) return;
  exportButton.disabled = true;
  status.textContent = "Generating AP242 STEP…";
  try {
    const blob = await model.exportSTEPBlob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "web-bracket.step";
    link.click();
    URL.revokeObjectURL(url);
    status.textContent = `Exported ${blob.size.toLocaleString()} STEP bytes.`;
  } catch (error) {
    status.classList.add("error");
    status.textContent = error instanceof Error ? error.message : String(error);
  } finally {
    exportButton.disabled = false;
  }
});

window.addEventListener("beforeunload", () => {
  void model?.dispose();
  void cad?.dispose();
});

function renderSemanticTree(tree: ModelTree): void {
  const nodes = new Map(tree.nodes.map((node) => [node.id, node]));
  const seen = new Set<string>();

  const append = (id: string, depth: number): void => {
    const node = nodes.get(id);
    if (!node || seen.has(id)) return;
    seen.add(id);
    const item = document.createElement("li");
    item.style.paddingLeft = `${depth * 18}px`;
    item.append(node.name, " ");
    const kind = document.createElement("small");
    kind.textContent = node.kind;
    item.append(kind);
    treeElement.append(item);
    node.children.forEach((childId) => append(childId, depth + 1));
  };

  append(tree.rootId, 0);
  tree.nodes.forEach((node) => append(node.id, 0));
}

function renderSummary(mesh: DisplayMesh): void {
  const vertices = mesh.definitions.reduce((sum, definition) => sum + definition.positions.length / 3, 0);
  const triangles = mesh.definitions.reduce((sum, definition) => sum + definition.indices.length / 3, 0);
  const values = [
    ["Definitions", mesh.definitions.length],
    ["Occurrences", mesh.occurrences.length],
    ["Vertices", vertices],
    ["Triangles", triangles],
  ] as const;

  for (const [label, value] of values) {
    const group = document.createElement("div");
    const term = document.createElement("dt");
    term.textContent = label;
    const description = document.createElement("dd");
    description.textContent = value.toLocaleString();
    group.append(term, description);
    summaryElement.append(group);
  }
}

function renderMesh(mesh: DisplayMesh): void {
  const context = canvas.getContext("2d");
  if (!context) throw new Error("2D canvas is unavailable.");

  const definitions = new Map(mesh.definitions.map((definition) => [definition.id, definition]));
  const lines: Array<readonly [Point2, Point2]> = [];
  for (const occurrence of mesh.occurrences) {
    if (!occurrence.definitionId) continue;
    const definition = definitions.get(occurrence.definitionId);
    if (!definition) continue;
    for (let index = 0; index < definition.indices.length; index += 3) {
      const a = project(transform(readPoint(definition.positions, definition.indices[index]), occurrence.transform));
      const b = project(transform(readPoint(definition.positions, definition.indices[index + 1]), occurrence.transform));
      const c = project(transform(readPoint(definition.positions, definition.indices[index + 2]), occurrence.transform));
      lines.push([a, b], [b, c], [c, a]);
    }
  }

  context.clearRect(0, 0, canvas.width, canvas.height);
  if (lines.length === 0) return;
  const points = lines.flat();
  const minX = Math.min(...points.map((point) => point.x));
  const maxX = Math.max(...points.map((point) => point.x));
  const minY = Math.min(...points.map((point) => point.y));
  const maxY = Math.max(...points.map((point) => point.y));
  const margin = 36;
  const scale = Math.min(
    (canvas.width - margin * 2) / Math.max(maxX - minX, 1),
    (canvas.height - margin * 2) / Math.max(maxY - minY, 1),
  );
  const draw = (point: Point2): Point2 => ({
    x: margin + (point.x - minX) * scale,
    y: canvas.height - margin - (point.y - minY) * scale,
  });

  context.beginPath();
  for (const [start, end] of lines) {
    const from = draw(start);
    const to = draw(end);
    context.moveTo(from.x, from.y);
    context.lineTo(to.x, to.y);
  }
  context.strokeStyle = "#75d9c7";
  context.lineWidth = 0.65;
  context.globalAlpha = 0.55;
  context.stroke();
  context.globalAlpha = 1;
}

interface Point { readonly x: number; readonly y: number; readonly z: number }
interface Point2 { readonly x: number; readonly y: number }

function readPoint(positions: Float64Array, index: number): Point {
  const offset = index * 3;
  return { x: positions[offset], y: positions[offset + 1], z: positions[offset + 2] };
}

function transform(point: Point, matrix: readonly number[]): Point {
  return {
    x: matrix[0] * point.x + matrix[4] * point.y + matrix[8] * point.z + matrix[12],
    y: matrix[1] * point.x + matrix[5] * point.y + matrix[9] * point.z + matrix[13],
    z: matrix[2] * point.x + matrix[6] * point.y + matrix[10] * point.z + matrix[14],
  };
}

function project(point: Point): Point2 {
  return { x: point.x - point.z * 0.65, y: point.y + point.z * 0.42 };
}

function requiredElement<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Missing #${id}.`);
  return element as T;
}
