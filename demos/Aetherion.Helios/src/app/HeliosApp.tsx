import { useCallback, useEffect, useRef, useState } from 'react';
import type { Diagnostic, EditableProperty, ModelSession, UnitValue } from '@aetheris/cad';
import { WebSdkCadRuntime } from '../sdk/CadRuntime';
import { assemblySource, bracketSource, emptyModelSource } from '../sdk/samples';
import { appendCenteredHole } from '../commands/sourceAuthoring';
import { ModelTree } from '../model-tree/ModelTree';
import { Inspector } from '../inspector/Inspector';
import { SourcePanel } from '../source/SourcePanel';
import { Viewport } from '../viewport/Viewport';
import type { BusyState, DisplayMode, ThemeName, ViewMode } from './types';

const runtime = new WebSdkCadRuntime();

export function HeliosApp() {
  const [model, setModel] = useState<ModelSession | null>(null);
  const [source, setSource] = useState(bracketSource);
  const [sourceName, setSourceName] = useState('editable-bracket.firmament');
  const [diagnostics, setDiagnostics] = useState<readonly Diagnostic[]>([]);
  const [selectedEntityId, setSelectedEntityId] = useState<string | null>(null);
  const [selectedFaceId, setSelectedFaceId] = useState<string | null>(null);
  const [busy, setBusy] = useState<BusyState>('Initializing Aetheris');
  const [theme, setTheme] = useState<ThemeName>(() => localStorage.getItem('helios-theme') === 'sirius' ? 'sirius' : 'mars');
  const [displayMode, setDisplayMode] = useState<DisplayMode>('edges');
  const [viewMode, setViewMode] = useState<ViewMode>('perspective');
  const [viewCommand, setViewCommand] = useState('fit');
  const [runtimeName, setRuntimeName] = useState('WASM');
  const [lastTiming, setLastTiming] = useState<{ label: string; milliseconds: number } | null>(null);
  const [history, setHistory] = useState([bracketSource]);
  const [historyIndex, setHistoryIndex] = useState(0);
  const fileInput = useRef<HTMLInputElement>(null);

  const open = useCallback(async (nextSource: string, nextName: string) => {
    const started = performance.now();
    setBusy(model ? 'Compiling' : 'Initializing Aetheris');
    try {
      const result = await runtime.open(nextSource, nextName);
      setRuntimeName(runtime.info ? `WASM · ${runtime.info.packageVersion}` : 'WASM');
      setModel(result.model); setDiagnostics(result.diagnostics); setSource(nextSource); setSourceName(nextName);
      setSelectedEntityId(result.model?.tree.rootId ?? null); setSelectedFaceId(null); setViewCommand(`fit-${Date.now()}`);
    } catch (error) {
      setDiagnostics([toDiagnostic(error, 'HELIOS-INIT')]);
    } finally { setLastTiming({ label: model ? 'COMPILE' : 'INIT+COMPILE', milliseconds: performance.now() - started }); setBusy(null); }
  }, [model]);

  useEffect(() => { void open(bracketSource, 'editable-bracket.firmament'); return () => { void runtime.dispose(); }; }, []);
  useEffect(() => { document.documentElement.dataset.theme = theme; localStorage.setItem('helios-theme', theme); }, [theme]);
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') { event.preventDefault(); downloadText(source, sourceName); }
      if (event.key.toLowerCase() === 'f' && !isTyping(event.target)) setViewCommand(`fit-${Date.now()}`);
    };
    addEventListener('keydown', onKey); return () => removeEventListener('keydown', onKey);
  }, [source, sourceName]);

  const select = (entityId: string | null, faceId: string | null = null) => { setSelectedEntityId(entityId); setSelectedFaceId(faceId); };
  const rebuildSource = async () => {
    if (!model) return;
    const started = performance.now();
    setBusy('Rebuilding');
    try {
      const result = await runtime.setSource(source, sourceName);
      setDiagnostics(result.diagnostics); setModel(snapshotSession(runtime.session()));
      if (result.success) { commitHistory(source); setSelectedEntityId(runtime.session()?.tree.rootId ?? null); }
    } catch (error) { setDiagnostics([toDiagnostic(error, 'HELIOS-REBUILD')]); }
    finally { setLastTiming({ label: 'SOURCE REBUILD', milliseconds: performance.now() - started }); setBusy(null); }
  };
  const applyProperty = async (property: EditableProperty, value: UnitValue) => {
    const started = performance.now();
    setBusy('Rebuilding');
    try {
      const result = await runtime.setProperty(property.id, value);
      setDiagnostics(result.diagnostics); setModel(snapshotSession(runtime.session()));
      return result.success;
    } catch (error) { setDiagnostics([toDiagnostic(error, 'HELIOS-PROPERTY')]); return false; }
    finally { setLastTiming({ label: 'PROPERTY REBUILD', milliseconds: performance.now() - started }); setBusy(null); }
  };
  const exportStep = async () => {
    setBusy('Exporting');
    try { const bytes = await runtime.exportSTEP(); downloadBlob(new Blob([Uint8Array.from(bytes).buffer], { type: 'model/step' }), `${stripExtension(sourceName)}.step`); }
    catch (error) { setDiagnostics([toDiagnostic(error, 'HELIOS-EXPORT')]); }
    finally { setBusy(null); }
  };
  const commitHistory = (value: string) => { const next = [...history.slice(0, historyIndex + 1), value]; setHistory(next); setHistoryIndex(next.length - 1); };
  const navigateHistory = (delta: number) => { const index = Math.max(0, Math.min(history.length - 1, historyIndex + delta)); setHistoryIndex(index); setSource(history[index]); };
  const loadSample = (kind: 'part' | 'assembly' | 'new') => {
    const value = kind === 'assembly' ? assemblySource : kind === 'new' ? emptyModelSource : bracketSource;
    const name = kind === 'assembly' ? 'shared-block-assembly.firmament' : kind === 'new' ? 'untitled.firmament' : 'editable-bracket.firmament';
    setHistory([value]); setHistoryIndex(0); void open(value, name);
  };
  const addHole = () => {
    try { const target = model?.tree.nodes.find(node => ['Box', 'Part'].includes(node.kind))?.name ?? 'Body'; setSource(appendCenteredHole(source, target)); }
    catch (error) { setDiagnostics([toDiagnostic(error, 'HELIOS-AUTHOR')]); }
  };

  return <div className="helios-app">
    <header className="topbar">
      <div className="brand"><span className="brand-mark">H</span><div><strong>HELIOS</strong><small>by Aetheris</small></div></div>
      <nav className="workspace-tabs" aria-label="Workspace"><button className="active">SOLID</button><button>SURFACE</button><button disabled title="Unavailable in browser/WASM X1">SHEET METAL</button><button onClick={() => loadSample('assembly')}>ASSEMBLY</button></nav>
      <div className="toolbar">
        <button onClick={() => loadSample('new')} title="New model">＋ <span>New</span></button>
        <button onClick={() => fileInput.current?.click()}>⌑ <span>Open</span></button>
        <button onClick={addHole}>⊙ <span>Hole</span></button>
        <button onClick={() => void rebuildSource()}>↻ <span>Rebuild</span></button>
        <button className="accent-button" onClick={() => void exportStep()}>⇩ <span>Export STEP</span></button>
      </div>
      <div className="top-actions"><button onClick={() => navigateHistory(-1)} disabled={historyIndex === 0} title="Undo">↶</button><button onClick={() => navigateHistory(1)} disabled={historyIndex === history.length - 1} title="Redo">↷</button><select aria-label="Theme" value={theme} onChange={event => setTheme(event.target.value as ThemeName)}><option value="mars">Mars</option><option value="sirius">Sirius</option></select></div>
      <input ref={fileInput} hidden type="file" accept=".firmament,.txt" onChange={async event => { const file = event.target.files?.[0]; if (file) { const value = await file.text(); setHistory([value]); setHistoryIndex(0); void open(value, file.name); } }} />
    </header>
    <main className="workspace">
      <ModelTree tree={model?.tree ?? null} selectedId={selectedEntityId} onSelect={id => select(id)} />
      <div className="center-stack">
        <div className="viewport-toolbar">
          <div><button onClick={() => setViewCommand(`front-${Date.now()}`)}>FRONT</button><button onClick={() => setViewCommand(`top-${Date.now()}`)}>TOP</button><button onClick={() => setViewCommand(`right-${Date.now()}`)}>RIGHT</button><button onClick={() => setViewCommand(`iso-${Date.now()}`)}>ISO</button><button onClick={() => setViewCommand(`fit-${Date.now()}`)}>FIT</button><button onClick={() => setViewCommand(`fitselection-${Date.now()}`)}>FIT SEL</button></div>
          <div><select aria-label="Display mode" value={displayMode} onChange={event => setDisplayMode(event.target.value as DisplayMode)}><option value="shaded">Shaded</option><option value="edges">Shaded with edges</option><option value="wireframe">Wireframe</option></select><button onClick={() => setViewMode(value => value === 'perspective' ? 'orthographic' : 'perspective')}>{viewMode === 'perspective' ? 'PERSP' : 'ORTHO'}</button></div>
        </div>
        <Viewport model={model} selectedEntityId={selectedEntityId} theme={theme} displayMode={displayMode} viewMode={viewMode} viewCommand={viewCommand} onSelect={select} />
        <SourcePanel source={source} diagnostics={diagnostics} onSourceChange={setSource} onRebuild={() => void rebuildSource()} onDiagnosticClick={diagnostic => { if (diagnostic.source) setSelectedEntityId(null); }} />
      </div>
      <Inspector model={model} entityId={selectedEntityId} diagnostics={diagnostics} onApply={applyProperty} />
    </main>
    <footer className="statusbar"><span className="status-ready"><i />{busy ?? (model ? 'READY' : 'NO MODEL')}</span><span>{runtimeName}</span><span>REV {model?.revision ?? '—'}</span><span>{model?.mesh.definitions.length ?? 0} DEFS · {model?.mesh.occurrences.length ?? 0} OCC</span><span className={diagnostics.length ? 'has-diagnostics' : ''}>{diagnostics.length ? `⚠ ${diagnostics.length}` : '✓ 0'} DIAGNOSTICS</span>{lastTiming && <span>{lastTiming.label} {lastTiming.milliseconds.toFixed(0)}MS</span>}<span className="status-selection">{selectedEntityId ? `${model?.entity(selectedEntityId)?.name ?? selectedEntityId}${selectedFaceId ? ` · ${selectedFaceId}` : ''}` : 'NO SELECTION'}</span><span>{viewMode.toUpperCase()}</span><span>{theme.toUpperCase()}</span></footer>
    {busy && <div className="busy-overlay" role="status"><span className="spinner" />{busy}</div>}
  </div>;
}

function stripExtension(name: string) { return name.replace(/\.[^.]+$/, ''); }
function snapshotSession(session: ModelSession | null) { return session ? Object.assign(Object.create(Object.getPrototypeOf(session)), session) as ModelSession : null; }
function isTyping(target: EventTarget | null) { return target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement; }
function toDiagnostic(error: unknown, code: string): Diagnostic { return { severity: 'error', code, message: error instanceof Error ? error.message : String(error) }; }
function downloadText(value: string, name: string) { downloadBlob(new Blob([value], { type: 'text/plain' }), name); }
function downloadBlob(blob: Blob, name: string) { const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = name; anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 0); }
