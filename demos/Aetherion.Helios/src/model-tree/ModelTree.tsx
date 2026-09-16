import { useMemo, useState } from 'react';
import type { ModelTree as SdkModelTree, ModelTreeNode } from '@aetheris/cad';

interface Props {
  tree: SdkModelTree | null;
  selectedId: string | null;
  onSelect(id: string): void;
}

export function ModelTree({ tree, selectedId, onSelect }: Props) {
  const [query, setQuery] = useState('');
  const [kind, setKind] = useState('');
  const nodes = useMemo(() => new Map(tree?.nodes.map(node => [node.id, node]) ?? []), [tree]);
  const kinds = useMemo(() => [...new Set(tree?.nodes.map(node => node.kind) ?? [])].sort((left, right) => left.localeCompare(right)), [tree]);
  const selectedKind = kinds.includes(kind) ? kind : '';
  const visible = useMemo(() => {
    const normalized = query.trim().toLowerCase();
    if (!normalized && !selectedKind) return null;

    const matches = new Set(tree?.nodes
      .filter(node => (!normalized || `${node.name} ${node.kind}`.toLowerCase().includes(normalized)) && (!selectedKind || node.kind === selectedKind))
      .map(node => node.id));
    for (const id of [...matches]) {
      let parentId = nodes.get(id)?.parentId;
      while (parentId) {
        matches.add(parentId);
        parentId = nodes.get(parentId)?.parentId;
      }
    }
    return matches;
  }, [nodes, query, selectedKind, tree]);

  return <section className="panel model-browser" aria-label="Model browser">
    <div className="panel-heading"><span>MODEL</span><span className="panel-meta">{tree?.nodes.length ?? 0}</span></div>
    <div className="tree-filters">
      <div className="tree-search"><span>⌕</span><input aria-label="Filter model" value={query} onChange={event => setQuery(event.target.value)} placeholder="Filter model" /></div>
      <select aria-label="Filter model by kind" value={selectedKind} onChange={event => setKind(event.target.value)}>
        <option value="">All</option>
        {kinds.map(option => <option key={option} value={option}>{option}</option>)}
      </select>
    </div>
    <div className="tree-scroll">
      {tree ? <TreeNode node={nodes.get(tree.rootId)!} nodes={nodes} selectedId={selectedId} visible={visible} onSelect={onSelect} depth={0} /> : <div className="empty-state">No model loaded</div>}
    </div>
  </section>;
}

function TreeNode({ node, nodes, selectedId, visible, onSelect, depth }: { node: ModelTreeNode; nodes: Map<string, ModelTreeNode>; selectedId: string | null; visible: Set<string> | null; onSelect(id: string): void; depth: number }) {
  const [open, setOpen] = useState(true);
  if (!node) return null;
  const children = node.children.map(id => nodes.get(id)).filter(Boolean) as ModelTreeNode[];
  const matches = !visible || visible.has(node.id) || children.some(child => visible.has(child.id));
  if (!matches) return null;
  return <div className="tree-branch">
    <div className={`tree-row ${selectedId === node.id ? 'selected' : ''}`} style={{ paddingLeft: 10 + depth * 14 }} onClick={() => onSelect(node.id)} data-entity-id={node.id}>
      <button className="disclosure" aria-label={`${open ? 'Collapse' : 'Expand'} ${node.name}`} onClick={event => { event.stopPropagation(); setOpen(value => !value); }}>{children.length ? (open ? '⌄' : '›') : '·'}</button>
      <span className={`kind-icon kind-${node.kind.toLowerCase()}`}>{kindGlyph(node.kind)}</span>
      <span className="tree-name">{node.name}</span>
      <span className="tree-kind">{node.kind}</span>
    </div>
    {open && children.map(child => <TreeNode key={child.id} node={child} nodes={nodes} selectedId={selectedId} visible={visible} onSelect={onSelect} depth={depth + 1} />)}
  </div>;
}

function kindGlyph(kind: string) {
  const normalized = kind.toLowerCase();
  if (normalized.includes('assembly')) return '◇';
  if (normalized.includes('hole')) return '⊙';
  if (normalized.includes('model') || normalized.includes('part')) return '◆';
  return '▧';
}
