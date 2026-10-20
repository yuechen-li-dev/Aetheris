import type { TelosLine } from './contracts.js';
import { BufferGeometry, Float32BufferAttribute, Group, LineBasicMaterial, LineSegments } from 'three';

export type MeasurementMode = 'points' | 'planes' | 'axes';
export interface MeasurementPick { occurrenceId: string | null; faceId: number | null; rayOrigin: number[]; rayDirection: number[] }
export interface MeasurementRequest { mode: MeasurementMode; first: MeasurementPick; second?: MeasurementPick; allowApproximate: boolean }
export interface MeasurementReference { occurrenceId: string; faceId: number; surfaceKind: string; point: number[]; quality: string; diameterMm?: number | null }
export interface MeasurementResult {
  mode: MeasurementMode; first: MeasurementReference; second?: MeasurementReference | null;
  distanceMm: number | null; deltaMm: number[] | null; witnessA: number[] | null; witnessB: number[] | null;
  angleDegrees: number | null; quality: string; explanation: string;
}

/** Presentation only. All geometry and interpretation come from the compiler/server query owner. */
export class MeasurementTool {
  active = false;
  mode: MeasurementMode = 'points';
  result: MeasurementResult | null = null;
  private first?: MeasurementPick;
  private second?: MeasurementPick;
  private slot: 'A' | 'B' = 'A';
  private approximate = false;
  private generation = 0;
  private context: unknown;
  private disabled = '';
  private message = '';
  private pending = false;
  private inches = false;
  private readonly keyboard = (event: KeyboardEvent) => {
    const element = event.target as HTMLElement | null;
    if (element?.closest('input,textarea,select,[contenteditable="true"]') || event.ctrlKey || event.metaKey || event.altKey) return;
    if (event.key === 'Escape' && this.active) { this.active = false; this.reset(); this.render(); }
    if (event.key.toLowerCase() === 'm' && !this.disabled && this.host.parentElement?.contains(document.activeElement)) {
      event.preventDefault(); this.toggle();
    }
  };
  constructor(private host: HTMLElement, private query: (request: MeasurementRequest) => Promise<MeasurementResult>, private changed: () => void) {
    host.style.cssText = 'position:absolute;top:54px;right:12px;z-index:20;max-width:min(330px,90%);font:12px/1.5 system-ui;color:#202020';
    host.addEventListener('pointerdown', event => event.stopPropagation());
    host.addEventListener('click', event => event.stopPropagation());
    document.addEventListener('keydown', this.keyboard);
    this.render();
  }
  setContext(context: unknown, disabled = '') {
    if (context === this.context && disabled === this.disabled) return;
    this.context = context; this.disabled = disabled; this.reset(); this.render();
  }
  dispose() { this.generation++; document.removeEventListener('keydown', this.keyboard); this.host.replaceChildren(); }
  private reset() { this.generation++; this.first = undefined; this.second = undefined; this.result = null; this.slot = 'A'; this.pending = false; this.message = ''; this.changed(); }
  private toggle() { this.active = !this.active; this.reset(); this.render(); }
  async pick(pick: MeasurementPick) {
    if (!this.active || this.disabled) return;
    const generation = ++this.generation;
    const first = this.slot === 'A' || !this.first ? pick : this.first;
    const second = this.slot === 'A' || !this.first ? this.second : pick;
    // Stage input identities immediately so rapid A/B clicks remain a pair. No number or witness
    // is published until the current complete request has been admitted by the query owner.
    this.first = first; this.second = second; this.slot = 'B';
    this.pending = true; this.message = ''; this.result = null; this.changed(); this.render();
    try {
      const result = await this.query({ mode: this.mode, first, second, allowApproximate: this.approximate });
      if (generation !== this.generation) return;
      this.first = first; this.second = second; this.result = result; this.slot = 'B'; this.changed();
    } catch (error) {
      if (generation !== this.generation) return;
      // A failed replacement cannot leave an old number looking like the new measurement.
      this.result = null; this.first = undefined; this.second = undefined; this.slot = 'A';
      this.changed(); this.message = error instanceof Error ? error.message : String(error);
      const details = (error as { details?: unknown })?.details;
      if (typeof details === 'string' && details) this.message += ` ${details}`;
    } finally { if (generation === this.generation) { this.pending = false; this.render(); } }
  }
  private render() {
    this.host.replaceChildren();
    this.host.style.background = ''; this.host.style.padding = ''; this.host.style.border = '';
    const button = (label: string, action: () => void) => {
      const b = document.createElement('button'); b.type = 'button'; b.textContent = label;
      b.style.cssText = 'padding:5px 9px;margin:2px;border:1px solid #bbb;background:white;color:#222;border-radius:4px;cursor:pointer';
      b.onclick = action; this.host.append(b); return b;
    };
    const toggle = button(this.active ? 'Close measurement (Esc)' : 'Measure · M', () => this.toggle());
    toggle.disabled = !!this.disabled && !this.active; toggle.title = this.disabled || 'Measure two references in the current built model';
    if (!this.active) return;
    this.host.style.background = '#fffef9'; this.host.style.padding = '8px'; this.host.style.border = '1px solid #c6aa62'; this.host.style.borderRadius = '6px';
    const line = (text: string) => { const p = document.createElement('div'); p.textContent = text; this.host.append(p); return p; };
    const select = document.createElement('select'); select.setAttribute('aria-label', 'Measurement mode');
    for (const [value, label] of [['points','Surface points'], ['planes','Support planes'], ['axes','Axes']]) {
      const option = document.createElement('option'); option.value = value!; option.textContent = label!; select.append(option);
    }
    select.value = this.mode; select.onchange = () => { this.mode = select.value as MeasurementMode; this.reset(); this.render(); }; this.host.append(select);
    button(this.inches ? 'Units: in' : 'Units: mm', () => { this.inches = !this.inches; this.render(); });
    line(this.disabled || (this.pending ? 'Resolving BRep reference…' : `Pick ${this.slot}: click a visible face. Drag to orbit.`));
    const slotButton = (slot: 'A' | 'B', reference?: MeasurementReference | null) => {
      const b = button(`${slot === this.slot ? '●' : '○'} ${slot}: ${reference ? `${reference.surfaceKind} · face ${reference.faceId}` : 'not selected'}`, () => { this.slot = slot; this.render(); });
      b.title = reference ? `${reference.occurrenceId} / face ${reference.faceId}` : `Replace reference ${slot}`;
    };
    slotButton('A', this.result?.first); slotButton('B', this.result?.second);
    button('New measurement', () => { this.reset(); this.render(); });
    const fmt = (value: number) => `${(value / (this.inches ? 25.4 : 1)).toLocaleString('en-US', { maximumFractionDigits: 6 })} ${this.inches ? 'in' : 'mm'}`;
    if (this.result) {
      const r = this.result;
      line(r.quality).style.fontWeight = '600';
      if (r.distanceMm !== null) line(`Distance: ${fmt(r.distanceMm)}`).style.fontSize = '17px';
      if (r.deltaMm) line(`ΔX ${fmt(r.deltaMm[0]!)} · ΔY ${fmt(r.deltaMm[1]!)} · ΔZ ${fmt(r.deltaMm[2]!)}`);
      if (r.angleDegrees !== null) line(`Acute angle: ${r.angleDegrees.toFixed(6)}°`);
      for (const [name, ref] of [['A', r.first], ['B', r.second]] as const) if (ref?.diameterMm != null) line(`${name} diameter: ${fmt(ref.diameterMm)}`);
      line(r.explanation);
      button('Copy result', () => { void navigator.clipboard?.writeText(JSON.stringify(r, null, 2)).catch(() => { this.message = 'Clipboard unavailable; select the displayed result to copy.'; this.render(); }); });
    }
    if (this.mode === 'points') {
      const label = document.createElement('label'), check = document.createElement('input'); check.type = 'checkbox'; check.checked = this.approximate;
      check.onchange = () => { this.approximate = check.checked; this.reset(); this.render(); };
      label.append(check, ' Allow approximate display points'); this.host.append(label);
    }
    if (this.message) { const message = line(this.message); message.setAttribute('role', 'alert'); message.style.color = '#a12217'; }
    line('Read-only · world coordinates · no drawing dimensions authored').style.color = '#666';
  }
}

/** Capture only deliberate clicks; an orbit drag must never replace a measurement reference. */
export function bindMeasurementPicking(element: HTMLElement, tool: () => MeasurementTool | undefined,
  rayAt: (x: number, y: number) => MeasurementPick | null): () => void {
  let start: [number, number] | null = null;
  let dragged = false;
  const down = (event: PointerEvent) => { if (event.button === 0) { start = [event.clientX, event.clientY]; dragged = false; } };
  const move = (event: PointerEvent) => { if (start && Math.hypot(event.clientX - start[0], event.clientY - start[1]) > 4) dragged = true; };
  const click = (event: MouseEvent) => {
    const current = tool();
    if (!current?.active) return;
    event.stopImmediatePropagation();
    if (!start || dragged || event.button !== 0) return;
    const rect = element.getBoundingClientRect();
    const pick = rayAt(event.clientX - rect.left, event.clientY - rect.top);
    if (pick) void current.pick(pick);
    start = null;
  };
  element.addEventListener('pointerdown', down); element.addEventListener('pointermove', move); element.addEventListener('click', click, true);
  return () => { element.removeEventListener('pointerdown', down); element.removeEventListener('pointermove', move); element.removeEventListener('click', click, true); };
}

/** Non-pickable witness guides. Endpoint crosses remain visible at zero distance. */
export function measurementLines(result: MeasurementResult | null): TelosLine[] {
  if (!result?.witnessA) return [];
  const a = result.witnessA, b = result.witnessB;
  const points: number[] = b ? [...a, ...b] : [];
  const size = Math.max(0.1, Math.min(2, (result.distanceMm ?? 10) * 0.02));
  for (const p of [a, b]) if (p) for (let axis = 0; axis < 3; axis++) {
    const left = [...p], right = [...p]; left[axis]! -= size; right[axis]! += size; points.push(...left, ...right);
  }
  const lines: TelosLine[] = [];
  for (let i = 0; i < points.length; i += 6) lines.push({ id: `measurement:witness:${i / 6}`, points: points.slice(i,i+6), identity: { occurrenceId: 'measurement:witness' }, color: [0.95, 0.55, 0.05, 1], widthPixels: 2, depthMode: 'always-on-top', overlay: true, pickable: false });
  return lines;
}

export function measurementGroup(result: MeasurementResult | null): Group {
  const group = new Group(); group.name = 'Measurement witnesses';
  for (const guide of measurementLines(result)) {
    const geometry = new BufferGeometry().setAttribute('position', new Float32BufferAttribute(Array.from(guide.points), 3));
    const line = new LineSegments(geometry, new LineBasicMaterial({ color: '#f5a21c', depthTest: false, depthWrite: false, toneMapped: false }));
    line.raycast = () => undefined; line.renderOrder = 30; group.add(line);
  }
  return group;
}
