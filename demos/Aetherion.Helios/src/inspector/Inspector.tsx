import { useEffect, useMemo, useState } from 'react';
import type { Diagnostic, EditableProperty, ModelSession, UnitValue } from '@aetheris/cad';

interface Props {
  model: ModelSession | null;
  entityId: string | null;
  diagnostics: readonly Diagnostic[];
  onApply(property: EditableProperty, value: UnitValue): Promise<boolean>;
}

export function Inspector({ model, entityId, diagnostics, onApply }: Props) {
  const entity = entityId ? model?.entity(entityId) : undefined;
  const occurrence = model?.mesh.occurrences.find(item => item.semanticEntityId === entityId || item.id === entityId);
  const properties = useMemo(() => model?.properties.filter(property => property.ownerEntityId === entityId) ?? [], [entityId, model]);
  return <section className="panel inspector" aria-label="Inspector">
    <div className="panel-heading"><span>INSPECTOR</span><span className="panel-meta">{entity?.kind ?? '—'}</span></div>
    <div className="inspector-scroll">
      {entity ? <>
        <div className="entity-hero"><span className="entity-glyph">{entity.kind === 'Hole' ? '⊙' : '◆'}</span><div><strong>{entity.name}</strong><small>{entity.kind}</small></div></div>
        <dl className="identity-list"><dt>Semantic ID</dt><dd title={entity.id}>{entity.id}</dd>{entity.source && <><dt>Source</dt><dd>{entity.source.source}:{entity.source.line}</dd></>}</dl>
        {occurrence && <><div className="section-label">OCCURRENCE</div><dl className="identity-list"><dt>Occurrence</dt><dd title={occurrence.id}>{occurrence.id}</dd><dt>Definition</dt><dd>{occurrence.definitionId ?? '—'}</dd><dt>Parent</dt><dd>{occurrence.parentId ?? 'Root'}</dd><dt>Translation</dt><dd>{formatTranslation(occurrence.transform)} mm</dd></dl></>}
        <div className="section-label">PARAMETERS</div>
        {properties.length ? properties.map(property => <PropertyEditor key={property.id} property={property} onApply={onApply} />) : <div className="empty-state compact">No editable properties on this entity.</div>}
        <div className="section-label">RELATED DIAGNOSTICS</div>
        <div className="empty-state compact">{diagnostics.length ? `${diagnostics.length} model diagnostic${diagnostics.length === 1 ? '' : 's'}` : 'No diagnostics'}</div>
      </> : <div className="empty-state">Select model geometry or a tree item to inspect its semantic identity.</div>}
    </div>
  </section>;
}

function formatTranslation(transform: readonly number[]) { return `X ${formatNumber(transform[12])} · Y ${formatNumber(transform[13])} · Z ${formatNumber(transform[14])}`; }
function formatNumber(value: number | undefined) { return Number(value ?? 0).toFixed(2); }

function PropertyEditor({ property, onApply }: { property: EditableProperty; onApply(property: EditableProperty, value: UnitValue): Promise<boolean> }) {
  const [draft, setDraft] = useState(String(property.value));
  const [invalid, setInvalid] = useState(false);
  const [applying, setApplying] = useState(false);
  useEffect(() => { if (!invalid) setDraft(String(property.value)); }, [property.value, invalid]);
  const commit = async () => {
    const value = Number(draft);
    if (!Number.isFinite(value)) { setInvalid(true); return; }
    setApplying(true);
    const success = await onApply(property, { value, unit: property.unit });
    setInvalid(!success);
    setApplying(false);
  };
  return <div className={`property-editor ${invalid ? 'invalid' : ''}`}>
    <label htmlFor={property.id}>{property.name}</label>
    <div className="property-control">
      <input id={property.id} value={draft} disabled={!property.writable || applying} onChange={event => setDraft(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') void commit(); }} />
      <span>{property.unit}</span>
      <button onClick={() => void commit()} disabled={!property.writable || applying}>Apply</button>
    </div>
    <small>{invalid ? 'Invalid value retained. Last valid geometry remains.' : `Source value at line ${property.source.line}; edits are effective overrides.`}</small>
  </div>;
}
