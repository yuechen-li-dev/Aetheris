/** A compiler projection, never an independent sketch model or constraint system. */
export interface SketchControl { id: string; label: string; authored: string; value: number; unit: string; editable: boolean; parameter: string | null; readOnlyReason: string | null; span: { start: number; length: number } }
export interface SketchShape { id: string; name: string; kind: string; center: readonly number[]; controls: readonly SketchControl[] }
export interface SketchCurve {
  id: string; name: string; kind: string; points: readonly (readonly number[])[]; center: readonly number[] | null;
  radius: number; startAngle: number; sweep: number; minorRadius: number; rotation: number;
  /** Resolver evidence. Its sourceSpan can identify lowered source; use control spans for authored navigation. */
  provenance?: { stableId: string; conceptStableId: string; sourceSpan: string; derivation: string; sourceFrame: string; tracedFrom?: string | null; reversed: boolean; pipelineIndex?: number | null; invocationSourceRange?: string | null };
}
export interface SketchProjection {
  version: 'firmament-sketch/1'; document: string; revision: string; node: string | null;
  profiles: readonly { id: string; name: string }[]; emptyModels: readonly { id: string; name: string }[];
  openingReferences: readonly { id: string; name: string }[];
  frame: { name: string; origin: readonly number[]; x: readonly number[]; y: readonly number[]; normal: readonly number[] } | null;
  loops: readonly { name: string; isOuter: boolean; curves: readonly SketchCurve[] }[];
  shapes: readonly SketchShape[]; diagnostics: readonly { severity: string; code: string; message: string; span: { start: number; length: number } | null }[];
  validationLevel: 'ProfileResolvedNotBuilt' | 'Unresolved';
}
export interface SketchEditRequest { kind: 'dimensions' | 'create' | 'opening'; node?: string | null; shape?: string; changes?: readonly { control: string; value: number }[]; x?: number; y?: number; reference?: string; anchor?: string; diameter?: number; width?: number; height?: number }
export interface SketchCallbacks {
  project(node: string | null): Promise<SketchProjection>;
  preview(request: SketchEditRequest): Promise<SketchProjection>;
  apply(request: SketchEditRequest): Promise<string | null>;
  source(span: { start: number; length: number }): void;
  close(): void;
}

const ns = 'http://www.w3.org/2000/svg';
const number = (value: number) => Number(value.toPrecision(8)).toString();
const errorText = (error: unknown) => {
  const code = (error as { code?: unknown })?.code;
  return (typeof code === 'string' ? `${code}: ` : '') + (error instanceof Error ? error.message : String(error));
};
/** Display sampling only; source geometry and engineering validation stay with Profile resolution. */
export function sketchCurvePoints(curve: SketchCurve): number[][] {
  if (curve.kind === 'Line') return curve.points.map(p => [...p]);
  if (curve.kind === 'Cubic') return Array.from({ length: 49 }, (_, i) => {
    const t = i / 48, u = 1 - t, p = curve.points;
    return [0, 1].map(a => u ** 3 * p[0][a] + 3 * u ** 2 * t * p[1][a] + 3 * u * t ** 2 * p[2][a] + t ** 3 * p[3][a]);
  });
  const count = Math.max(8, Math.ceil(Math.abs(curve.sweep) * 36));
  return Array.from({ length: count + 1 }, (_, i) => {
    const angle = curve.startAngle + curve.sweep * i / count;
    const x = curve.radius * Math.cos(angle), y = (curve.kind === 'Ellipse' ? curve.minorRadius : curve.radius) * Math.sin(angle);
    return [curve.center![0] + x * Math.cos(curve.rotation) - y * Math.sin(curve.rotation),
      curve.center![1] + x * Math.sin(curve.rotation) + y * Math.cos(curve.rotation)];
  });
}

/** Framework-independent 2D workspace shared by product hosts. Each completed gesture is one source edit. */
export class SketchWorkspace {
  private view: SketchProjection | null = null;
  private selected: string | null = null;
  private generation = 0;
  private disposed = false;
  private pending = false;
  private error = '';
  private draft = false;
  private revision: unknown;
  private timer?: ReturnType<typeof setTimeout>;
  private dragCleanup?: () => void;
  private svg?: SVGSVGElement;
  private extent = [-60, -40, 120, 80];
  private readonly keyboard = (event: KeyboardEvent) => {
    if (event.key === 'Tab') {
      const controls = Array.from(this.host.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),select:not(:disabled),[tabindex="0"]'));
      const first = controls[0], last = controls.at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
      return;
    }
    if (event.key !== 'Escape') return;
    if (this.dragCleanup) { this.cancelDrag(); this.render(); }
    else if (!(event.target as HTMLElement)?.closest('input,select')) this.callbacks.close();
  };
  constructor(private host: HTMLElement, private callbacks: SketchCallbacks) {
    host.style.cssText = 'position:absolute;inset:0;z-index:30;display:flex;flex-direction:column;background:#fafaf7;color:#242424;font:13px/1.45 system-ui';
    host.style.colorScheme = 'light';
    host.setAttribute('role', 'region'); host.setAttribute('aria-label', 'Sketch workspace');
    host.tabIndex = -1; host.focus();
    host.addEventListener('keydown', this.keyboard);
  }
  async load(revision: unknown, node: string | null = this.view?.node ?? null) {
    this.revision = revision; this.cancelDrag();
    const generation = ++this.generation;
    this.pending = true; this.render();
    try {
      const view = await this.callbacks.project(node);
      if (generation !== this.generation || this.disposed) return;
      this.view = view; this.error = ''; this.draft = false;
      if (!view.shapes.some(s => s.id === this.selected)) this.selected = view.shapes[0]?.id ?? null;
    } catch (error) { if (generation === this.generation) { this.view = null; this.error = errorText(error); } }
    finally { if (generation === this.generation && !this.disposed) { this.pending = false; this.render(); } }
  }
  dispose() { this.disposed = true; ++this.generation; this.cancelDrag(); this.host.removeEventListener('keydown', this.keyboard); this.host.replaceChildren(); }
  private cancelDrag() { clearTimeout(this.timer); this.dragCleanup?.(); this.dragCleanup = undefined; this.draft = false; }
  private async apply(request: SketchEditRequest) {
    if (this.pending) return;
    const generation = ++this.generation;
    this.pending = true; this.error = ''; this.draft = false; this.render();
    try {
      const refused = await this.callbacks.apply(request);
      if (generation !== this.generation || this.disposed) return;
      if (refused) this.error = refused;
    } catch (error) { if (generation === this.generation) this.error = errorText(error); }
    finally { if (generation === this.generation && !this.disposed) { this.pending = false; this.render(); } }
  }
  private render() {
    this.host.replaceChildren();
    const bar = document.createElement('div'); bar.style.cssText = 'display:flex;gap:8px;align-items:center;padding:10px 14px;border-bottom:1px solid #ddd;flex-wrap:wrap'; this.host.append(bar);
    const button = (parent: HTMLElement, text: string, action: () => void) => {
      const b = document.createElement('button'); b.type = 'button'; b.textContent = text; b.onclick = action;
      b.style.cssText = 'padding:6px 10px;border:1px solid #b7b7af;border-radius:4px;background:white;color:#222;cursor:pointer'; parent.append(b); return b;
    };
    const title = document.createElement('strong'); title.textContent = 'Sketch'; bar.append(title);
    const profiles = document.createElement('select'); profiles.setAttribute('aria-label', 'Sketch profile');
    for (const profile of this.view?.profiles ?? []) { const option = document.createElement('option'); option.value = profile.id; option.textContent = profile.name; profiles.append(option); }
    profiles.value = this.view?.node ?? ''; profiles.disabled = this.pending;
    profiles.onchange = () => void this.load(this.revision, profiles.value); bar.append(profiles);
    button(bar, 'Fit', () => this.draw(true));
    const close = button(bar, 'Done', () => this.callbacks.close()); close.style.marginLeft = 'auto';
    if (this.view?.frame) {
      const frame = document.createElement('div'); frame.textContent = `Plane: ${this.view.frame.name} · origin (${this.view.frame.origin.map(number).join(', ')}) mm · normal (${this.view.frame.normal.map(number).join(', ')})`;
      frame.style.cssText = 'padding:6px 14px;color:#62625a;background:#f1f0e9'; this.host.append(frame);
    }
    if (this.view?.emptyModels.length) {
      const create = document.createElement('div'); create.style.cssText = 'padding:8px 14px;display:flex;gap:8px;align-items:center';
      const label = document.createElement('span'); label.textContent = 'Start a profile + 6 mm extrusion:'; create.append(label);
      const parent = document.createElement('select'); parent.setAttribute('aria-label', 'New sketch model');
      for (const model of this.view.emptyModels) { const option = document.createElement('option'); option.value = model.id; option.textContent = model.name; parent.append(option); } create.append(parent);
      for (const [shape, text] of [['Rect2', 'Rectangle'], ['Circle2', 'Circle'], ['Slot2', 'Slot'], ['RoundedRect2', 'Rounded rectangle']])
        button(create, text, () => void this.apply({ kind: 'create', node: parent.value, shape })).disabled = this.pending;
      this.host.append(create);
    }
    const content = document.createElement('div'); content.style.cssText = 'display:flex;flex:1;min-height:0'; this.host.append(content);
    const canvas = document.createElement('div'); canvas.style.cssText = 'position:relative;flex:1;min-width:0;overflow:hidden'; content.append(canvas);
    this.svg = document.createElementNS(ns, 'svg'); this.svg.setAttribute('aria-label', 'Profile canvas'); this.svg.style.cssText = 'width:100%;height:100%;min-height:260px;touch-action:none'; canvas.append(this.svg);
    this.svg.addEventListener('wheel', event => {
      event.preventDefault(); const scale = event.deltaY > 0 ? 1.12 : 1 / 1.12;
      const [x, y, w, h] = this.extent; this.extent = [x + w * (1 - scale) / 2, y + h * (1 - scale) / 2, w * scale, h * scale]; this.draw(false);
    }, { passive: false });
    this.svg.addEventListener('pointerdown', event => {
      if (event.button !== 1 && !(event.button === 0 && event.altKey)) return;
      event.preventDefault(); this.cancelDrag();
      const startX = event.clientX, startY = event.clientY, extent = [...this.extent];
      const scale = 1 / Math.abs(this.svg!.getScreenCTM()?.a ?? 1);
      const move = (next: PointerEvent) => { this.extent = [extent[0] - (next.clientX - startX) * scale, extent[1] - (next.clientY - startY) * scale, extent[2], extent[3]]; this.draw(false); };
      const finish = () => { cleanup(); this.dragCleanup = undefined; };
      const cleanup = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', finish); window.removeEventListener('pointercancel', finish); };
      this.dragCleanup = cleanup; window.addEventListener('pointermove', move); window.addEventListener('pointerup', finish); window.addEventListener('pointercancel', finish);
    });
    const inspector = document.createElement('div'); inspector.style.cssText = 'width:250px;max-width:38%;padding:12px;overflow:auto;border-left:1px solid #ddd;background:white'; content.append(inspector);
    const choices = document.createElement('select'); choices.setAttribute('aria-label', 'Sketch shape'); choices.style.width = '100%';
    for (const shape of this.view?.shapes ?? []) { const option = document.createElement('option'); option.value = shape.id; option.textContent = `${shape.name} · ${shape.kind}`; choices.append(option); }
    choices.value = this.selected ?? ''; choices.onchange = () => { this.selected = choices.value; this.render(); }; inspector.append(choices);
    const shape = this.view?.shapes.find(s => s.id === this.selected);
    for (const control of shape?.controls ?? []) {
      const row = document.createElement('label'); row.style.cssText = 'display:block;margin-top:12px';
      const label = document.createElement('span'); label.textContent = `${control.label} (${control.unit})`; row.append(label);
      const input = document.createElement('input'); input.type = 'number'; input.step = 'any'; input.value = number(control.value);
      input.setAttribute('aria-label', control.label); input.disabled = this.pending || !control.editable;
      input.title = control.readOnlyReason ?? (control.parameter ? `Edits parameter ${control.parameter}` : control.authored);
      input.style.cssText = 'display:block;box-sizing:border-box;width:100%;padding:6px;margin-top:3px'; row.append(input);
      let submitted = false;
      const commit = () => {
        if (submitted || input.value.trim() === '' || !Number.isFinite(input.valueAsNumber) || input.valueAsNumber === control.value) return;
        submitted = true; void this.apply({ kind: 'dimensions', node: this.view?.node, changes: [{ control: control.id, value: input.valueAsNumber }] });
      };
      input.onblur = commit; input.onkeydown = event => { if (event.key === 'Enter') { event.preventDefault(); commit(); } if (event.key === 'Escape') { input.value = number(control.value); input.blur(); } };
      const origin = document.createElement('small'); origin.style.cssText = 'display:block;color:#777;overflow-wrap:anywhere';
      origin.textContent = control.parameter ? `↗ ${control.parameter} · shared parameter` : control.editable ? 'Driving dimension' : `ƒ ${control.authored}`;
      row.append(origin);
      if (!control.editable) button(row, 'Edit source', () => this.callbacks.source(control.span));
      inspector.append(row);
    }
    if (this.view?.openingReferences.length && !this.pending) {
      const form = document.createElement('form'); form.style.cssText = 'border-top:1px solid #ddd;margin-top:20px;padding-top:10px';
      const heading = document.createElement('strong'); heading.textContent = 'Circular profile opening'; form.append(heading);
      const help = document.createElement('p'); help.textContent = 'Follow a rectangle corner with shared driving parameters. Insets are inward from its edges.'; form.append(help);
      const reference = document.createElement('select'); reference.setAttribute('aria-label', 'Opening reference');
      for (const item of this.view.openingReferences) { const option = document.createElement('option'); option.value = item.id; option.textContent = item.name; reference.append(option); } form.append(reference);
      const anchor = document.createElement('select'); anchor.setAttribute('aria-label', 'Opening anchor');
      for (const name of ['BottomLeft', 'BottomRight', 'TopRight', 'TopLeft', 'Center', 'Left', 'Right', 'Bottom', 'Top']) { const option = document.createElement('option'); option.value = name; option.textContent = name.replace(/([a-z])([A-Z])/g, '$1 $2') + (['Left', 'Right', 'Bottom', 'Top'].includes(name) ? ' midpoint' : ''); anchor.append(option); } form.append(anchor);
      const input = (name: string, initial: number) => {
        const label = document.createElement('label'); label.textContent = `${name} (mm)`; label.style.cssText = 'display:block;margin-top:8px';
        const field = document.createElement('input'); field.type = 'number'; field.step = 'any'; field.min = '0'; field.value = String(initial); field.required = true; field.setAttribute('aria-label', name); field.style.width = '95%'; label.append(field); form.append(label); return field;
      };
      const insetX = input('X inset', 8), insetY = input('Y inset', 8), diameter = input('Opening diameter', 4);
      anchor.onchange = () => { insetX.disabled = ['Center', 'Top', 'Bottom'].includes(anchor.value); insetY.disabled = ['Center', 'Left', 'Right'].includes(anchor.value); };
      const add = button(form, 'Add opening', () => { if (form.reportValidity()) void this.apply({ kind: 'opening', node: this.view?.node,
        reference: reference.value, anchor: anchor.value, x: insetX.valueAsNumber, y: insetY.valueAsNumber, diameter: diameter.valueAsNumber }); });
      add.style.marginTop = '10px'; form.onsubmit = event => { event.preventDefault(); add.click(); }; inspector.append(form);
    }
    const status = document.createElement('div'); status.setAttribute('role', this.error || this.view?.diagnostics.length ? 'alert' : 'status');
    status.style.cssText = 'padding:8px 14px;border-top:1px solid #ddd;white-space:pre-wrap;max-height:120px;overflow:auto';
    status.textContent = this.error || this.view?.diagnostics.map(d => `${d.code}: ${d.message}`).join('\n') || (this.pending ? 'Resolving…' : this.draft ? 'Profile preview · not built' : this.view && !this.view.profiles.length && !this.view.emptyModels.length
      ? 'No authored Profile in this document. Use Command palette → New sketch part, or open a document containing a Profile.'
      : 'Drag a dimension or type a value. Scroll to zoom; middle-drag to pan. Solid admission is checked by Build.'); this.host.append(status);
    this.draw(true);
  }
  private draw(fit: boolean, preview = this.view) {
    const svg = this.svg; if (!svg) return; svg.replaceChildren();
    const curves = preview?.loops.flatMap(l => l.curves) ?? [];
    const points = curves.flatMap(sketchCurvePoints);
    if (fit && points.length) {
      const xs = points.map(p => p[0]), ys = points.map(p => p[1]);
      const x = Math.min(...xs), y = Math.min(...ys), w = Math.max(1, Math.max(...xs) - x), h = Math.max(1, Math.max(...ys) - y);
      const pad = Math.max(w, h) * .3; this.extent = [x - pad, -y - h - pad, w + 2 * pad, h + 2 * pad];
    }
    svg.setAttribute('viewBox', this.extent.join(' '));
    const element = (tag: string, attrs: Record<string, string>) => {
      const el = document.createElementNS(ns, tag); for (const [key, value] of Object.entries(attrs)) el.setAttribute(key, value); svg.append(el); return el;
    };
    const [x, y, w, h] = this.extent;
    for (const [a, b, c, d, color] of [[x, 0, x + w, 0, '#bb7777'], [0, y, 0, y + h, '#779977']])
      element('line', { x1: String(a), y1: String(b), x2: String(c), y2: String(d), stroke: String(color), 'stroke-width': '1', 'vector-effect': 'non-scaling-stroke', 'stroke-dasharray': '4 5' });
    for (const curve of curves) {
      const p = sketchCurvePoints(curve);
      const path = element('polyline', { points: p.map(v => `${v[0]},${-v[1]}`).join(' '), fill: 'none', stroke: this.draft ? '#ad7a17' : '#293c48', 'stroke-width': '2', 'vector-effect': 'non-scaling-stroke', 'data-member': curve.id });
      const title = document.createElementNS(ns, 'title'); title.textContent = curve.name; path.append(title);
    }
    const label = element('text', { x: String(x + w * .02), y: String(y + h * .05), 'font-size': String(w / 65), fill: '#667' }); label.textContent = '+X →    +Y ↑ · mm';
    const shape = preview?.shapes.find(s => s.id === this.selected);
    for (const candidate of preview?.shapes ?? []) {
      const marker = element('circle', { cx: String(candidate.center[0]), cy: String(-candidate.center[1]), r: String(w / 180),
        fill: candidate.id === this.selected ? '#c99630' : '#fff', stroke: '#a27b2d', 'stroke-width': '1', 'vector-effect': 'non-scaling-stroke',
        tabindex: '0', role: 'button', 'aria-label': `Select ${candidate.name}` });
      marker.style.cursor = 'pointer'; marker.addEventListener('click', () => { this.selected = candidate.id; this.render(); });
      marker.addEventListener('keydown', event => { if ((event as KeyboardEvent).key === 'Enter') { this.selected = candidate.id; this.render(); } });
    }
    const controls = shape?.controls.filter(c => c.label !== 'Center X' && c.label !== 'Center Y') ?? [];
    controls.forEach((control, index) => {
      const fontSize = 13 / Math.abs(svg.getScreenCTM?.()?.a ?? (600 / w));
      const cx = x + w * (.09 + .46 * (index % 2)), cy = y + h * .82 + fontSize * 1.7 * Math.floor(index / 2);
      const text = element('text', { x: String(cx), y: String(cy), 'font-size': String(fontSize), fill: control.editable ? '#885d0c' : '#777', tabindex: control.editable ? '0' : '-1', role: 'button', 'aria-label': `Drag ${control.label}`, 'data-control': control.id });
      text.textContent = `${control.editable ? '◆ ' : 'ƒ '}${control.label}: ${number(control.value)} ${control.unit}`;
      text.style.cursor = control.editable ? 'ew-resize' : 'help';
      text.addEventListener('pointerdown', event => { if (control.editable && !this.pending) this.beginDrag(event as PointerEvent, control); });
      text.addEventListener('keydown', event => {
        const key = (event as KeyboardEvent).key;
        if (!control.editable || !['ArrowLeft', 'ArrowRight'].includes(key)) return;
        event.preventDefault(); void this.apply({ kind: 'dimensions', node: this.view?.node, changes: [{ control: control.id, value: control.value + (key === 'ArrowRight' ? 1 : -1) }] });
      });
    });
  }
  private beginDrag(event: PointerEvent, control: SketchControl) {
    event.preventDefault(); this.cancelDrag(); const start = event.clientX;
    const matrix = this.svg!.getScreenCTM(); const mmPerPixel = matrix ? 1 / Math.abs(matrix.a) : this.extent[2] / this.svg!.clientWidth;
    let value = control.value, requested = 0, inFlight = false, finished = false;
    const request = (): SketchEditRequest => ({ kind: 'dimensions', node: this.view?.node, changes: [{ control: control.id, value }] });
    const preview = async () => {
      if (inFlight || finished) return; inFlight = true; const current = requested;
      try {
        const view = await this.callbacks.preview(request());
        if (finished || current !== requested || this.disposed) return;
        this.draft = true; this.error = ''; this.draw(false, view);
        const status = this.host.querySelector('[role="status"],[role="alert"]'); if (status) status.textContent = 'Profile preview · not built';
      } catch (error) { if (!finished && current === requested) { this.error = errorText(error); const status = this.host.querySelector('[role="status"],[role="alert"]'); if (status) status.textContent = this.error; } }
      finally { inFlight = false; if (!finished && current !== requested) this.timer = setTimeout(() => void preview(), 60); }
    };
    const move = (next: PointerEvent) => {
      value = Math.round((control.value + (next.clientX - start) * mmPerPixel) * 100) / 100; ++requested;
      clearTimeout(this.timer); this.timer = setTimeout(() => void preview(), 60);
    };
    const up = () => {
      cleanup(); this.dragCleanup = undefined; this.draft = false;
      if (value !== control.value) void this.apply(request()); else this.render();
    };
    const cleanup = () => { finished = true; clearTimeout(this.timer); window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); window.removeEventListener('pointercancel', cancel); };
    const cancel = () => { cleanup(); this.dragCleanup = undefined; this.draft = false; this.render(); };
    this.dragCleanup = cleanup;
    window.addEventListener('pointermove', move); window.addEventListener('pointerup', up, { once: true }); window.addEventListener('pointercancel', cancel, { once: true });
  }
}
