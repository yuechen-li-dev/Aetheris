import { useMemo, useRef, useState } from 'react';
import type { Diagnostic } from '@aetheris/cad';

interface Props {
  source: string;
  diagnostics: readonly Diagnostic[];
  onSourceChange(value: string): void;
  onRebuild(): void;
  onDiagnosticClick(diagnostic: Diagnostic): void;
}

export function SourcePanel({ source, diagnostics, onSourceChange, onRebuild, onDiagnosticClick }: Props) {
  const [tab, setTab] = useState<'source' | 'diagnostics'>('source');
  const editor = useRef<HTMLTextAreaElement>(null);
  const lines = useMemo(() => Array.from({ length: Math.max(1, source.split('\n').length) }, (_, i) => i + 1), [source]);
  const jumpToDiagnostic = (diagnostic: Diagnostic) => {
    onDiagnosticClick(diagnostic);
    if (!diagnostic.source) return;
    setTab('source');
    const linesBefore = source.split('\n').slice(0, Math.max(0, diagnostic.source.line - 1));
    const offset = linesBefore.reduce((total, line) => total + line.length + 1, 0) + Math.max(0, diagnostic.source.column - 1);
    requestAnimationFrame(() => { editor.current?.focus(); editor.current?.setSelectionRange(offset, offset + 1); });
  };
  return <section className="panel source-panel" aria-label="Firmament source and diagnostics">
    <div className="bottom-tabs">
      <button className={tab === 'source' ? 'active' : ''} onClick={() => setTab('source')}>FIRMAMENT SOURCE</button>
      <button className={tab === 'diagnostics' ? 'active' : ''} onClick={() => setTab('diagnostics')}>DIAGNOSTICS <span className={diagnostics.some(item => item.severity === 'error') ? 'badge error' : 'badge'}>{diagnostics.length}</span></button>
      <span className="source-authority">AUTHORITATIVE SOURCE · OVERRIDES SHOWN IN INSPECTOR</span>
      <button className="rebuild-inline" onClick={onRebuild}>↻ Rebuild</button>
    </div>
    {tab === 'source' ? <div className="code-editor">
      <pre aria-hidden="true">{lines.join('\n')}</pre>
      <textarea ref={editor} aria-label="Firmament source" spellCheck={false} value={source} onChange={event => onSourceChange(event.target.value)} onKeyDown={event => { if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') { event.preventDefault(); onRebuild(); } }} />
    </div> : <div className="diagnostic-list">
      {diagnostics.length ? diagnostics.map((diagnostic, index) => <button key={`${diagnostic.code}-${index}`} className={`diagnostic ${diagnostic.severity}`} onClick={() => jumpToDiagnostic(diagnostic)}>
        <span className="severity-dot">●</span><strong>{diagnostic.code}</strong><span>{diagnostic.message}</span><small>{diagnostic.source ? `${diagnostic.source.source}:${diagnostic.source.line}:${diagnostic.source.column}` : ''}</small>
      </button>) : <div className="diagnostic-ok"><span>✓</span> Model is clean. No diagnostics.</div>}
    </div>}
  </section>;
}
